using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Models;

namespace xbytechat.api.Features.CRM.Timelines.Mappers
{
    public static class LeadTimelineMapper
    {
        public static LeadTimelineDto ToDto(Models.LeadTimeline entry)
        {
            if (entry == null) return null;

            return new LeadTimelineDto
            {
                ContactId = entry.ContactId,
                ContactName = entry.Contact?.Name,                // ✅ Enriched from navigation
                ContactNumber = entry.Contact?.PhoneNumber,       // ✅ Enriched from navigation
                EventType = entry.EventType,
                Description = entry.Description,
                Data = entry.Data,
                ReferenceId = entry.ReferenceId,
                IsSystemGenerated = entry.IsSystemGenerated,
                CreatedBy = entry.CreatedBy,
                Source = entry.Source,
                Category = entry.Category,
                // ✅ CreatedAt is intentionally excluded from DTO
            };
        }

        // Optional for create/update, include only necessary fields
        public static Models.LeadTimeline ToModel(LeadTimelineDto dto)
        {
            if (dto == null) return null;

            return new Models.LeadTimeline
            {
                ContactId = dto.ContactId,
                EventType = dto.EventType,
                Description = dto.Description,
                Data = dto.Data,
                ReferenceId = dto.ReferenceId,
                IsSystemGenerated = dto.IsSystemGenerated,
                CreatedBy = dto.CreatedBy,
                Source = dto.Source,
                Category = dto.Category,
                CreatedAt = DateTime.UtcNow // ✅ Always use UTC when creating
            };
        }
    }
}
