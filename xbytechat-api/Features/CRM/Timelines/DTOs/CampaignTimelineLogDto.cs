public class CampaignTimelineLogDto
{
    public Guid ContactId { get; set; }
    public Guid BusinessId { get; set; }   // ✅ Needed for timeline insertion
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty; // ✅ Safe default to avoid null issues
    public DateTime? Timestamp { get; set; } // optional
}

