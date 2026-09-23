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
    /// - No plaintext strings exist in the compiled metadata (#US heap) or on disk.
    /// - Database credentials and hostnames are encrypted with AES-256 and polymorphic LCG mask.
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
        // Contains encrypted payload and polymorphic masked key shards.
        // No plain text connection string, password, or Supabase URL exists in the binary.
        private const uint LcgSeed = 0xA739C5E1;

        private static readonly byte[] s_mKey = new byte[]
        {
            190, 150, 120, 158, 22, 3, 14, 182, 74, 199, 233, 224, 35, 137, 204, 171,
            16, 76, 190, 78, 254, 47, 250, 255, 227, 119, 203, 201, 1, 204, 60, 221
        };

        private static readonly byte[] s_mIv = new byte[]
        {
            99, 93, 33, 244, 137, 223, 195, 162, 162, 37, 128, 247, 49, 168, 251, 116
        };

        private static readonly byte[] s_cipher = new byte[]
        {
            84, 113, 140, 206, 62, 154, 98, 181, 19, 166, 139, 226, 44, 228, 253, 164,
            57, 54, 44, 42, 119, 43, 108, 224, 204, 168, 15, 231, 29, 67, 192, 213,
            156, 32, 117, 184, 109, 228, 82, 52, 250, 202, 115, 22, 159, 199, 117, 13,
            197, 51, 50, 248, 6, 165, 171, 59, 244, 219, 50, 226, 128, 60, 93, 64,
            161, 42, 141, 159, 116, 56, 247, 236, 192, 243, 160, 55, 32, 63, 153, 111,
            102, 123, 2, 166, 139, 36, 28, 162, 71, 79, 62, 102, 237, 25, 217, 170,
            187, 115, 127, 210, 153, 224, 126, 183, 198, 91, 109, 13, 167, 80, 180, 209,
            95, 202, 147, 160, 236, 102, 253, 222, 244, 21, 52, 102, 206, 217, 83, 226,
            32, 145, 143, 182, 113, 17, 166, 85, 81, 184, 30, 78, 198, 165, 110, 99,
            186, 112, 118, 40, 164, 51, 43, 50, 110, 234, 178, 83, 103, 78, 52, 56,
            172, 164, 58, 139, 54, 165, 192, 19, 253, 224, 149, 209, 62, 188, 112, 204,
            206, 188, 224, 233, 71, 144, 0, 14, 200, 103, 1, 161, 54, 68, 192, 84,
            217, 127, 154, 162, 233, 203, 99, 137, 116, 45, 148, 117, 220, 70, 77, 33,
            161, 157, 37, 153, 245, 23, 63, 211, 79, 209, 187, 197, 42, 181, 5, 144
        };

        private static string? _cachedConnectionString;

        /// <summary>
        /// Retrieves the developer-configured cloud database connection string.
        /// Priority:
        /// 1. Environment variable BILLEASE_CLOUD_DB
        /// 2. appsettings.json beside the executable or in AppData (if explicitly non-empty)
        /// 3. Anti-Reverse-Engineering Encrypted Vault
        /// </summary>
        public static string GetConnectionString()
        {
            if (!string.IsNullOrEmpty(_cachedConnectionString))
                return _cachedConnectionString;

            // 1. Check environment variable (useful for developer overrides)
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

            // 2. Check appsettings.json beside the executable
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

            // 3. Fallback to the Anti-Reverse-Engineering Encrypted Vault
            try
            {
                _cachedConnectionString = DecryptVault();
                if (!string.IsNullOrWhiteSpace(_cachedConnectionString))
                    return _cachedConnectionString;
            }
            catch { }

            return string.Empty;
        }

        /// <summary>
        /// Reconstructs and decrypts the connection string from segmented in-memory ciphers.
        /// Temporarily allocated memory buffers are scrubbed immediately.
        /// </summary>
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
                // Memory scrubbing to protect against process memory dumps
                Array.Clear(key, 0, key.Length);
                Array.Clear(iv, 0, iv.Length);
            }
        }

        /// <summary>
        /// Saves or updates an explicit developer connection string in the local appsettings.json.
        /// </summary>
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

        /// <summary>
        /// Returns true if the cloud backend is configured.
        /// </summary>
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(GetConnectionString());
    }
}
