using System.Text.RegularExpressions;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CustomeApi.Services
{
    public static class CtaJourneyMapper
    {
        private static string Digits(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "" : Regex.Replace(s, "[^0-9]", "");

    
        public static Models.CtaJourneyEventDto Build(
            string journeyKey,          // REQUIRED -> "product_view_to_interest" (your must-match)
            Contact? contact = null,
            string? profileName = null,
            string? userId = null,      // we don’t have: pass null
            string? phoneNumberId = null,   // Meta phone_number_id
            string? businessDisplayPhone = null, // WhatsAppBusinessNumber
            string? categoryBrowsed = null,
            string? productBrowsed = null
        )
        {
            // Choose botId priority: phoneNumberId (Meta) -> business WA number -> null
            var botId = !string.IsNullOrWhiteSpace(phoneNumberId)
                ? phoneNumberId!.Trim()
                : (!string.IsNullOrWhiteSpace(businessDisplayPhone) ? Digits(businessDisplayPhone) : null);

            return new Models.CtaJourneyEventDto
            {
                userId = userId, // normally null (we don’t store)
                userName = profileName ?? contact?.ProfileName ?? contact?.Name,
                userPhone = Digits(contact?.PhoneNumber),
                botId = botId,
                categoryBrowsed = categoryBrowsed,   // keep null 
                productBrowsed = productBrowsed,     // keep null 
                CTAJourney = journeyKey               // e.g. "Button Name"
            };
        }
    }
}
