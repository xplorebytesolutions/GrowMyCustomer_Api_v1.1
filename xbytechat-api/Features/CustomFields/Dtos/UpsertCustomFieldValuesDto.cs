using System;
using System.Collections.Generic;
using System.Text.Json;

namespace xbytechat.api.Features.CustomFields.Dtos
{
    /// <summary>
    /// Bulk upsert values for one record (e.g., one Contact).
    /// </summary>
    public sealed class UpsertCustomFieldValuesDto
    {
        public string EntityType { get; set; } = "CONTACT";

        /// <summary>
        /// Record id (e.g., ContactId).
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// Values to upsert.
        /// Service will wrap each into {"value": <this>} for storage.
        /// </summary>
        public List<UpsertCustomFieldValueItemDto> Values { get; set; } = new();
    }

    public sealed class UpsertCustomFieldValueItemDto
    {
        public Guid FieldId { get; set; }

        /// <summary>
        /// Typed value as JSON.
        /// Examples: "abc", 123, true, "2025-12-14T00:00:00Z", ["A","B"]
        /// </summary>
        public JsonElement? Value { get; set; }
    }
}
