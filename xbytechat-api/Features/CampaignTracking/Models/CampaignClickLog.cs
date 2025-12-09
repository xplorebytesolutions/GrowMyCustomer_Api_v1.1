// 📄 Features/CampaignTracking/Models/CampaignClickLog.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CampaignTracking.Worker
{
    [Table("CampaignClickLogs")]
    public class CampaignClickLog
    {
        [Key] public Guid Id { get; set; }

        public Guid? RunId { get; set; }
        // FK through CampaignSendLog to CampaignId & ContactId
        public Guid CampaignSendLogId { get; set; }

        public Guid CampaignId { get; set; }      // denormalized for fast filtering
        public Guid? ContactId { get; set; }      // denormalized if available

        public int ButtonIndex { get; set; }

        [MaxLength(120)]
        public string ButtonTitle { get; set; } = "";

        // NEW: "web" | "call" | "whatsapp" (lowercase)
        [MaxLength(16)]
        public string ClickType { get; set; } = "web";

        [MaxLength(2048)]
        public string Destination { get; set; } = "";

        [MaxLength(64)]
        public string Ip { get; set; } = "";

        [MaxLength(512)]
        public string UserAgent { get; set; } = "";

        public DateTime ClickedAt { get; set; }
    }
}
