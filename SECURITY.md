# Security Policy

## Overview

Billease Pro is an offline-first business billing and ERP application that handles sensitive financial records, customer data, inventory, and business credentials. We take security seriously and are committed to responsible disclosure and timely remediation of vulnerabilities.

---

## Supported Versions

Only the latest release is actively maintained with security fixes.

| Version | Supported |
| :------ | :-------: |
| Latest (main) | ✅ Yes |
| Older releases | ❌ No |

> **Important:** Always keep Billease Pro updated to the latest release to receive all security patches.

---

## Reporting a Vulnerability

**Please do NOT report security vulnerabilities as public GitHub Issues.**

If you discover a security vulnerability, report it privately so we can address it before public disclosure.

### How to Report

**Email:** Contact the lead developer directly via GitHub — [@thevirajdev](https://github.com/thevirajdev)

When reporting, please include:

- A clear description of the vulnerability
- Steps to reproduce the issue
- The potential impact (data exposure, privilege escalation, crash, etc.)
- Your environment (Windows version, Billease Pro version)
- Any proof-of-concept code or screenshots (if applicable)

### What to Expect

| Timeline | Action |
| :------- | :----- |
| **Within 48 hours** | Acknowledgement of your report |
| **Within 7 days** | Initial assessment and severity classification |
| **Within 30 days** | Patch released (for critical/high severity) |
| **After patch** | Public disclosure credit (with your permission) |

---

## Security Architecture

### 🔐 Authentication & Passwords

- Passwords are hashed using **PBKDF2-SHA256** with a **per-user random 16-byte salt** and **210,000 iterations** (NIST SP 800-132 compliant).
- Plaintext passwords are **never stored** anywhere — not in the database, logs, or memory beyond the duration of the hash operation.
- Password verification uses **`CryptographicOperations.FixedTimeEquals`** to prevent timing-based side-channel attacks.
- The iteration count is stored per-user row, allowing future increases without invalidating existing accounts.
- The minimum password length is enforced at 6 characters during registration and password changes.

### 📧 Account Verification

- New account registrations require **email OTP verification** (6-digit code) before the account is activated.
- OTP codes expire after **10 minutes** and are generated using `RandomNumberGenerator.GetInt32` (cryptographically secure RNG).
- When Supabase Auth is configured, OTP verification is delegated to Supabase's Auth API.

### 🗄️ Data Isolation

- All business data (invoices, products, customers, purchases, payments) is **strictly isolated per user account** via EF Core query filters.
- Every row is tagged with `OwnerUserId`; when a user is signed in, the query filter enforces `WHERE OwnerUserId = @currentUser` — one business can never see another's records.
- Legacy/unowned rows are claimed atomically on first login.

### 💾 Local Database

- Data is stored in **SQLite** (`billing.db`) on the local device.
- The database file is located in the application data directory — not in a web-accessible or publicly shared path.
- **Recommendation:** Enable Windows full-disk encryption (BitLocker) to protect the database file at rest.

### ☁️ Cloud Synchronization

- Cloud sync is **opt-in** and only activates when a Supabase connection string is configured.
- Data is transmitted over **HTTPS/TLS** exclusively — no plaintext cloud communication.
- Background sync is debounced and rate-limited to prevent flooding the cloud endpoint.
- The `_isPulling` flag prevents concurrent sync operations that could cause data corruption.

### 🔑 Credentials & Secrets

- Cloud connection strings and API keys are **not hardcoded** in the source code.
- Secrets are loaded at runtime from configuration — never committed to the repository.
- The `.gitignore` excludes build outputs, local configuration files, and sensitive artifacts.

### 🔒 Brute-Force & Rate Limiting Protection

- In-process rate limiting via `LoginRateLimiter` enforces lockout against automated credential stuffing.
- After **5 consecutive failed attempts** for any account, sign-in is blocked for **5 minutes**.
- Subsequent lockout windows use exponential backoff (5 → 10 → 20 → 40 → 60 minutes).
- Live countdown timer is presented in the UI showing remaining lockout time.
- Successful sign-in clears the failure counter immediately.

### 📋 Security Audit Logging

- Append-only security audit trail implemented via `AuditLogService`.
- Events are written to daily rotating log files in `%LOCALAPPDATA%\BillingSuite\audit_logs\audit_YYYY-MM-DD.log`.
- Tracks critical security events: `LOGIN_SUCCESS`, `LOGIN_FAILED`, `LOGIN_LOCKOUT`, `LOGOUT`, `PASSWORD_CHANGE`, and `ACCOUNT_CREATED`.

### ⏱️ Session Management (Offline POS Design)

- Designed specifically for retail counters and billing desks: sessions remain persistent throughout active business hours to avoid disruptive auto-lockouts during peak billing rushes.
- Users can manually sign out or switch accounts anytime from the Settings panel.

---

## Known Security Considerations

| Area | Status | Notes |
| :--- | :----- | :---- |
| Password hashing | ✅ Secure | PBKDF2-SHA256, 210k iterations, per-user salt |
| Timing-safe comparison | ✅ Secure | `FixedTimeEquals` used |
| OTP expiry | ✅ Implemented | 10-minute expiry, CSPRNG |
| Data isolation | ✅ Enforced | Per-user EF query filters |
| Transport encryption | ✅ HTTPS only | TLS for all cloud calls |
| Brute-force protection | ✅ Implemented | 5 attempts → 5-min lockout, exponential back-off up to 60 min with UI countdown |
| Audit logging | ✅ Implemented | Append-only security audit trail in `%LOCALAPPDATA%\BillingSuite\audit_logs\` |
| Database encryption at rest | ⚠️ OS-level | Relies on BitLocker / OS disk encryption |
| Session management | ℹ️ POS Mode | Persistent local session by design (no auto-timeout to prevent billing interruption) |

---

## Security Best Practices for Deployment

1. **Enable BitLocker** (or equivalent full-disk encryption) on any device running Billease Pro to protect the local SQLite database at rest.
2. **Use a strong, unique password** for your Billease Pro account. Avoid reusing passwords from other services.
3. **Keep Windows updated** — OS-level security patches protect the underlying environment.
4. **Restrict physical access** to the billing computer. Billease Pro uses auto-login for convenience; physical access = data access.
5. **Regular backups** — Use `Ctrl + Shift + B` to create encrypted `.bak` snapshots and store them offsite or in a secure cloud location.
6. **Do not share cloud connection strings** or Supabase API keys. Treat them as passwords.

---

## Scope

The following are **in scope** for security reports:

- Authentication bypass or privilege escalation
- User data isolation failures (one account seeing another's records)
- Remote code execution or injection vulnerabilities
- Sensitive data exposure (passwords, credentials, financial records)
- Cloud sync security issues (MITM, data leakage)

The following are **out of scope**:

- Vulnerabilities requiring physical access to an already unlocked device
- Social engineering attacks
- Issues in third-party dependencies not directly controlled by this project
- Missing features that are not security vulnerabilities

---

## Dependency Security

This project uses the following security-sensitive dependencies:

| Dependency | Purpose |
| :--------- | :------ |
| `Microsoft.EntityFrameworkCore.Sqlite` | Local database ORM |
| `Npgsql` | PostgreSQL cloud database driver |
| `System.Security.Cryptography` | Password hashing (PBKDF2-SHA256) |

Run `dotnet list package --vulnerable` periodically to check for known CVEs in dependencies.

---

## Attribution

We are grateful to security researchers who responsibly disclose vulnerabilities. With your permission, your name will be credited in the release notes of the patch that fixes the issue you reported.

---

*Copyright © 2026 NexaAutomate. All rights reserved.*
