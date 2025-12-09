// xbytechat-api/Controllers/HealthCheckController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace xbytechat.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthCheckController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok("✅ xByteChat backend is running 🚀");

        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            var version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0";
            return Ok(new
            {
                status = "ok",
                version,
                serverTimeUtc = DateTime.UtcNow.ToString("o")
            });
        }
    }
}
