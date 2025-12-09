using xbytechat.api.Features.AuditTrail.Models;
using xbytechat.api.Features.AuditTrail.Services;

public static class AuditLoggingHelper
{
    private static IServiceProvider? _serviceProvider;

    public static void Configure(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static void Log(
        string actionType,
        string? entityName,
        string? entityId,
        string? description,
        IHttpContextAccessor contextAccessor)
    {
        if (_serviceProvider == null) return;

        var scope = _serviceProvider.CreateScope();
        var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var httpContext = contextAccessor.HttpContext;
        var user = httpContext?.User;
        var claims = user?.Identities?.FirstOrDefault();

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            ActionType = actionType,
            Description = description,
            BusinessId = TryParseGuid(claims?.FindFirst("businessId")?.Value),
            PerformedByUserId = TryParseGuid(claims?.FindFirst("sub")?.Value),
            PerformedByUserName = claims?.FindFirst("email")?.Value,
            RoleAtTime = claims?.FindFirst("role")?.Value,
            IPAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _ = Task.Run(() => auditLogService.SaveLogAsync(log));
    }

    private static Guid TryParseGuid(string? input) =>
        Guid.TryParse(input, out var guid) ? guid : Guid.Empty;
}
