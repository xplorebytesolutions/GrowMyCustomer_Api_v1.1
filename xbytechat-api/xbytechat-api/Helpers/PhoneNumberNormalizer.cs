// 📄 xbytechat-api/Helpers/PhoneNumberNormalizer.cs
using System;
using System.Linq;
using PhoneNumbers;

namespace xbytechat.api.Helpers
{
    /// <summary>
    /// Canonical phone format for xByteChat:
    /// ✅ E.164 digits-only (NO '+')
    /// Example: "+91 98765 43210" => "919876543210"
    /// </summary>
    public static class PhoneNumberNormalizer
    {
        private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

        /// <summary>
        /// Normalize any input into E.164 digits-only (no '+').
        /// Returns null if invalid/unparseable.
        ///
        /// Rules:
        /// - Accepts "+E.164" and "00" prefix (converted to '+')
        /// - If input is local digits without '+', parses using defaultRegion
        /// - Output is ALWAYS digits-only E.164 (country code included)
        /// - If parsing fails:
        ///    - If defaultRegion is IN and digits length == 10, assume IN (prepend "91")
        ///    - Otherwise return null (do NOT guess) to avoid identity corruption
        /// </summary>
        public static string? NormalizeToE164Digits(string? input, string defaultRegion = "IN")
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // Keep digits and leading '+'
            var cleaned = new string(input.Trim().Where(ch => char.IsDigit(ch) || ch == '+').ToArray());

            // Common "00" international prefix => "+"
            if (cleaned.StartsWith("00", StringComparison.Ordinal))
                cleaned = "+" + cleaned.Substring(2);

            try
            {
                // If it starts with '+', parse as international; else parse using defaultRegion.
                var parsed = Util.Parse(cleaned, cleaned.StartsWith("+", StringComparison.Ordinal) ? null : defaultRegion);

                if (!Util.IsValidNumber(parsed))
                    return null;

                var e164 = Util.Format(parsed, PhoneNumberFormat.E164); // "+919876..."
                return e164.TrimStart('+'); // "919876..."
            }
            catch
            {
                // Fallback: digits-only sanity check (E.164 max 15 digits)
                var digits = new string(cleaned.Where(char.IsDigit).ToArray());
                if (digits.Length < 7 || digits.Length > 15) return null;

                // If defaultRegion is IN and someone entered 10 digits, assume IN
                if (defaultRegion.Equals("IN", StringComparison.OrdinalIgnoreCase) && digits.Length == 10)
                    return "91" + digits;

                // Otherwise, DO NOT guess. Force user/business to provide a parseable number.
                // This avoids silently storing ambiguous identities (e.g., "9876543210").
                return null;
            }
        }
    }
}

