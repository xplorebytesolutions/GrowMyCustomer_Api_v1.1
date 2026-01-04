// 📄 xbytechat-api/Features/CRM/Controllers/NotesController.cs
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotesController : ControllerBase
    {
        private readonly INoteService _noteService;

        public NotesController(INoteService noteService)
        {
            _noteService = noteService;
        }

        // ✅ Clean, REST-y POST: /api/notes
        [HttpPost]
        public async Task<IActionResult> AddNote([FromBody] NoteDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest(ResponseResult.ErrorInfo("Note payload is missing."));
                }

                var businessId = HttpContext.User.GetBusinessId();
                var result = await _noteService.AddNoteAsync(businessId, dto);
                return Ok(ResponseResult.SuccessInfo("Note created.", result));
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    ResponseResult.ErrorInfo("Error creating note", ex.Message)
                );
            }
        }

        // GET /api/notes/contact/{contactId}
        [HttpGet("contact/{contactId}")]
        public async Task<IActionResult> GetNotesByContact(Guid contactId)
        {
            try
            {
                var businessId = HttpContext.User.GetBusinessId();
                var result = await _noteService.GetNotesByContactAsync(businessId, contactId);
                return Ok(ResponseResult.SuccessInfo("Notes loaded.", result));
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    ResponseResult.ErrorInfo("Error fetching notes", ex.Message)
                );
            }
        }

        // GET /api/notes/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetNoteById(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var result = await _noteService.GetNoteByIdAsync(businessId, id);

            if (result == null)
            {
                return NotFound(ResponseResult.ErrorInfo("Note not found."));
            }

            return Ok(ResponseResult.SuccessInfo("Note loaded.", result));
        }

        // PUT /api/notes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(Guid id, [FromBody] NoteDto dto)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _noteService.UpdateNoteAsync(businessId, id, dto);

            if (!success)
            {
                return NotFound(ResponseResult.ErrorInfo("Note not found."));
            }

            return Ok(ResponseResult.SuccessInfo("Note updated."));
        }

        // DELETE /api/notes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _noteService.DeleteNoteAsync(businessId, id);

            if (!success)
            {
                return NotFound(ResponseResult.ErrorInfo("Note not found."));
            }

            return Ok(ResponseResult.SuccessInfo("Note deleted."));
        }
    }
}
