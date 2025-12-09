namespace xbytechat.api.Models
{
    public class WhatsAppResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RawResponse { get; set; }
    }
}
