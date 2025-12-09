using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Mappers;
using xbytechat.api.Features.CRM.Timelines.Services;
using xbytechat.api.Features.CRM.Timelines.DTOs;

namespace xbytechat.api.Features.CRM.Services
{
    public class NoteService : INoteService
    {
        private readonly AppDbContext _db;
        private readonly ITimelineService _timelineService; // ✅ Injected Timeline Service

        // ✅ Constructor: Inject AppDbContext + TimelineService
        public NoteService(AppDbContext db, ITimelineService timelineService)
        {
            _db = db;
            _timelineService = timelineService;
        }

        // 📝 Add a new Note + Log into LeadTimeline
        public async Task<NoteDto> AddNoteAsync(Guid businessId, NoteDto dto)
        {
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
                        ContactId = dto.ContactId.Value,       // ➔ Which contact the note is related to
                        BusinessId = businessId,               // ➔ Which business created this
                        EventType = "NoteAdded",                // ➔ Timeline event type
                        Description = $"📝 Note added: {dto.Title ?? "(Untitled)"}", // ➔ Friendly description
                        ReferenceId = note.Id,                  // ➔ Link back to Note Id
                        CreatedBy = dto.CreatedBy,              // ➔ Who created it
                        Timestamp = DateTime.UtcNow             // ➔ When created
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
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.BusinessId == businessId);
            if (note == null) return false;

            note.Title = dto.Title;
            note.Content = dto.Content;
            note.IsPinned = dto.IsPinned;
            note.IsInternal = dto.IsInternal;
            note.EditedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc); // Always UTC timestamp

            await _db.SaveChangesAsync();
            return true;
        }

        // 🗑️ Soft delete (actually remove) a Note
        public async Task<bool> DeleteNoteAsync(Guid businessId, Guid noteId)
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.BusinessId == businessId);
            if (note == null) return false;

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
