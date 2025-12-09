using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;
using xbytechat.api.Features.CRM.Dtos;
using xbytechat.api.Features.CRM.Interfaces;

namespace xbytechat.api.Features.CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;
        private readonly ILogger<ContactsController> _logger;
        public ContactsController(IContactService contactService, ILogger<ContactsController> logger)
        {
            _contactService = contactService;
            _logger = logger;
        }

        // POST: api/contacts
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
                    : BadRequest(result); // Already ResponseResult.ErrorInfo
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 Unexpected error in AddContact");
                return StatusCode(500, ResponseResult.ErrorInfo("🚨 Server error while creating contact.", ex.ToString()));
            }
        }




        // GET: api/contacts/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetContactById(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var contact = await _contactService.GetContactByIdAsync(businessId, id);
            if (contact == null)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));
            return Ok(ResponseResult.SuccessInfo("Contact loaded.", contact));
        }

        // PUT: api/contacts/{id}
        [HttpPut("{id}")]
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _contactService.DeleteContactAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));
            return Ok(ResponseResult.SuccessInfo("Contact deleted."));
        }

        // POST: api/contacts/parse-csv
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("parse-csv")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseCsvToContactsAsync([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ResponseResult.ErrorInfo("CSV file is required."));

            var businessId = HttpContext.User.GetBusinessId();
            using var stream = file.OpenReadStream();

            try
            {
                var parseResult = await _contactService.ParseCsvToContactsAsync(businessId, stream);
                return Ok(ResponseResult.SuccessInfo("CSV parsed with detailed results.", parseResult));
            }
            catch (Exception ex)
            {
                return BadRequest(ResponseResult.ErrorInfo("CSV parsing failed: " + ex.Message));
            }
        }

        // PATCH: /api/contacts/{id}/favorite
        [HttpPatch("{id}/favorite")]
        public async Task<IActionResult> ToggleFavorite(Guid id)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var success = await _contactService.ToggleFavoriteAsync(businessId, id);
            if (!success)
                return NotFound(ResponseResult.ErrorInfo("Contact not found."));
            return Ok(ResponseResult.SuccessInfo("Favorite toggled."));
        }

        // PATCH: /api/contacts/{id}/archive
        [HttpPatch("{id}/archive")]
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

        //[HttpGet("contacts")]
        [HttpGet]
        public async Task<IActionResult> GetAllContacts(
        [FromQuery] string? tab = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
        {
            var businessId = HttpContext.User.GetBusinessId();
            var pagedResult = await _contactService.GetPagedContactsAsync(businessId, tab, page, pageSize);
            return Ok(ResponseResult.SuccessInfo("Contacts loaded.", pagedResult));
        }
        // GET: api/contacts/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAllContactsFlat()
        {
            var businessId = HttpContext.User.GetBusinessId();
            var allContacts = await _contactService.GetAllContactsAsync(businessId); // This returns IEnumerable<ContactDto>
            return Ok(allContacts); // Returns plain array!
        }

        [HttpPost("filter-by-tags")]
        public async Task<IActionResult> GetContactsByTags([FromBody] List<string> tags)
        {
            var businessId = HttpContext.User.GetBusinessId();

            // ✅ Convert to Guid list safely
            var tagGuids = tags
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            var contacts = await _contactService.GetContactsByTagsAsync(businessId, tagGuids);

            return Ok(ResponseResult.SuccessInfo("Contacts filtered successfully", contacts));
        }

        [HttpPost("bulk-import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BulkImportContactsAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ResponseResult.ErrorInfo("CSV file is required."));

            var businessId = HttpContext.User.GetBusinessId();

            try
            {
                var result = await _contactService.BulkImportAsync(businessId, file.OpenReadStream());
                return Ok(ResponseResult.SuccessInfo("Contacts imported successfully.", result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk import failed.");
                return BadRequest(ResponseResult.ErrorInfo("Import failed: " + ex.Message));
            }
        }

        [HttpGet("by-tags")]
        public async Task<IActionResult> GetContactsByTags([FromQuery] List<Guid> tagIds)
        {
            var businessId = User.GetBusinessId();  // Your tenant logic
            var contacts = await _contactService.GetContactsByTagsAsync(businessId, tagIds);
            return Ok(contacts);
        }


    }
}
