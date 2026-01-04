using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Mappers;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Services;

namespace xbytechat.api.Features.CRM.Services
{
    public class NoteService : INoteService
    {
        private readonly AppDbContext _db;
        private readonly ITimelineService _timelineService;

        public NoteService(AppDbContext db, ITimelineService timelineService)
        {
            _db = db;
            _timelineService = timelineService;
        }

        // 📝 Add a new Note + Log into LeadTimeline
        public async Task<NoteDto> AddNoteAsync(Guid businessId, NoteDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            // ✅ Content is the real important field
            dto.Content = (dto.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dto.Content))
                throw new ArgumentException("Note content is required.");

            // ✅ Title is optional → derive from Content if missing
            dto.Title = NormalizeTitle(dto.Title, dto.Content);

            // 1️⃣ Map incoming DTO to Note entity
            var note = NoteMapper.MapToEntity(dto, businessId);

            // 2️⃣ Save the Note into database
            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            // 3️⃣ Log this Note creation into LeadTimeline (only if ContactId is present)
            if (dto.ContactId.HasValue)
            {
                try
                {
                    await _timelineService.LogNoteAddedAsync(new CRMTimelineLogDto
                    {
                        ContactId = dto.ContactId.Value,
                        BusinessId = businessId,
                        EventType = "NoteAdded",
                        Description = $"📝 Note added: {dto.Title ?? "(Untitled)"}",
                        ReferenceId = note.Id,
                        CreatedBy = dto.CreatedBy, // ⚠️ ideally override from claims in controller
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // 🛡 Timeline saving failure should not break note creation
                    Console.WriteLine($"⚠️ Timeline log failed for NoteId {note.Id}: {ex.Message}");
                }
            }

            // 4️⃣ Return the saved note as DTO
            return NoteMapper.MapToDto(note);
        }

        // 📋 List all Notes by Contact
        public async Task<IEnumerable<NoteDto>> GetNotesByContactAsync(Guid businessId, Guid contactId)
        {
            return await _db.Notes
                .AsNoTracking()
                .Where(n => n.BusinessId == businessId && n.ContactId == contactId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => NoteMapper.MapToDto(n))
                .ToListAsync();
        }

        // 📋 Get a single Note by Id
        public async Task<NoteDto?> GetNoteByIdAsync(Guid businessId, Guid noteId)
        {
            var note = await _db.Notes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == noteId && n.BusinessId == businessId);

            return note == null ? null : NoteMapper.MapToDto(note);
        }

        // ✏️ Update an existing Note
        public async Task<bool> UpdateNoteAsync(Guid businessId, Guid noteId, NoteDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.BusinessId == businessId);
            if (note == null) return false;

            // ✅ Content is required
            dto.Content = (dto.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dto.Content))
                throw new ArgumentException("Note content is required.");

            // ✅ Title optional: only update if provided, else derive from Content
            var normalizedTitle = NormalizeTitle(dto.Title, dto.Content);
            note.Title = normalizedTitle;

            note.Content = dto.Content;
            note.IsPinned = dto.IsPinned;
            note.IsInternal = dto.IsInternal;
            note.EditedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

            await _db.SaveChangesAsync();
            return true;
        }

        // 🗑️ HARD delete (actual remove) a Note
        public async Task<bool> DeleteNoteAsync(Guid businessId, Guid noteId)
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.BusinessId == businessId);
            if (note == null) return false;

            _db.Notes.Remove(note); // ✅ Hard delete
            await _db.SaveChangesAsync();
            return true;
        }

        // ----------------- helpers -----------------

        private static string? NormalizeTitle(string? title, string content)
        {
            var t = (title ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(t))
                return t;

            // derive from content
            var c = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(c))
                return null;

            const int max = 60;
            return c.Length <= max ? c : (c.Substring(0, max) + "…");
        }
    }
}
