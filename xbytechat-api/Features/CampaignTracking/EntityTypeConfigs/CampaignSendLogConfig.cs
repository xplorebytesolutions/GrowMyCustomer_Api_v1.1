using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using xbytechat.api.Features.CampaignTracking.Models;

namespace xbytechat.api.Features.CampaignTracking.EntityTypeConfigs
{
    public class CampaignSendLogConfig : IEntityTypeConfiguration<CampaignSendLog>
    {
        public void Configure(EntityTypeBuilder<CampaignSendLog> e)
        {
            e.ToTable("CampaignSendLogs"); // your table name
            e.HasKey(x => x.Id);

            // common lengths to keep COPY happy (adjust if you already have constraints)
            e.Property(x => x.MessageId).HasMaxLength(128);
            e.Property(x => x.TemplateId).HasMaxLength(128);
            e.Property(x => x.SendStatus).HasMaxLength(32);
            e.Property(x => x.ErrorMessage).HasMaxLength(1024);
            e.Property(x => x.CreatedBy).HasMaxLength(128);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.DeviceInfo).HasMaxLength(256);
            e.Property(x => x.MacAddress).HasMaxLength(64);
            e.Property(x => x.SourceChannel).HasMaxLength(64);
            e.Property(x => x.DeviceType).HasMaxLength(64);
            e.Property(x => x.Browser).HasMaxLength(64);
            e.Property(x => x.Country).HasMaxLength(64);
            e.Property(x => x.City).HasMaxLength(64);
            e.Property(x => x.ClickType).HasMaxLength(64);
            e.Property(x => x.LastRetryStatus).HasMaxLength(32);

            // CreatedAt default (UTC) if not set by code
            e.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

            // helpful indexes
            e.HasIndex(x => new { x.BusinessId, x.CampaignId, x.CreatedAt });
            e.HasIndex(x => new { x.CampaignId, x.SendStatus, x.CreatedAt });
            e.HasIndex(x => new { x.RecipientId, x.CreatedAt });
            e.HasIndex(x => x.MessageId);
            e.HasIndex(x => x.RunId);
        }
    }
}
