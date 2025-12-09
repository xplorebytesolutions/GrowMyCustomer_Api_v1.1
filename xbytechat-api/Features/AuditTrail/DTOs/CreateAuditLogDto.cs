namespace xbytechat.api.Features.AuditTrail.DTOs;

public class CreateAuditLogDto
{
    public string ActionType { get; set; }
    public string Module { get; set; }
    public string? RecordId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Description { get; set; }

    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; }
}
