using System;
using System.Collections.Concurrent;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// In-process login rate limiter.
    ///
    /// Rules:
    ///   - After <see cref="MaxAttempts"/> consecutive failed attempts for the same
    ///     email address, further sign-in attempts are blocked for a lockout period.
    ///   - The lockout duration doubles with each additional failed block
    ///     (exponential back-off), capped at <see cref="MaxLockoutMinutes"/>.
    ///   - A successful sign-in clears the failure counter for that email.
    ///   - All state is in-memory and resets when the application restarts.
    ///     This is intentional — the app is offline-first and designed for a
    ///     single trusted device. It defends against automated scripted attacks.
    /// </summary>
    public static class LoginRateLimiter
    {
        /// <summary>Maximum consecutive failures before lockout.</summary>
        public const int MaxAttempts = 5;

        /// <summary>Base lockout duration in minutes (doubles each block).</summary>
        public const int BaseLockoutMinutes = 5;

        /// <summary>Cap on lockout duration regardless of how many blocks.</summary>
        public const int MaxLockoutMinutes = 60;

        private sealed class Entry
        {
            public int FailCount { get; set; }
            public int LockoutBlockCount { get; set; }   // how many times we've blocked
            public DateTime? LockedUntil { get; set; }
        }

        private static readonly ConcurrentDictionary<string, Entry> _state
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns (allowed: true, waitSeconds: 0) when the attempt is permitted.
        /// Returns (allowed: false, waitSeconds: N) when the account is locked out.
        /// </summary>
        public static (bool allowed, int waitSeconds, int attemptsLeft) Check(string email)
        {
            email = Normalize(email);
            if (!_state.TryGetValue(email, out var entry))
                return (true, 0, MaxAttempts);

            if (entry.LockedUntil.HasValue && DateTime.UtcNow < entry.LockedUntil.Value)
            {
                var wait = (int)Math.Ceiling((entry.LockedUntil.Value - DateTime.UtcNow).TotalSeconds);
                return (false, wait, 0);
            }

            // Lockout expired — still keep the fail count so the next failure
            // immediately triggers another (longer) lockout.
            int left = Math.Max(0, MaxAttempts - entry.FailCount);
            return (true, 0, left);
        }

        /// <summary>
        /// Records a failed attempt. Applies lockout if the threshold is reached.
        /// </summary>
        public static void RecordFailure(string email)
        {
            email = Normalize(email);
            var entry = _state.GetOrAdd(email, _ => new Entry());

            lock (entry)
            {
                entry.FailCount++;

                if (entry.FailCount >= MaxAttempts)
                {
                    entry.LockoutBlockCount++;
                    int minutes = Math.Min(
                        BaseLockoutMinutes * (int)Math.Pow(2, entry.LockoutBlockCount - 1),
                        MaxLockoutMinutes);
                    entry.LockedUntil = DateTime.UtcNow.AddMinutes(minutes);
                    entry.FailCount = 0; // reset counter for next window
                    try
                    {
                        AuditLogService.Log(AuditLogService.Events.LoginLockout, email, $"Locked for {minutes} min (block #{entry.LockoutBlockCount})");
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Records a successful sign-in — clears all failure state for this email.
        /// </summary>
        public static void RecordSuccess(string email)
        {
            _state.TryRemove(Normalize(email), out _);
        }

        /// <summary>
        /// Returns a human-readable lockout message, or null if not locked.
        /// </summary>
        public static string? GetLockoutMessage(string email)
        {
            var (allowed, waitSeconds, _) = Check(email);
            if (allowed) return null;

            if (waitSeconds >= 60)
            {
                int mins = (int)Math.Ceiling(waitSeconds / 60.0);
                return $"Too many failed attempts. Please try again in {mins} minute{(mins == 1 ? "" : "s")}.";
            }
            return $"Too many failed attempts. Please try again in {waitSeconds} second{(waitSeconds == 1 ? "" : "s")}.";
        }

        private static string Normalize(string email) => (email ?? "").Trim().ToLowerInvariant();
    }
}
