using xbytechat.api.Features.CRM.Dtos;

namespace xbytechat.api.Features.CRM.Interfaces
{
    public interface INoteService
    {
        // For creating new note
        Task<NoteDto> AddNoteAsync(Guid businessId, NoteDto dto);

        // List all notes for dashboard view
        Task<IEnumerable<NoteDto>> GetNotesByContactAsync(Guid businessId, Guid contactId);

        // For loading note in edit mode
        Task<NoteDto?> GetNoteByIdAsync(Guid businessId, Guid noteId);
        // Handles editing
        Task<bool> UpdateNoteAsync(Guid businessId, Guid noteId, NoteDto dto);
        // Soft delete → IsActive = false
        Task<bool> DeleteNoteAsync(Guid businessId, Guid noteId);
    }
}

