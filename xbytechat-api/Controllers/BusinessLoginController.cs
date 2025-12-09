using Microsoft.AspNetCore.Mvc;
using xbytechat.api.DTOs;
using xbytechat.api.DTOs.Tenants;
using xbytechat.api.Services.Interfaces;

namespace xbytechat.api.Controllers
{
    [ApiController]
    [Route("api/tenants")]
    public class BusinessLoginController : ControllerBase
    {
        private readonly IBusinessService _tenantService;

        public BusinessLoginController(IBusinessService tenantService)
        {
            _tenantService = tenantService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] BusinessLoginRequest request)
        {
            try
            {
                var tenant = await _tenantService.LoginAsync(request);

                return Ok(new
                {
                    tenant.Id,
                    tenant.Email,
                    tenant.CompanyName,
                    //tenant.Role,
                    //tenant.Plan,
                    Message = "Login successful"
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
        }
    }
}
