#if DEBUG
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

[ApiController]
[Route("api/dev-seed")]
[ApiExplorerSettings(IgnoreApi = true)]
public class DevSeedController : ControllerBase
{
    private readonly UserManager<IdentityUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IWebHostEnvironment _env;

    public DevSeedController(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        IWebHostEnvironment env)
    {
        _users = users;
        _roles = roles;
        _env = env;
    }

    [AllowAnonymous]
    [HttpPost("e2e-user")]
    public async Task<IActionResult> SeedE2E([FromBody] SeedReq req)
    {
        // Only allow in Development
        if (!_env.IsDevelopment())
            return Forbid();

        var email = (req.Email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "email/password required" });

        // Idempotent create/update
        var user = await _users.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var create = await _users.CreateAsync(user, req.Password);
            if (!create.Succeeded)
                return BadRequest(new { message = "create failed", errors = create.Errors.Select(e => e.Description) });
        }

        // Ensure role (use "superadmin" or a minimal CRM role your app understands)
        var role = string.IsNullOrWhiteSpace(req.Role) ? "superadmin" : req.Role.Trim();
        if (!await _roles.RoleExistsAsync(role))
            await _roles.CreateAsync(new IdentityRole(role));
        if (!await _users.IsInRoleAsync(user, role))
            await _users.AddToRoleAsync(user, role);

        return Ok(new { ok = true, email, role });
    }

    public record SeedReq(string Email, string Password, string Role = "superadmin");
}
#endif
