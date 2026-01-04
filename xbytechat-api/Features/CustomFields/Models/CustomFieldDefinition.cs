using System;

namespace xbytechat.api.Features.CustomFields.Models
{
    /// <summary>
    /// Defines a custom field (schema) for a Business and an EntityType (e.g., Contact).
    /// Example:
    ///  - EntityType = "Contact"
    ///  - Key = "gst_number"
    ///  - Label = "GST Number"
    ///  - DataType = Text
    /// </summary>
    public sealed class CustomFieldDefinition
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }

        /// <summary>
        /// Target entity type. Keep it flexible as string so we can reuse this module later:
        /// "Contact", "Conversation", "MessageLog", etc.
        /// </summary>
        public string EntityType { get; set; } = "Contact";

        /// <summary>
        /// Stable key used internally (snake_case recommended).
        /// Example: gst_number, preferred_language
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Human-friendly label shown in UI.
        /// Example: "GST Number"
        /// </summary>
        public string Label { get; set; } = string.Empty;

        public CustomFieldDataType DataType { get; set; } = CustomFieldDataType.Text;

        /// <summary>
        /// For select types: store options, UI metadata, validation rules etc.
        /// Stored as jsonb.
        /// Example: {"options":["A","B","C"]}
        /// </summary>
        public string? OptionsJson { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated automatically by your UpdatedAtUtcInterceptor.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
