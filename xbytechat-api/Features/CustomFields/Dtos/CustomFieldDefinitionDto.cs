using System;

namespace xbytechat.api.Features.CustomFields.Dtos
{
    /// <summary>
    /// Response DTO for a Custom Field Definition (schema).
    /// </summary>
    public sealed class CustomFieldDefinitionDto
    {
        public Guid Id { get; set; }

        public string EntityType { get; set; } = "Contact";

        /// <summary>
        /// Stable internal key (snake_case recommended).
        /// Example: gst_number
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Human label shown in UI.
        /// Example: "GST Number"
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Data type as string to avoid enum-serialization surprises across clients.
        /// Example: "Text", "Number", "Date", "Boolean", "SingleSelect", "MultiSelect"
        /// </summary>
        public string DataType { get; set; } = "Text";

        /// <summary>
        /// Options metadata stored as JSON (for select types).
        /// Example: {"options":["A","B","C"]}
        /// </summary>
        public string? OptionsJson { get; set; }

        public bool IsRequired { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }
}
