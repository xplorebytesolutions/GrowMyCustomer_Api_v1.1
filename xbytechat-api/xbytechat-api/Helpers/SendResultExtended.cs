using xbytechat.api.Helpers;

public class SendResultExtended : ResponseResult
{
   // public string? MessageId { get; set; }         // WAMID from WhatsApp
    public Guid? MessageLogId { get; set; }        // Our DB log ID (from MessageLogs table)
}
