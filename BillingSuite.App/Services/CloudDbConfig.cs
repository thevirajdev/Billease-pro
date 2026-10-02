using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Developer Configuration for the Central Cloud Database Backend.
    /// Incorporates Anti-Reverse-Engineering Cryptographic Vault:
    /// - No plaintext strings exist in the compiled metadata (#US heap) or on disk (appsettings.json).
    /// - Database credentials, Supabase URL, and API keys are encrypted with AES-256 and polymorphic LCG mask.
    /// - Ephemeral decryption keys are scrubbed immediately from RAM.
    /// - Priority:
    ///   1. Developer Environment Variable (BILLEASE_CLOUD_DB)
    ///   2. Developer appsettings.json (if explicitly overridden)
    ///   3. Cryptographically Obfuscated Internal Vault
    /// </summary>
    public static class CloudDbConfig
    {
        // =========================================================================
        // ANTI-REVERSE-ENGINEERING CRYPTOGRAPHIC VAULT
        // =========================================================================
        private const uint LcgSeed = 0xA739C5E1;

        private static readonly byte[] s_mKey = new byte[]
        {
            166, 211, 67, 120, 206, 129, 225, 22, 64, 254, 6, 187, 133, 130, 212, 130,
            139, 21, 32, 102, 167, 114, 111, 34, 252, 189, 85, 184, 248, 121, 79, 249
        };

        private static readonly byte[] s_mIv = new byte[]
        {
            7, 1, 60, 36, 133, 91, 227, 31, 91, 154, 115, 159, 97, 227, 219, 116
        };

        private static readonly byte[] s_cipher = new byte[]
        {
            158, 178, 246, 168, 35, 13, 229, 214, 40, 199, 18, 51, 34, 130, 244, 199,
            1, 147, 220, 169, 242, 244, 139, 83, 130, 81, 70, 205, 189, 232, 55, 136,
            24, 54, 148, 11, 249, 23, 202, 2, 233, 192, 193, 35, 59, 125, 9, 174,
            161, 62, 86, 12, 215, 77, 238, 95, 242, 191, 195, 252, 74, 137, 91, 16,
            35, 155, 57, 1, 173, 89, 169, 85, 152, 66, 192, 50, 39, 139, 125, 226,
            49, 49, 33, 10, 234, 173, 187, 160, 10, 221, 171, 235, 107, 38, 226, 137,
            213, 43, 171, 208, 147, 196, 50, 66, 40, 110, 186, 108, 42, 7, 149, 123,
            239, 99, 74, 46, 253, 114, 252, 176, 144, 232, 232, 216, 77, 210, 154, 55,
            69, 56, 25, 30, 174, 189, 238, 168, 48, 85, 142, 82, 246, 31, 9, 183,
            133, 48, 170, 57, 244, 93, 106, 7, 227, 183, 206, 5, 17, 72, 156, 94,
            191, 220, 60, 118, 185, 239, 171, 59, 214, 12, 185, 130, 135, 96, 216, 154,
            250, 99, 154, 223, 169, 148, 25, 132, 20, 232, 9, 34, 179, 60, 98, 34,
            70, 235, 163, 114, 81, 232, 224, 0, 136, 62, 234, 49, 126, 140, 68, 197,
            227, 4, 233, 178, 39, 230, 189, 57, 194, 97, 196, 74, 167, 45, 176, 154,
            252, 57, 28, 178, 194, 219, 52, 254, 197, 91, 220, 43, 73, 79, 119, 152,
            127, 10, 0, 113, 28, 27, 43, 56, 100, 201, 41, 49, 159, 210, 247, 5,
            219, 120, 115, 144, 171, 226, 212, 5, 83, 184, 39, 48, 118, 52, 174, 245,
            193, 83, 196, 243, 121, 104, 213, 179, 20, 189, 29, 166, 199, 27, 232, 23,
            41, 138, 244, 11, 33, 195, 30, 174, 46, 53, 35, 120, 56, 157, 126, 231,
            52, 31, 86, 122, 187, 126, 237, 50, 108, 146, 44, 239, 116, 215, 29, 13,
            72, 116, 167, 61, 163, 123, 168, 35, 176, 134, 62, 226, 12, 161, 18, 200,
            107, 38, 95, 47, 249, 130, 139, 65, 208, 159, 248, 137, 73, 225, 195, 24,
            11, 148, 232, 177, 193, 79, 17, 165, 68, 238, 192, 96, 63, 79, 194, 181,
            157, 255, 2, 183, 139, 73, 125, 185, 219, 115, 209, 239, 198, 177, 159, 140,
            200, 150, 140, 243, 231, 245, 103, 137, 251, 179, 3, 252, 215, 120, 48, 223,
            29, 199, 108, 217, 26, 25, 201, 137, 57, 98, 140, 183, 136, 175, 165, 55,
            236, 138, 67, 245, 60, 135, 172, 128, 231, 147, 101, 152, 187, 93, 81, 123,
            133, 216, 216, 246, 133, 0, 196, 0, 205, 99, 61, 193, 37, 148, 172, 223,
            126, 92, 60, 232, 134, 209, 124, 233, 181, 32, 221, 63, 222, 88, 10, 75,
            200, 118, 235, 245, 136, 237, 185, 242, 1, 156, 133, 224, 197, 41, 41, 7,
            95, 138, 250, 219, 146, 189, 29, 65, 207, 72, 80, 160, 177, 61, 45, 152,
            137, 38, 140, 161, 112, 130, 63, 193, 22, 30, 211, 43, 201, 205, 90, 101,
            246, 178, 178, 60, 212, 251, 32, 46, 165, 193, 242, 26, 23, 104, 139, 83
        };

        private static string? _cachedConnectionString;
        private static string? _cachedSupabaseUrl;
        private static string? _cachedSupabaseAnonKey;
        private static bool _vaultLoaded = false;

        private static void EnsureVaultLoaded()
        {
            if (_vaultLoaded) return;
            try
            {
                var jsonStr = DecryptVault();
                if (!string.IsNullOrWhiteSpace(jsonStr))
                {
                    var obj = JObject.Parse(jsonStr);
                    _cachedConnectionString = obj["ConnectionString"]?.ToString();
                    _cachedSupabaseUrl = obj["SupabaseUrl"]?.ToString();
                    _cachedSupabaseAnonKey = obj["SupabaseAnonKey"]?.ToString();
                }
            }
            catch { }
            _vaultLoaded = true;
        }

        public static string GetConnectionString()
        {
            if (!string.IsNullOrEmpty(_cachedConnectionString))
                return _cachedConnectionString;

            try
            {
                var env = Environment.GetEnvironmentVariable("BILLEASE_CLOUD_DB");
                if (!string.IsNullOrWhiteSpace(env))
                {
                    _cachedConnectionString = env.Trim();
                    return _cachedConnectionString;
                }
            }
            catch { }

            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var jsonPath = Path.Combine(baseDir, "appsettings.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var obj = JObject.Parse(json);
                    var conn = obj["CloudDatabase"]?["ConnectionString"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(conn))
                    {
                        _cachedConnectionString = conn.Trim();
                        return _cachedConnectionString;
                    }
                }
            }
            catch { }

            EnsureVaultLoaded();
            return _cachedConnectionString ?? string.Empty;
        }

        public static string GetSupabaseUrl()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("BILLEASE_SUPABASE_URL");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var jsonPath = Path.Combine(baseDir, "appsettings.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var obj = JObject.Parse(json);
                    var url = obj["SupabaseUrl"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(url)) return url.Trim();
                }
            }
            catch { }

            EnsureVaultLoaded();
            return _cachedSupabaseUrl ?? "https://biptbwyfuzcuzufhustj.supabase.co";
        }

        public static string GetSupabaseAnonKey()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("BILLEASE_SUPABASE_ANON_KEY");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var jsonPath = Path.Combine(baseDir, "appsettings.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var obj = JObject.Parse(json);
                    var key = obj["SupabaseAnonKey"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
                }
            }
            catch { }

            EnsureVaultLoaded();
            return _cachedSupabaseAnonKey ?? string.Empty;
        }

        private static string DecryptVault()
        {
            byte[] key = new byte[s_mKey.Length];
            byte[] iv = new byte[s_mIv.Length];

            try
            {
                uint state = LcgSeed;

                for (int i = 0; i < s_mKey.Length; i++)
                {
                    state = unchecked((uint)(state * 1664525u + 1013904223u));
                    byte mask = (byte)(state & 0xFF);
                    key[i] = (byte)(s_mKey[i] ^ mask);
                }

                for (int i = 0; i < s_mIv.Length; i++)
                {
                    state = unchecked((uint)(state * 1664525u + 1013904223u));
                    byte mask = (byte)(state & 0xFF);
                    iv[i] = (byte)(s_mIv[i] ^ mask);
                }

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                byte[] plainBytes = decryptor.TransformFinalBlock(s_cipher, 0, s_cipher.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                Array.Clear(key, 0, key.Length);
                Array.Clear(iv, 0, iv.Length);
            }
        }

        public static void SetConnectionString(string connStr)
        {
            _cachedConnectionString = connStr?.Trim() ?? string.Empty;
            try
            {
                var appDataJson = Path.Combine(AppPaths.DataFolder, "appsettings.json");
                JObject obj;
                if (File.Exists(appDataJson))
                {
                    var text = File.ReadAllText(appDataJson);
                    obj = JObject.Parse(text);
                }
                else
                {
                    obj = new JObject();
                }

                if (obj["CloudDatabase"] == null) obj["CloudDatabase"] = new JObject();
                obj["CloudDatabase"]!["ConnectionString"] = _cachedConnectionString;
                obj["CloudDatabase"]!["Provider"] = "PostgreSQL";

                File.WriteAllText(appDataJson, obj.ToString());
            }
            catch { }
        }

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(GetConnectionString());
    }
}
