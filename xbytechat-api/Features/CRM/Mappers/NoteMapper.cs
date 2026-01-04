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
                Title = note.Title ?? string.Empty,
                Content = note.Content ?? string.Empty,
                Source = note.Source ?? string.Empty,
                CreatedBy = note.CreatedBy ?? string.Empty,
                IsPinned = note.IsPinned,
                IsInternal = note.IsInternal,
                CreatedAt = note.CreatedAt,
                EditedAt = note.EditedAt
            };
        }

        public static Note MapToEntity(NoteDto dto, Guid businessId)
        {
            var content = (dto.Content ?? string.Empty).Trim();
            var title = NormalizeTitle(dto.Title, content);

            return new Note
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                ContactId = dto.ContactId,

                // ✅ Title is optional, derived from content if missing
                Title = title,

                // ✅ Content is the real payload
                Content = content,

                // ✅ Defaults to keep DB safe even if frontend sends null/empty
                Source = string.IsNullOrWhiteSpace(dto.Source) ? "Manual" : dto.Source.Trim(),
                CreatedBy = string.IsNullOrWhiteSpace(dto.CreatedBy) ? "System" : dto.CreatedBy.Trim(),

                IsPinned = dto.IsPinned,
                IsInternal = dto.IsInternal,

                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                EditedAt = null
            };
        }

        private static string NormalizeTitle(string? title, string content)
        {
            var t = (title ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(t)) return t;

            // derive title from content
            var c = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(c)) return "(Untitled)"; // service already blocks empty content

            const int max = 60;
            return c.Length <= max ? c : (c.Substring(0, max) + "…");
        }
    }
}
