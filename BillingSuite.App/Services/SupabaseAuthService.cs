using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Integrates natively with Supabase Auth REST API (GoTrue) for email OTP signup,
    /// verification, resending, and password recovery.
    /// </summary>
    public static class SupabaseAuthService
    {
        private static readonly HttpClient s_httpClient = new();

        public static string SupabaseUrl => CloudDbConfig.GetSupabaseUrl().TrimEnd('/');

        public static string SupabaseAnonKey => CloudDbConfig.GetSupabaseAnonKey();

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(SupabaseAnonKey);

        /// <summary>
        /// Sends signup request to Supabase Auth REST API, which dispatches a real OTP verification email.
        /// </summary>
        public static async Task<(bool ok, string message)> SignUpAsync(string email, string password, string fullName)
        {
            var anonKey = SupabaseAnonKey;
            if (string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "Supabase Anon Key is required to send real Supabase Auth emails. Please set 'SupabaseAnonKey' in appsettings.json.");
            }

            try
            {
                var endpoint = $"{SupabaseUrl}/auth/v1/signup";
                var payload = new
                {
                    email = email,
                    password = password,
                    data = new { full_name = fullName }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("apikey", anonKey);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", anonKey);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var res = await s_httpClient.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    return (true, $"A Supabase verification OTP email has been sent to {email}. Please check your inbox and spam folder.");
                }

                var obj = JObject.Parse(json);
                var err = obj["msg"]?.ToString() ?? obj["error_description"]?.ToString() ?? obj["message"]?.ToString() ?? "Supabase signup failed.";
                return (false, err);
            }
            catch (Exception ex)
            {
                return (false, $"Supabase Auth request error: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies 6-digit OTP code against Supabase Auth REST API.
        /// </summary>
        public static async Task<(bool ok, string message, string? accessToken)> VerifyOtpAsync(string email, string token, string type = "signup")
        {
            var anonKey = SupabaseAnonKey;
            if (string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "Supabase Anon Key is required for OTP verification.", null);
            }

            try
            {
                var endpoint = $"{SupabaseUrl}/auth/v1/verify";
                var payload = new
                {
                    type = type,
                    email = email,
                    token = token
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("apikey", anonKey);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", anonKey);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var res = await s_httpClient.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    var obj = JObject.Parse(json);
                    var accessTok = obj["access_token"]?.ToString();
                    return (true, "Supabase OTP verified successfully!", accessTok);
                }

                var errObj = JObject.Parse(json);
                var err = errObj["msg"]?.ToString() ?? errObj["error_description"]?.ToString() ?? errObj["message"]?.ToString() ?? "Invalid or expired OTP code.";
                return (false, err, null);
            }
            catch (Exception ex)
            {
                return (false, $"Supabase Auth verification error: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Updates the authenticated user's password in Supabase Auth GoTrue.
        /// Requires a valid user Access Token (e.g. from password recovery verify).
        /// </summary>
        public static async Task<(bool ok, string message)> UpdateUserPasswordAsync(string accessToken, string newPassword)
        {
            var anonKey = SupabaseAnonKey;
            if (string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "Supabase Anon Key is required.");
            }

            try
            {
                var endpoint = $"{SupabaseUrl}/auth/v1/user";
                var payload = new { password = newPassword };

                using var req = new HttpRequestMessage(HttpMethod.Put, endpoint);
                req.Headers.Add("apikey", anonKey);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var res = await s_httpClient.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    return (true, "Password updated in Supabase Auth successfully.");
                }

                var errObj = JObject.Parse(json);
                var err = errObj["msg"]?.ToString() ?? errObj["error_description"]?.ToString() ?? errObj["message"]?.ToString() ?? "Failed to update password in Supabase Auth.";
                return (false, err);
            }
            catch (Exception ex)
            {
                return (false, $"Supabase user password update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Resends signup OTP email via Supabase Auth REST API.
        /// </summary>
        public static async Task<(bool ok, string message)> ResendOtpAsync(string email)
        {
            var anonKey = SupabaseAnonKey;
            if (string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "Supabase Anon Key is required.");
            }

            try
            {
                var endpoint = $"{SupabaseUrl}/auth/v1/resend";
                var payload = new
                {
                    type = "signup",
                    email = email
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("apikey", anonKey);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", anonKey);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var res = await s_httpClient.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    return (true, $"Supabase verification code resent to {email}.");
                }

                var obj = JObject.Parse(json);
                var err = obj["msg"]?.ToString() ?? obj["message"]?.ToString() ?? "Failed to resend OTP.";
                return (false, err);
            }
            catch (Exception ex)
            {
                return (false, $"Supabase Auth resend error: {ex.Message}");
            }
        }

        /// <summary>
        /// Triggers password reset recovery email via Supabase Auth REST API.
        /// </summary>
        public static async Task<(bool ok, string message)> SendPasswordResetEmailAsync(string email)
        {
            var anonKey = SupabaseAnonKey;
            if (string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "Supabase Anon Key is required.");
            }

            try
            {
                var endpoint = $"{SupabaseUrl}/auth/v1/recover";
                var payload = new { email = email };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("apikey", anonKey);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", anonKey);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var res = await s_httpClient.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    return (true, $"Password reset OTP email sent to {email}.");
                }

                var obj = JObject.Parse(json);
                var err = obj["msg"]?.ToString() ?? obj["message"]?.ToString() ?? "Failed to send password reset email.";
                return (false, err);
            }
            catch (Exception ex)
            {
                return (false, $"Supabase password recovery error: {ex.Message}");
            }
        }

        /// <summary>
        /// Authenticates user against Supabase Auth REST API (GoTrue) using email and password.
        /// </summary>
        public static async Task<(bool ok, string message, string? accessToken, string? fullName)> SignInWithPasswordAsync(string email, string password)
        {
            var anonKey = SupabaseAnonKey;
            if (string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "Supabase Anon Key is required for authentication.", null, null);
            }

            try
            {
                var endpoint = $"{SupabaseUrl}/auth/v1/token?grant_type=password";
                var payload = new
                {
                    email = email,
                    password = password
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("apikey", anonKey);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", anonKey);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var res = await s_httpClient.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    var obj = JObject.Parse(json);
                    var accessTok = obj["access_token"]?.ToString();
                    var userObj = obj["user"];
                    var fullName = userObj?["user_metadata"]?["full_name"]?.ToString() ?? userObj?["email"]?.ToString();
                    return (true, "Authenticated via Supabase Auth successfully!", accessTok, fullName);
                }

                var errObj = JObject.Parse(json);
                var err = errObj["msg"]?.ToString() ?? errObj["error_description"]?.ToString() ?? errObj["message"]?.ToString() ?? "Invalid login credentials.";
                return (false, err, null, null);
            }
            catch (Exception ex)
            {
                return (false, $"Supabase Auth login error: {ex.Message}", null, null);
            }
        }
    }
}
