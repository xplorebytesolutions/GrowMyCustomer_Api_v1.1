using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using xbytechat.api;

[ApiController]
[Route("api/getflow")]
public class DevCustomerWebhookConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    public DevCustomerWebhookConfigController(AppDbContext db) => _db = db;

    [HttpGet("{businessId:guid}")]
    public async Task<IActionResult> Get(Guid businessId)
    {
        var cfg = await _db.CustomerWebhookConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsActive);
        return Ok(cfg is null ? new { found = false } : new { found = true, url = cfg.Url });
    }
}
