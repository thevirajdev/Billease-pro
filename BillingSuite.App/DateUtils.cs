using System;
using System.Globalization;

namespace BillingSuite.App
{
    public static class DateUtils
    {
        /// <summary>
        /// Parses an expiry date string into a DateTime.
        /// Supports: dd/MM/yyyy, dd/MM/yy, MM/yyyy, MM/yy, MM-yyyy, MM-yy, MMyyyy, MMyy.
        /// If only month and year are provided, it defaults to the 1st of that month.
        /// </summary>
        public static DateTime? ParseExpiryDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            input = input.Trim().Replace("-", "/").Replace(".", "/").Replace(" ", "");
            
            string[] formats = {
                "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy",
                "dd/MM/yy", "d/M/yy", "dd/M/yy", "d/MM/yy",
                "MM/yyyy", "M/yyyy",
                "MM/yy", "M/yy",
                "MM-yyyy", "MM-yy",
                "MMyyyy", "MMyy"
            };

            if (DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            // Fallback for cases like "10/25" which TryParseExact might miss if expecting specific separators
            try
            {
                var parts = input.Split('/');
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int y))
                    {
                        if (y < 100) y += 2000;
                        if (m >= 1 && m <= 12)
                            return new DateTime(y, m, 1);
                    }
                }
            }
            catch { }

            return null;
        }

        public static string FormatExpiry(DateTime? dt)
        {
            return dt?.ToString("MM/yyyy") ?? "";
        }
    }
}
