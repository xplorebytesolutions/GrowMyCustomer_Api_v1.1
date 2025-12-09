using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.CRM.Mappers
{
    public static class NoteMapper
    {
        public static NoteDto MapToDto(Note note)
        {
            return new NoteDto
            {
                Id = note.Id,
                ContactId = note.ContactId,
                Title = note.Title,
                Content = note.Content,
                Source = note.Source,
                CreatedBy = note.CreatedBy,
                IsPinned = note.IsPinned,
                IsInternal = note.IsInternal,
                CreatedAt = note.CreatedAt,
                EditedAt = note.EditedAt
            };
        }

        public static Note MapToEntity(NoteDto dto, Guid businessId)
        {
            return new Note
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ContactId = dto.ContactId,
                Title = dto.Title,
                Content = dto.Content,
                Source = dto.Source,
                CreatedBy = dto.CreatedBy,
                IsPinned = dto.IsPinned,
                IsInternal = dto.IsInternal,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

