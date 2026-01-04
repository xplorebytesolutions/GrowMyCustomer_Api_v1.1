using System.Text.Json;

namespace xbytechat.api.Features.CustomFields.Dtos
{
    /// <summary>
    /// Request DTO to create a custom field definition.
    /// </summary>
    public sealed class CreateCustomFieldDefinitionDto
    {
        public string EntityType { get; set; } = "Contact";

        /// <summary>
        /// Stable internal key (snake_case recommended).
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// UI label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Data type as string:
        /// "Text", "Number", "Date", "Boolean", "SingleSelect", "MultiSelect"
        /// </summary>
        public string DataType { get; set; } = "Text";

        /// <summary>
        /// For select types: options and any future metadata.
        /// Example: {"options":["Retail","Wholesale"]}
        /// </summary>
        public JsonElement? Options { get; set; }

        public bool IsRequired { get; set; } = false;

        public int SortOrder { get; set; } = 0;
    }
}
