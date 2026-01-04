// 📄 File: xbytechat-api/Features/CRM/Controllers/ContactsController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;

namespace xbytechat.api.Features.CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // CRM should be authenticated
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IContactTagService _contactTagService;
        private readonly ILogger<ContactsController> _logger;

        public ContactsController(
            IContactService contactService,
            IContactTagService contactTagService,
            ILogger<ContactsController> logger)
        {
            _contactService = contactService;
            _contactTagService = contactTagService;
            _logger = logger;
        }

        // POST: api/contacts/create
        [HttpPost("create")]
        public async Task<IActionResult> AddContact([FromBody] ContactDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ResponseResult.ErrorInfo("❌ Invalid contact payload."));

            try
            {
                var businessId = HttpContext.User.GetBusinessId();
                var result = await _contactService.AddContactAsync(businessId, dto);

                return result.Success
                    ? Ok(result)
                    : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 Unexpected error in AddContact");
                return StatusCode(500, ResponseResult.ErrorInfo("🚨 Server error while creating contact.", ex.ToString()));
            }
        }

        // GET: api/contacts/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetContactById(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var contact = await _contactService.GetContactByIdAsync(businessId, id);

            if (contact == null)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));

            return Ok(ResponseResult.SuccessInfo("Contact loaded.", contact));
        }

        // PUT: api/contacts/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateContact(Guid id, [FromBody] ContactDto dto)
        {
            var businessId = HttpContext.User.GetBusinessId();
            dto.Id = id;

            var success = await _contactService.UpdateContactAsync(businessId, dto);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));

            return Ok(ResponseResult.SuccessInfo("Contact updated."));
        }

        // DELETE: api/contacts/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteContact(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();

            var success = await _contactService.DeleteContactAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));

            return Ok(ResponseResult.SuccessInfo("Contact deleted."));
        }

        // POST: api/contacts/parse-csv  (hidden from swagger as you did)
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("parse-csv")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseCsvToContactsAsync([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ResponseResult.ErrorInfo("CSV file is required."));

            var businessId = HttpContext.User.GetBusinessId();

            try
            {
                using var stream = file.OpenReadStream();
                var parseResult = await _contactService.ParseCsvToContactsAsync(businessId, stream);
                return Ok(ResponseResult.SuccessInfo("CSV parsed with detailed results.", parseResult));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV parsing failed.");
                return BadRequest(ResponseResult.ErrorInfo("CSV parsing failed: " + ex.Message));
            }
        }

        // PATCH: /api/contacts/{id}/favorite
        [HttpPatch("{id:guid}/favorite")]
        public async Task<IActionResult> ToggleFavorite(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();

            var success = await _contactService.ToggleFavoriteAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));

            return Ok(ResponseResult.SuccessInfo("Favorite toggled."));
        }

        // PATCH: /api/contacts/{id}/archive
        [HttpPatch("{id:guid}/archive")]
        public async Task<IActionResult> ToggleArchive(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();

            var success = await _contactService.ToggleArchiveAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));

            return Ok(ResponseResult.SuccessInfo("Archive toggled."));
        }

        // POST: api/contacts/bulk-assign-tag
        [HttpPost("bulk-assign-tag")]
        public async Task<IActionResult> AssignTagToContacts([FromBody] AssignTagToContactsDto dto)
        {
            if (dto.ContactIds == null || !dto.ContactIds.Any())
                return BadRequest(ResponseResult.ErrorInfo("No contact IDs provided."));

            var businessId = HttpContext.User.GetBusinessId();

            await _contactService.AssignTagToContactsAsync(businessId, dto.ContactIds, dto.TagId);
            return Ok(ResponseResult.SuccessInfo("Tag assigned to selected contacts."));
        }

        // DELETE: api/contacts/{contactId}/tags/{tagId}
        // Used by Inbox “remove tag” on one contact.
        [HttpDelete("{contactId:guid}/tags/{tagId:guid}")]
        public async Task<IActionResult> RemoveTagFromContact(Guid contactId, Guid tagId)
        {
            var businessId = HttpContext.User.GetBusinessId();

            var removed = await _contactTagService.RemoveTagFromContactAsync(businessId, contactId, tagId);
            if (!removed)
                return NotFound(ResponseResult.ErrorInfo("Tag link not found for this contact."));

            return Ok(ResponseResult.SuccessInfo("Tag removed from contact."));
        }

        // POST: api/contacts/bulk-unassign-tag
        // Avoid axios DELETE-body edge cases
        [HttpPost("bulk-unassign-tag")]
        public async Task<IActionResult> BulkUnassignTag([FromBody] AssignTagToContactsDto dto)
        {
            if (dto.ContactIds == null || !dto.ContactIds.Any())
                return BadRequest(ResponseResult.ErrorInfo("No contact IDs provided."));

            var businessId = HttpContext.User.GetBusinessId();

            var removedCount = 0;
            foreach (var contactId in dto.ContactIds)
            {
                var removed = await _contactTagService.RemoveTagFromContactAsync(businessId, contactId, dto.TagId);
                if (removed) removedCount++;
            }

            return Ok(ResponseResult.SuccessInfo($"Tag unassigned from {removedCount} contact(s).", new { removedCount }));
        }

        // OPTIONAL: keep DELETE too (prevents 405 surprises)
        [HttpDelete("bulk-unassign-tag")]
        public Task<IActionResult> BulkUnassignTagDelete([FromBody] AssignTagToContactsDto dto)
            => BulkUnassignTag(dto);

        // GET: api/contacts?tab=all&search=...&page=1&pageSize=25
        [HttpGet]
        public async Task<IActionResult> GetAllContacts(
            [FromQuery] string? tab = "all",
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200; // safety cap

            var businessId = HttpContext.User.GetBusinessId();

            var pagedResult = await _contactService.GetPagedContactsAsync(businessId, tab, page, pageSize, search);
            return Ok(ResponseResult.SuccessInfo("Contacts loaded.", pagedResult));
        }

        // GET: api/contacts/all  (flat list, used in dropdowns)
        [HttpGet("all")]
        public async Task<IActionResult> GetAllContactsFlat()
        {
            var businessId = HttpContext.User.GetBusinessId();
            var allContacts = await _contactService.GetAllContactsAsync(businessId);

            // Keep wrapper consistent (helps frontend)
            return Ok(ResponseResult.SuccessInfo("Contacts loaded.", allContacts));
        }

        // POST: api/contacts/filter-by-tags  (body = list of tagIds as strings)
        [HttpPost("filter-by-tags")]
        public async Task<IActionResult> FilterContactsByTags([FromBody] List<string> tags)
        {
            var businessId = HttpContext.User.GetBusinessId();

            var tagGuids = (tags ?? new List<string>())
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            var contacts = await _contactService.GetContactsByTagsAsync(businessId, tagGuids);

            return Ok(ResponseResult.SuccessInfo("Contacts filtered successfully.", contacts));
        }

        // POST: api/contacts/bulk-import
        [HttpPost("bulk-import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkImportContactsAsync( IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ResponseResult.ErrorInfo("CSV file is required."));

            var businessId = HttpContext.User.GetBusinessId();

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _contactService.BulkImportAsync(businessId, stream);
                return Ok(ResponseResult.SuccessInfo("Contacts imported successfully.", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk import failed.");
                return BadRequest(ResponseResult.ErrorInfo("Import failed: " + ex.Message));
            }
        }

        // GET: api/contacts/by-tags?tagIds=...&tagIds=...
        [HttpGet("by-tags")]
        public async Task<IActionResult> GetContactsByTagIds([FromQuery] List<Guid> tagIds)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var contacts = await _contactService.GetContactsByTagsAsync(businessId, tagIds);

            return Ok(ResponseResult.SuccessInfo("Contacts filtered successfully.", contacts));
        }
    }
}
