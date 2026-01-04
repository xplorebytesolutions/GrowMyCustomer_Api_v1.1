using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xbytechat.api.Features.CustomFields.Dtos;

namespace xbytechat.api.Features.CustomFields.Services
{
    public interface ICustomFieldsService
    {
        Task<List<CustomFieldDefinitionDto>> GetDefinitionsAsync(Guid businessId, string entityType, bool includeInactive);
        Task<CustomFieldDefinitionDto> CreateDefinitionAsync(Guid businessId, CreateCustomFieldDefinitionDto dto);
        Task<CustomFieldDefinitionDto> UpdateDefinitionAsync(Guid businessId, Guid fieldId, UpdateCustomFieldDefinitionDto dto);
        Task<bool> DeactivateDefinitionAsync(Guid businessId, Guid fieldId);

        Task<List<CustomFieldValueDto>> GetValuesAsync(Guid businessId, string entityType, Guid entityId);
        Task UpsertValuesAsync(Guid businessId, UpsertCustomFieldValuesDto dto);

        /// <summary>
        /// Convenience endpoint for UI: returns schema + current values for a record.
        /// </summary>
        Task<(List<CustomFieldDefinitionDto> Definitions, List<CustomFieldValueDto> Values)> GetSchemaWithValuesAsync(
            Guid businessId, string entityType, Guid entityId);
    }
}
