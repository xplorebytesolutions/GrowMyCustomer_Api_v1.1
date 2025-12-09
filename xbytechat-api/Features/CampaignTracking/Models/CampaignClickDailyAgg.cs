// 📄 Features/CampaignTracking/Models/CampaignClickDailyAgg.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CampaignTracking.Worker
{
    [Table("CampaignClickDailyAgg")]
    public class CampaignClickDailyAgg
    {
        [Key] public Guid Id { get; set; }
        public Guid CampaignId { get; set; }
        public DateTime Day { get; set; } // date-only (store as date in migration)
        public int ButtonIndex { get; set; }
        public long Clicks { get; set; }
    }
}
