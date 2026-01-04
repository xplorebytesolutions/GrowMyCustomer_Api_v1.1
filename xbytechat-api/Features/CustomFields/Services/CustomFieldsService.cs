using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CustomFields.Dtos;
using xbytechat.api.Features.CustomFields.Models;

namespace xbytechat.api.Features.CustomFields.Services
{
    public sealed class CustomFieldsService : ICustomFieldsService
    {
        private readonly AppDbContext _db;

        // key: snake_case recommended, enforce stable internal keys
        private static readonly Regex KeyRegex = new("^[a-z][a-z0-9_]{0,119}$", RegexOptions.Compiled);

        public CustomFieldsService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<CustomFieldDefinitionDto>> GetDefinitionsAsync(Guid businessId, string entityType, bool includeInactive)
        {
            var et = NormalizeEntityType(entityType);

            var q = _db.CustomFieldDefinitions
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.EntityType == et);

            if (!includeInactive)
                q = q.Where(x => x.IsActive);

            var rows = await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Label).ToListAsync();

            return rows.Select(MapDefinition).ToList();
        }

        public async Task<CustomFieldDefinitionDto> CreateDefinitionAsync(Guid businessId, CreateCustomFieldDefinitionDto dto)
        {
            var et = NormalizeEntityType(dto.EntityType);
            var key = NormalizeKey(dto.Key);

            ValidateDefinitionInputs(key, dto.Label);

            // DB unique index exists, but we still do a friendly pre-check for better error messages.
            var exists = await _db.CustomFieldDefinitions
                .AnyAsync(x => x.BusinessId == businessId && x.EntityType == et && x.Key == key);

            if (exists)
                throw new InvalidOperationException($"Custom field key '{key}' already exists for entity '{et}'.");

            var entity = new CustomFieldDefinition
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                EntityType = et,
                Key = key,
                Label = dto.Label.Trim(),
                DataType = ParseDataType(dto.DataType),
                OptionsJson = NormalizeOptions(dto.Options),
                IsRequired = dto.IsRequired,
                IsActive = true,
                SortOrder = dto.SortOrder
            };

            _db.CustomFieldDefinitions.Add(entity);
            await _db.SaveChangesAsync();

            return MapDefinition(entity);
        }

        public async Task<CustomFieldDefinitionDto> UpdateDefinitionAsync(Guid businessId, Guid fieldId, UpdateCustomFieldDefinitionDto dto)
        {
            var entity = await _db.CustomFieldDefinitions
                .FirstOrDefaultAsync(x => x.Id == fieldId && x.BusinessId == businessId);

            if (entity == null)
                throw new KeyNotFoundException("Custom field definition not found.");

            // Do NOT allow changing EntityType/Key in MVP (prevents breaking existing data + joins).
            if (!string.IsNullOrWhiteSpace(dto.Label))
                entity.Label = dto.Label.Trim();

            if (!string.IsNullOrWhiteSpace(dto.DataType))
                entity.DataType = ParseDataType(dto.DataType);

            if (dto.Options != null)
                entity.OptionsJson = NormalizeOptions(dto.Options);

            entity.IsRequired = dto.IsRequired;
            entity.IsActive = dto.IsActive;
            entity.SortOrder = dto.SortOrder;

            await _db.SaveChangesAsync();
            return MapDefinition(entity);
        }

        public async Task<bool> DeactivateDefinitionAsync(Guid businessId, Guid fieldId)
        {
            var entity = await _db.CustomFieldDefinitions
                .FirstOrDefaultAsync(x => x.Id == fieldId && x.BusinessId == businessId);

            if (entity == null) return false;

            entity.IsActive = false;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<CustomFieldValueDto>> GetValuesAsync(Guid businessId, string entityType, Guid entityId)
        {
            var et = NormalizeEntityType(entityType);

            var rows = await _db.CustomFieldValues
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.EntityType == et && x.EntityId == entityId)
                .ToListAsync();

            return rows.Select(x => new CustomFieldValueDto
            {
                FieldId = x.FieldId,
                ValueJson = x.ValueJson ?? "{}"
            }).ToList();
        }

        public async Task UpsertValuesAsync(Guid businessId, UpsertCustomFieldValuesDto dto)
        {
            var et = NormalizeEntityType(dto.EntityType);

            if (dto.EntityId == Guid.Empty)
                throw new ArgumentException("EntityId is required.");

            if (dto.Values == null || dto.Values.Count == 0)
                return;

            // Load definitions once, validate field ownership + types
            var fieldIds = dto.Values.Select(v => v.FieldId).Distinct().ToList();

            var defs = await _db.CustomFieldDefinitions
                .AsNoTracking()
                .Where(x => x.BusinessId == businessId && x.EntityType == et && fieldIds.Contains(x.Id) && x.IsActive)
                .ToListAsync();

            if (defs.Count != fieldIds.Count)
                throw new InvalidOperationException("One or more fields are invalid/inactive for this business/entity.");

            var defMap = defs.ToDictionary(x => x.Id, x => x);

            // Fetch existing values for this entity+fields
            var existing = await _db.CustomFieldValues
                .Where(x => x.BusinessId == businessId && x.EntityType == et && x.EntityId == dto.EntityId && fieldIds.Contains(x.FieldId))
                .ToListAsync();

            var existingMap = existing.ToDictionary(x => x.FieldId, x => x);

            foreach (var item in dto.Values)
            {
                if (item.FieldId == Guid.Empty)
                    throw new ArgumentException("FieldId is required.");

                var def = defMap[item.FieldId];
                var wrapped = WrapAndValidateValue(def, item.Value);

                if (existingMap.TryGetValue(item.FieldId, out var row))
                {
                    row.ValueJson = wrapped;
                }
                else
                {
                    _db.CustomFieldValues.Add(new CustomFieldValue
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        EntityType = et,
                        EntityId = dto.EntityId,
                        FieldId = item.FieldId,
                        ValueJson = wrapped
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task<(List<CustomFieldDefinitionDto> Definitions, List<CustomFieldValueDto> Values)> GetSchemaWithValuesAsync(
            Guid businessId, string entityType, Guid entityId)
        {
            var defs = await GetDefinitionsAsync(businessId, entityType, includeInactive: false);
            var vals = await GetValuesAsync(businessId, entityType, entityId);
            return (defs, vals);
        }

        // ---------------- helpers ----------------

        private static string NormalizeEntityType(string entityType)
        {
            var et = (entityType ?? "").Trim();
            if (string.IsNullOrWhiteSpace(et)) et = "CONTACT";
            return et.ToUpperInvariant(); // canonical storage + comparisons
        }

        private static string NormalizeKey(string key)
        {
            var k = (key ?? "").Trim().ToLowerInvariant();
            if (!KeyRegex.IsMatch(k))
                throw new ArgumentException("Key must be snake_case (a-z, 0-9, underscore), max 120 chars.");
            return k;
        }

        private static void ValidateDefinitionInputs(string key, string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label is required.");

            if (label.Trim().Length > 200)
                throw new ArgumentException("Label too long (max 200).");

            // key already validated by regex
        }

        private static CustomFieldDataType ParseDataType(string? dataType)
        {
            var raw = (dataType ?? "Text").Trim();

            if (Enum.TryParse<CustomFieldDataType>(raw, ignoreCase: true, out var dt))
                return dt;

            throw new ArgumentException($"Invalid DataType '{raw}'.");
        }

        private static string? NormalizeOptions(JsonElement? options)
        {
            if (options == null) return null;

            var kind = options.Value.ValueKind;
            if (kind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            // (Recommended) options should be object/array, not a primitive
            if (kind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new ArgumentException("Options must be a JSON object or array.");

            // JsonElement is already validated JSON. Store normalized JSON text.
            return options.Value.GetRawText();
        }

        private static string WrapAndValidateValue(CustomFieldDefinition def, JsonElement? value)
        {
            // Required check
            if (def.IsRequired && (value == null || value.Value.ValueKind == JsonValueKind.Null))
                throw new ArgumentException($"Field '{def.Label}' is required.");

            // Minimal type checks (MVP). You can extend later.
            if (value != null && value.Value.ValueKind != JsonValueKind.Null)
            {
                switch (def.DataType)
                {
                    case CustomFieldDataType.Number:
                        if (value.Value.ValueKind != JsonValueKind.Number)
                            throw new ArgumentException($"Field '{def.Label}' must be a number.");
                        break;

                    case CustomFieldDataType.Boolean:
                        if (value.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                            throw new ArgumentException($"Field '{def.Label}' must be boolean.");
                        break;

                    case CustomFieldDataType.MultiSelect:
                        if (value.Value.ValueKind != JsonValueKind.Array)
                            throw new ArgumentException($"Field '{def.Label}' must be an array.");
                        break;

                    default:
                        // Text/Date/SingleSelect/etc -> keep permissive for MVP
                        break;
                }
            }

            // Wrap into {"value": ...}
            var wrapped = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["value"] = value?.Deserialize<object?>()
            });

            return wrapped;
        }

        private static CustomFieldDefinitionDto MapDefinition(CustomFieldDefinition x)
        {
            return new CustomFieldDefinitionDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                Key = x.Key,
                Label = x.Label,
                DataType = x.DataType.ToString(),
                OptionsJson = x.OptionsJson,
                IsRequired = x.IsRequired,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            };
        }
    }
}
