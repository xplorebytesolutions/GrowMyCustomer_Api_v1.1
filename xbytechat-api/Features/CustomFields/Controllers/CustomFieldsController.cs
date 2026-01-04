using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.AccessControl.PermissionAttributes;
using xbytechat.api.Features.AccessControl.Seeder;
using xbytechat.api.Features.CustomFields.Dtos;
using xbytechat.api.Features.CustomFields.Services;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.CustomFields.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public sealed class CustomFieldsController : ControllerBase
    {
        private readonly ICustomFieldsService _service;

        public CustomFieldsController(ICustomFieldsService service)
        {
            _service = service;
        }

        private Guid GetBusinessIdOrReject(out IActionResult? errorResult)
        {
            errorResult = null;

            var businessId = HttpContext.User.GetBusinessId();
            if (businessId == Guid.Empty)
            {
                errorResult = Unauthorized(ResponseResult.ErrorInfo("Missing BusinessId in user claims."));
                return Guid.Empty;
            }

            return businessId;
        }

        // --------------------------------------------------------------------
        // Definitions (READ = allow any authenticated user in the business)
        // --------------------------------------------------------------------

        /// <summary>
        /// READ schema for the given entityType (e.g. CONTACT).
        /// ✅ MVP: Allow any authenticated user (business-scoped). This matches the current CRM module behavior.
        /// Later you can add a dedicated permission like: customfields.view
        /// </summary>
        [HttpGet("definitions")]
        public async Task<IActionResult> GetDefinitions(
            [FromQuery] string entityType = "CONTACT",
            [FromQuery] bool includeInactive = false)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            var rows = await _service.GetDefinitionsAsync(bizId, entityType, includeInactive);
            return Ok(ResponseResult.SuccessInfo("✅ Custom field definitions fetched.", rows));
        }

        /// <summary>
        /// Managing schema is an "admin-ish" action.
        /// Reusing TagsEdit for now as the closest existing CRM admin permission.
        /// (Later: introduce customfields.manage / customfields.edit)
        /// </summary>
        [HttpPost("definitions")]
        
        public async Task<IActionResult> CreateDefinition([FromBody] CreateCustomFieldDefinitionDto dto)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            var created = await _service.CreateDefinitionAsync(bizId, dto);
            return Ok(ResponseResult.SuccessInfo("✅ Custom field definition created.", created));
        }

        [HttpPut("definitions/{fieldId:guid}")]
        
        public async Task<IActionResult> UpdateDefinition([FromRoute] Guid fieldId, [FromBody] UpdateCustomFieldDefinitionDto dto)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            var updated = await _service.UpdateDefinitionAsync(bizId, fieldId, dto);
            return Ok(ResponseResult.SuccessInfo("✅ Custom field definition updated.", updated));
        }

        [HttpDelete("definitions/{fieldId:guid}")]
       
        public async Task<IActionResult> DeactivateDefinition([FromRoute] Guid fieldId)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            var ok = await _service.DeactivateDefinitionAsync(bizId, fieldId);
            return Ok(ResponseResult.SuccessInfo(ok ? "✅ Field deactivated." : "ℹ️ Field not found.", ok));
        }

        // --------------------------------------------------------------------
        // Values (READ = allow any authenticated user in the business)
        // --------------------------------------------------------------------

        /// <summary>
        /// ✅ MVP: Allow reading values for any authenticated user (business-scoped).
        /// Later you can gate with a dedicated permission like: customfields.values.view
        /// </summary>
        [HttpGet("values")]
        public async Task<IActionResult> GetValues([FromQuery] string entityType, [FromQuery] Guid entityId)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            if (entityId == Guid.Empty)
                return BadRequest(ResponseResult.ErrorInfo("EntityId is required."));

            var rows = await _service.GetValuesAsync(bizId, entityType, entityId);
            return Ok(ResponseResult.SuccessInfo("✅ Custom field values fetched.", rows));
        }

        /// <summary>
        /// Upserting values changes CRM data. Keep it strict for MVP.
        /// Reusing TagsEdit for now as "CRM admin/edit" gate.
        /// </summary>
        [HttpPut("values")]
      
        public async Task<IActionResult> UpsertValues([FromBody] UpsertCustomFieldValuesDto dto)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            await _service.UpsertValuesAsync(bizId, dto);
            return Ok(ResponseResult.SuccessInfo("✅ Custom field values saved.", true));
        }

        /// <summary>
        /// Convenience endpoint for UI: fetch active definitions + current values.
        /// ✅ MVP: Allow any authenticated user (business-scoped) so the page can load.
        /// </summary>
        [HttpGet("schema-with-values")]
        public async Task<IActionResult> GetSchemaWithValues([FromQuery] string entityType, [FromQuery] Guid entityId)
        {
            var bizId = GetBusinessIdOrReject(out var err);
            if (err != null) return err;

            if (entityId == Guid.Empty)
                return BadRequest(ResponseResult.ErrorInfo("EntityId is required."));

            var data = await _service.GetSchemaWithValuesAsync(bizId, entityType, entityId);

            return Ok(ResponseResult.SuccessInfo("✅ Schema + values fetched.", new
            {
                definitions = data.Definitions,
                values = data.Values
            }));
        }
    }
}
