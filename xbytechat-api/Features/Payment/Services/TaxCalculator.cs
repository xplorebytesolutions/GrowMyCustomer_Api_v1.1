#nullable enable
using System;
using System.Text.Json;

namespace xbytechat.api.Features.Payment.Services
{
    /// <summary>
    /// Central place for GST/tax math so rules can be updated without touching many files.
    /// This is intentionally simple for now.
    /// </summary>
    public static class TaxCalculator
    {
        // For now: default 18% GST (update via config/feature flags later).
        private const decimal DefaultGstRate = 0.18m;

        public static (decimal taxAmount, string? breakdownJson) CalculateGstForIndianCustomer(
            decimal taxableAmount,
            bool isInterState)
        {
            if (taxableAmount <= 0)
                return (0m, null);

            var totalGst = Math.Round(taxableAmount * DefaultGstRate, 2, MidpointRounding.AwayFromZero);

            // Simple split:
            if (isInterState)
            {
                var igst = totalGst;
                var breakdown = new
                {
                    type = "IGST",
                    rate = 18,
                    igst
                };
                return (totalGst, JsonSerializer.Serialize(breakdown));
            }
            else
            {
                var half = Math.Round(totalGst / 2m, 2, MidpointRounding.AwayFromZero);
                var breakdown = new
                {
                    type = "CGST_SGST",
                    rate = 18,
                    cgst = half,
                    sgst = half
                };
                return (totalGst, JsonSerializer.Serialize(breakdown));
            }
        }
    }
}

