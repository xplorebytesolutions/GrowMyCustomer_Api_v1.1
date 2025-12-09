using System;

namespace xbytechat.api.Features.AccountInsights.DTOs
{
    public class AccountInsightsActionDto
    {
        public long Id { get; set; }
        public Guid BusinessId { get; set; }

        public string Type { get; set; }
        public string Label { get; set; }
        public string Actor { get; set; }

        public string MetaJson { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
