using System;

namespace xbytechat.api.Features.CustomFields.Models
{
    /// <summary>
    /// Stores a field value for a specific entity record.
    /// Example:
    ///  - EntityType = "Contact"
    ///  - EntityId = Contact.Id
    ///  - FieldId = CustomFieldDefinition.Id
    ///  - ValueJson = {"value":"27ABCDE1234F1Z5"} or {"value":true} etc.
    /// </summary>
    public sealed class CustomFieldValue
    {
        public Guid Id { get; set; }

        public Guid BusinessId { get; set; }

        public string EntityType { get; set; } = "Contact";

        /// <summary>
        /// The record id (e.g., ContactId) this value belongs to.
        /// </summary>
        public Guid EntityId { get; set; }

        public Guid FieldId { get; set; }

        /// <summary>
        /// JSON payload holding the typed value.
        /// For simplicity we always store jsonb. UI/service enforces shape.
        /// Example:
        ///  {"value":"text"}
        ///  {"value":123}
        ///  {"value":true}
        ///  {"value":"2025-12-14T00:00:00Z"}
        ///  {"value":["A","B"]}
        /// </summary>
        public string ValueJson { get; set; } = "{}";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated automatically by your UpdatedAtUtcInterceptor.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation (optional but helpful)
        public CustomFieldDefinition? Field { get; set; }
    }
}
