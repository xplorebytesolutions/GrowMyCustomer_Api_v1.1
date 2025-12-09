namespace xbytechat.api.WhatsAppSettings.DTOs
{
    /// <summary>
    /// DTO representing a simplified view of WhatsApp template metadata.
    /// </summary>
    public class TemplateMetadataDto
    {
        /// Unique name of the template.
        public string Name { get; set; } = string.Empty;

        /// Language code used when creating the template (e.g., en_US, hi_IN).
        public string Language { get; set; } = "en_US";

        /// The message body content with placeholders (e.g., "Hi {{1}}, your order is ready").
        public string Body { get; set; } = string.Empty;

        /// Number of dynamic parameters required (e.g., 2 for {{1}} and {{2}}).
        public int PlaceholderCount { get; set; }

        public List<ButtonMetadataDto> ButtonParams { get; set; } = new List<ButtonMetadataDto>(); // ✅ Added Buttons
        public bool HasImageHeader { get; set; } = true;

        public string HeaderKind { get; set; } = "none";

    }
}
