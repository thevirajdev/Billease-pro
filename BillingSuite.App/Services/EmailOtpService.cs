using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BillingSuite.App.Services
{
    /// <summary>
    /// Service for sending 6-digit OTP verification emails to users during sign up.
    /// Supports SMTP email dispatch with automated local fallback notice for developer/offline testing.
    /// </summary>
    public static class EmailOtpService
    {
        public static async Task<(bool Success, string Message)> SendOtpEmailAsync(string recipientEmail, string recipientName, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
                return (false, "Recipient email address is invalid.");

            if (string.IsNullOrWhiteSpace(otpCode))
                return (false, "OTP code cannot be empty.");

            // 1. Check if SMTP settings are available via env vars, appsettings.json, or database
            string smtpHost = Environment.GetEnvironmentVariable("BILLEASE_SMTP_HOST")
                ?? GetAppSettingOrJson("SmtpHost", "Smtp:Host", "smtp.gmail.com");

            int smtpPort = 587;
            var portStr = Environment.GetEnvironmentVariable("BILLEASE_SMTP_PORT")
                ?? GetAppSettingOrJson("SmtpPort", "Smtp:Port", "587");
            int.TryParse(portStr, out smtpPort);
            if (smtpPort <= 0) smtpPort = 587;

            string smtpUser = Environment.GetEnvironmentVariable("BILLEASE_SMTP_USER")
                ?? GetAppSettingOrJson("SmtpUser", "Smtp:User", "");

            string smtpPass = Environment.GetEnvironmentVariable("BILLEASE_SMTP_PASS")
                ?? GetAppSettingOrJson("SmtpPass", "Smtp:Pass", "");

            string fromAddress = Environment.GetEnvironmentVariable("BILLEASE_SMTP_FROM")
                ?? GetAppSettingOrJson("SmtpFromAddress", "Smtp:FromAddress", "no-reply@nexaautomate.com");

            string fromName = AppSettingsService.GetString("SmtpFromName", "Billease Pro Security");

            // 2. Build Email Subject & Rich HTML Body
            string subject = $"{otpCode} is your Billease Pro verification code";
            string bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; color: #1e293b; }}
        .card {{ max-width: 500px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); border: 1px solid #e2e8f0; }}
        .brand {{ font-size: 20px; font-weight: bold; color: #0f172a; margin-bottom: 24px; display: block; }}
        .title {{ font-size: 18px; font-weight: 600; color: #1e293b; margin-bottom: 12px; }}
        .otp-box {{ background-color: #f1f5f9; border-radius: 8px; padding: 16px; text-align: center; margin: 24px 0; border: 1px solid #cbd5e1; }}
        .otp-code {{ font-size: 32px; font-weight: 800; letter-spacing: 6px; color: #2563eb; font-family: monospace; }}
        .footer {{ font-size: 12px; color: #64748b; margin-top: 24px; border-top: 1px solid #e2e8f0; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='brand'>Billease Pro</div>
        <div class='title'>Hello {(string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName)},</div>
        <p>Thank you for creating your account on Billease Pro. Use the 6-digit verification code below to complete your registration:</p>
        <div class='otp-box'>
            <div class='otp-code'>{otpCode}</div>
        </div>
        <p>This code is valid for <strong>10 minutes</strong>. If you did not request this account registration, please ignore this email.</p>
        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} NexaAutomate (thevirajrealm). All rights reserved.
        </div>
    </div>
</body>
</html>";

            // 3. Attempt SMTP Send if credentials are configured
            if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
            {
                try
                {
                    using var mail = new MailMessage();
                    mail.From = new MailAddress(fromAddress, fromName);
                    mail.To.Add(new MailAddress(recipientEmail, recipientName));
                    mail.Subject = subject;
                    mail.Body = bodyHtml;
                    mail.IsBodyHtml = true;

                    using var client = new SmtpClient(smtpHost, smtpPort)
                    {
                        Credentials = new NetworkCredential(smtpUser, smtpPass),
                        EnableSsl = true,
                        Timeout = 10000
                    };

                    await client.SendMailAsync(mail);
                    return (true, $"A 6-digit verification code has been sent to {recipientEmail}. Please check your inbox and spam folder.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SMTP email send failed: {ex.Message}");
                    return (false, $"SMTP email delivery failed: {ex.Message}");
                }
            }

            Console.WriteLine($"[EMAIL OTP DISPATCH] To: {recipientEmail} | OTP: {otpCode}");
            return (false, "Email delivery server is unconfigured. Please enter your Supabase Anon Key or SMTP credentials in appsettings.json.");
        }

        public static async Task<(bool Success, string Message)> SendPasswordResetOtpEmailAsync(string recipientEmail, string recipientName, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
                return (false, "Recipient email address is invalid.");

            if (string.IsNullOrWhiteSpace(otpCode))
                return (false, "OTP code cannot be empty.");

            string smtpHost = Environment.GetEnvironmentVariable("BILLEASE_SMTP_HOST")
                ?? GetAppSettingOrJson("SmtpHost", "Smtp:Host", "smtp.gmail.com");

            int smtpPort = 587;
            var portStr = Environment.GetEnvironmentVariable("BILLEASE_SMTP_PORT")
                ?? GetAppSettingOrJson("SmtpPort", "Smtp:Port", "587");
            int.TryParse(portStr, out smtpPort);
            if (smtpPort <= 0) smtpPort = 587;

            string smtpUser = Environment.GetEnvironmentVariable("BILLEASE_SMTP_USER")
                ?? GetAppSettingOrJson("SmtpUser", "Smtp:User", "");

            string smtpPass = Environment.GetEnvironmentVariable("BILLEASE_SMTP_PASS")
                ?? GetAppSettingOrJson("SmtpPass", "Smtp:Pass", "");

            string fromAddress = Environment.GetEnvironmentVariable("BILLEASE_SMTP_FROM")
                ?? GetAppSettingOrJson("SmtpFromAddress", "Smtp:FromAddress", "no-reply@nexaautomate.com");

            string fromName = AppSettingsService.GetString("SmtpFromName", "Billease Pro Security");

            string subject = $"{otpCode} is your Billease Pro password reset code";
            string bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; color: #1e293b; }}
        .card {{ max-width: 500px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.08); border: 1px solid #e2e8f0; }}
        .brand {{ font-size: 20px; font-weight: bold; color: #0f172a; margin-bottom: 24px; display: block; }}
        .title {{ font-size: 18px; font-weight: 600; color: #1e293b; margin-bottom: 12px; }}
        .otp-box {{ background-color: #fef2f2; border-radius: 8px; padding: 16px; text-align: center; margin: 24px 0; border: 1px solid #fecaca; }}
        .otp-code {{ font-size: 32px; font-weight: 800; letter-spacing: 6px; color: #dc2626; font-family: monospace; }}
        .footer {{ font-size: 12px; color: #64748b; margin-top: 24px; border-top: 1px solid #e2e8f0; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='brand'>Billease Pro Security</div>
        <div class='title'>Password Reset Request</div>
        <p>Hello {(string.IsNullOrWhiteSpace(recipientName) ? "User" : recipientName)},</p>
        <p>We received a request to reset your password for your Billease Pro account. Use the 6-digit verification code below to reset your password:</p>
        <div class='otp-box'>
            <div class='otp-code'>{otpCode}</div>
        </div>
        <p>This code is valid for <strong>10 minutes</strong>. If you did not request a password reset, please secure your account immediately or ignore this email.</p>
        <div class='footer'>
            &copy; {DateTime.UtcNow.Year} NexaAutomate (thevirajrealm). All rights reserved.
        </div>
    </div>
</body>
</html>";

            if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
            {
                try
                {
                    using var mail = new MailMessage();
                    mail.From = new MailAddress(fromAddress, fromName);
                    mail.To.Add(new MailAddress(recipientEmail, recipientName));
                    mail.Subject = subject;
                    mail.Body = bodyHtml;
                    mail.IsBodyHtml = true;

                    using var client = new SmtpClient(smtpHost, smtpPort)
                    {
                        Credentials = new NetworkCredential(smtpUser, smtpPass),
                        EnableSsl = true,
                        Timeout = 10000
                    };

                    await client.SendMailAsync(mail);
                    return (true, $"A 6-digit password reset code has been sent to {recipientEmail}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SMTP email send failed: {ex.Message}");
                    return (false, $"SMTP email delivery failed: {ex.Message}");
                }
            }

            Console.WriteLine($"[PASSWORD RESET OTP DISPATCH] To: {recipientEmail} | OTP: {otpCode}");
            return (false, "Email delivery server is unconfigured. Please enter your Supabase Anon Key or SMTP credentials in appsettings.json.");
        }

        private static string GetAppSettingOrJson(string dbKey, string jsonKey, string defaultValue)
        {
            try
            {
                var val = AppSettingsService.GetString(dbKey, "");
                if (!string.IsNullOrWhiteSpace(val)) return val;

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var jsonPath = System.IO.Path.Combine(baseDir, "appsettings.json");
                if (System.IO.File.Exists(jsonPath))
                {
                    var text = System.IO.File.ReadAllText(jsonPath);
                    var obj = Newtonsoft.Json.Linq.JObject.Parse(text);
                    var parts = jsonKey.Split(':');
                    Newtonsoft.Json.Linq.JToken? token = obj;
                    foreach (var part in parts)
                    {
                        if (token == null) break;
                        token = token[part];
                    }
                    if (token != null && !string.IsNullOrWhiteSpace(token.ToString()))
                        return token.ToString().Trim();
                }
            }
            catch { }
            return defaultValue;
        }
    }
}
