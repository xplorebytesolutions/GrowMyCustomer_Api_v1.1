using System.Text.Json.Serialization;

namespace xbytechat.api.Features.CustomeApi.Models
{
    public sealed class CtaJourneyEventDto
    {
        // User’s expected fields (nulls allowed when we don’t have them)
        public string? userId { get; set; }            // we don’t have this → null
        public string? userName { get; set; }          // our Contact.ProfileName or Contact.Name
        public string? userPhone { get; set; }         // digits only
        public string? botId { get; set; }             // your WA PhoneNumberId or BusinessNumber (see 2.4)
        public string? categoryBrowsed { get; set; }   // optional, keep null
        public string? productBrowsed { get; set; }    // optional, keep null

        // REQUIRED by partner: this is the key we must match
        //public string CTAJourney { get; set; } = string.Empty;
        [JsonPropertyName("CTAJourney")]
        public string? CTAJourney { get; set; }
    }
}
