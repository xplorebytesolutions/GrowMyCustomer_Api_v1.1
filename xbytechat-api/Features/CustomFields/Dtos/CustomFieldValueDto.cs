using System;

namespace xbytechat.api.Features.CustomFields.Dtos
{
    /// <summary>
    /// Represents a stored value for a field for a specific entity record.
    /// Returned to the UI as raw JSON (ValueJson).
    /// </summary>
    public sealed class CustomFieldValueDto
    {
        public Guid FieldId { get; set; }

        /// <summary>
        /// Stored jsonb payload (we store as {"value": ...}).
        /// </summary>
        public string ValueJson { get; set; } = "{}";
    }
}
