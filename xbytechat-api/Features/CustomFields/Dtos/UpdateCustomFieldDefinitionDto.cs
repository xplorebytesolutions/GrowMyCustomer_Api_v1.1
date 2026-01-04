using System.Text.Json;

namespace xbytechat.api.Features.CustomFields.Dtos
{
    /// <summary>
    /// Request DTO to update a custom field definition.
    /// We keep it flexible; service decides what fields are allowed to change.
    /// </summary>
    public sealed class UpdateCustomFieldDefinitionDto
    {

        public string? Label { get; set; }
        /// <summary>
        /// Optional: allow changing datatype later if you want,
        /// but by default we typically keep datatype immutable.
        /// </summary>
        public string? DataType { get; set; }

        public JsonElement? Options { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }
}
