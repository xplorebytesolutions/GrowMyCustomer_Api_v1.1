using Microsoft.EntityFrameworkCore;
using System.Globalization;
using xbytechat.api.Features.Catalog.Models;
using xbytechat.api.Models.BusinessModel;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CampaignTracking.Models;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.AccessControl.Seeder;
using xbytechat.api.Features.AuditTrail.Models;
using xbytechat_api.WhatsAppSettings.Models;
using xbytechat.api.Features.CTAManagement.Models;
using xbytechat.api.Features.Tracking.Models;
using xbytechat.api.Features.MessageManagement.DTOs;
using xbytechat.api.Features.Webhooks.Models;
using xbytechat.api.Features.CTAFlowBuilder.Models;
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Features.AutoReplyBuilder.Models;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Models;
using xbytechat.api.Features.BusinessModule.Models;

using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Features.Automation.Models;
using xbytechat.api.Features.CampaignTracking.Worker;
using xbytechat.api.Features.WhatsAppSettings.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using xbytechat_api.Features.Billing.Models;
using xbytechat.api.Features.CustomeApi.Models;
using xbytechat.api.Features.CampaignTracking.EntityTypeConfigs;
using xbytechat.api.Features.TemplateModule.Models;
using xbytechat.api.Features.ESU.Shared;
using xbytechat.api.Features.ESU.Facebook.Models;
using xbytechat.api.Features.AccountInsights.Models;
using xbytechat.api.Features.Payment.Models;
using xbytechat.api.Features.Entitlements.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.CRM.Timelines.Models;


namespace xbytechat.api
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Business> Businesses { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<MessageLog> MessageLogs { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CatalogClickLog> CatalogClickLogs { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<LeadTimeline> LeadTimelines { get; set; }
        public DbSet<ContactTag> ContactTags { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<CampaignRecipient> CampaignRecipients { get; set; }
        public DbSet<CampaignSendLog> CampaignSendLogs { get; set; }
        public DbSet<MessageStatusLog> MessageStatusLogs { get; set; }

        // 🧩 Access Control
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<WhatsAppSettingEntity> WhatsAppSettings { get; set; }
        public DbSet<BusinessPlanInfo> BusinessPlanInfos { get; set; }
        public DbSet<TrackingLog> TrackingLogs { get; set; }
        public DbSet<CTADefinition> CTADefinitions { get; set; }
        public DbSet<CampaignButton> CampaignButtons { get; set; }
        public DbSet<FailedWebhookLog> FailedWebhookLogs { get; set; }
        public DbSet<WebhookSettings> WebhookSettings { get; set; }
        public DbSet<CTAFlowConfig> CTAFlowConfigs { get; set; }
        public DbSet<CTAFlowStep> CTAFlowSteps { get; set; }
        public DbSet<FlowButtonLink> FlowButtonLinks { get; set; }

        public DbSet<CampaignFlowOverride> CampaignFlowOverrides { get; set; }
        public DbSet<FlowExecutionLog> FlowExecutionLogs { get; set; }
        public DbSet<ContactRead> ContactReads { get; set; }
       
        public DbSet<AutoReplyFlow> AutoReplyFlows { get; set; } = null!;
        public DbSet<AutoReplyFlowNode> AutoReplyNodes { get; set; } = null!;
        // Back-compat alias until older code is removed
        public DbSet<AutoReplyFlowNode> AutoReplyFlowNodes { get; set; } = null!;
        public DbSet<AutoReplyFlowEdge> AutoReplyFlowEdges { get; set; }
        public DbSet<AutoReplyLog> AutoReplyLogs { get; set; }
        public DbSet<ChatSessionState> ChatSessionStates { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<PlanPermission> PlanPermissions { get; set; }

        public DbSet<PlanFeatureMatrix> PlanFeatureMatrix { get; set; }


        public DbSet<AutomationFlow> AutomationFlows { get; set; }
        public DbSet<WhatsAppTemplate> WhatsAppTemplates { get; set; }
        public DbSet<CampaignClickLog> CampaignClickLogs => Set<CampaignClickLog>();
        public DbSet<CampaignClickDailyAgg> CampaignClickDailyAgg => Set<CampaignClickDailyAgg>();
        public DbSet<QuickReply> QuickReplies { get; set; } = null!;
        public DbSet<WhatsAppPhoneNumber> WhatsAppPhoneNumbers { get; set; }
        public DbSet<Audience> Audiences { get; set; }
        public DbSet<AudienceMember> AudienceMembers { get; set; }
        public DbSet<CsvBatch> CsvBatches { get; set; }
        public DbSet<CsvRow> CsvRows { get; set; }
        public DbSet<CampaignVariableMap> CampaignVariableMaps { get; set; }
        public DbSet<ProviderBillingEvent> ProviderBillingEvents { get; set; } = default!;
        public DbSet<OutboundCampaignJob> OutboundCampaignJobs { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; } = null!;
        public DbSet<CustomerWebhookConfig> CustomerWebhookConfigs { get; set; }
        public DbSet<ContactJourneyState> ContactJourneyStates { get; set; }
        public DbSet<OutboundMessageJob> OutboundMessageJobs { get; set; }

        // Template Creation
        public DbSet<TemplateDraft> TemplateDrafts { get; set; } = default!;
        public DbSet<TemplateDraftVariant> TemplateDraftVariants { get; set; } = default!;
        public DbSet<TemplateLibraryItem> TemplateLibraryItems { get; set; } = default!;
        public DbSet<TemplateLibraryVariant> TemplateLibraryVariants { get; set; } = default!;

        // ESU
        public DbSet<IntegrationFlags> IntegrationFlags { get; set; } = null!;
        public DbSet<EsuToken> EsuTokens { get; set; }

        public DbSet<AccountInsightsAction> AccountInsightsActions { get; set; }



        // Payment module
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }
        public DbSet<Coupon> Coupons { get; set; }

        // Quota
        public DbSet<PlanQuota> PlanQuotas => Set<PlanQuota>();
        public DbSet<BusinessQuotaOverride> BusinessQuotaOverrides => Set<BusinessQuotaOverride>();
        public DbSet<BusinessUsageCounter> BusinessUsageCounters => Set<BusinessUsageCounter>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ────────────────────── DETERMINISTIC SEED TIMESTAMPS ──────────────────────
            var seedCreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var planCreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var created = new DateTime(2025, 9, 13, 0, 0, 0, DateTimeKind.Utc);

            // ────────────────────── SEEDS (unchanged GUIDs) ──────────────────────
            var superadminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var partnerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var resellerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
            var businessRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");
            var agentRoleId = Guid.Parse("00000000-0000-0000-0000-000000000005");

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = superadminRoleId, Name = "admin", Description = "Super Admin", CreatedAt = seedCreatedAt },
                new Role { Id = partnerRoleId, Name = "partner", Description = "Business Partner", CreatedAt = seedCreatedAt },
                new Role { Id = resellerRoleId, Name = "reseller", Description = "Reseller Partner", CreatedAt = seedCreatedAt },
                new Role { Id = businessRoleId, Name = "business", Description = "Business Owner", CreatedAt = seedCreatedAt },
                new Role { Id = agentRoleId, Name = "staff", Description = "Staff", CreatedAt = seedCreatedAt }
            );

            var superAdminUserId = Guid.Parse("62858aa2-3a54-4fd5-8696-c343d9af7634");
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = superAdminUserId,
                Name = "Super Admin",
                Email = "admin@xbytechat.com",
                RoleId = superadminRoleId,
                Status = "active",
                CreatedAt = seedCreatedAt,
                DeletedAt = null,
                IsDeleted = false,
                BusinessId = null,
                PasswordHash = "JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=",
                RefreshToken = null,
                RefreshTokenExpiry = null
            });

            var basicPlanId = Guid.Parse("5f9f5de1-a0b2-48ba-b03d-77b27345613f");
            
            modelBuilder.Entity<Plan>().HasData(new Plan
            {
                Id = basicPlanId,
                Code = "SYSTEM_DEFAULT",
                Name = "System Default",
                Description = "Default free plan",
                IsActive = true,
                IsInternal = true,
                CreatedAt = planCreatedAt
            });

            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000000"), Code = "dashboard.view", Name = "dashboard.view", Group = "Dashboard", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), Code = "campaign.view", Name = "campaign.view", Group = "Campaign", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000002"), Code = "campaign.create", Name = "campaign.create", Group = "Campaign", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000003"), Code = "campaign.delete", Name = "campaign.delete", Group = "Campaign", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000004"), Code = "product.view", Name = "product.view", Group = "Catalog", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000005"), Code = "product.create", Name = "product.create", Group = "Catalog", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000006"), Code = "product.delete", Name = "product.delete", Group = "Catalog", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000007"), Code = "contacts.view", Name = "contacts.view", Group = "CRM", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000008"), Code = "tags.edit", Name = "tags.edit", Group = null, IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000009"), Code = "admin.business.approve", Name = "admin.business.approve", Group = "Admin", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000010"), Code = "admin.logs.view", Name = "admin.logs.view", Group = null, IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000011"), Code = "admin.plans.view", Name = "admin.plans.view", Group = "Admin", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000012"), Code = "admin.plans.create", Name = "admin.plans.create", Group = "Admin", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000013"), Code = "admin.plans.update", Name = "admin.plans.update", Group = "Admin", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("30000000-0000-0000-0000-000000000014"), Code = "admin.plans.delete", Name = "admin.plans.delete", Group = "Admin", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("74c8034f-d9cb-4a17-8578-a9f765bd845c"), Code = "messaging.report.view", Name = "messaging.report.view", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("c819f1bd-422d-4609-916c-cc185fe44ab0"), Code = "messaging.status.view", Name = "messaging.status.view", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("eecd0fac-223c-4dba-9fa1-2a6e973d61d1"), Code = "messaging.inbox.view", Name = "messaging.inbox.view", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("974af1f9-3caa-4857-a1a7-48462c389332"), Code = "messaging.send.text", Name = "messaging.send.text", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("0485154c-dde5-4732-a7aa-a379c77a5b27"), Code = "messaging.send.template", Name = "messaging.send.template", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("29461562-ef9c-48c0-a606-482ff57b8f95"), Code = "messaging.send", Name = "messaging.send", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("bbc5202a-eac9-40bb-aa78-176c677dbf5b"), Code = "messaging.whatsappsettings.view", Name = "messaging.whatsappsettings.view", Group = "Messaging", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("205b87c7-b008-4e51-9fea-798c2dc4f9c2"), Code = "admin.whatsappsettings.view", Name = "admin.whatsappsettings.view", Group = "Admin", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("6e4d3a86-7cf9-4ac2-b8a7-ed10c9f0173d"), Code = "settings.whatsapp.view", Name = "Settings - WhatsApp View", Group = "Settings", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("ad36cdb7-5221-448b-a6a6-c35c9f88d021"), Code = "inbox.view", Name = "inbox.view", Group = "Inbox", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("74828fc0-e358-4cfc-b924-13719a0d9f50"), Code = "inbox.menu", Name = "inbox.menu", Group = "Inbox", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("98572fe7-d142-475a-b990-f248641809e2"), Code = "settings.profile.view", Name = "settings.profile.view", Group = "Settings", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("821480c6-1464-415e-bba8-066fcb4e7e63"), Code = "automation.menu", Name = "automation.menu", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("918a61d0-5ab6-46af-a3d3-41e37b7710f9"), Code = "automation.Create.Template.Flow", Name = "automation.Create.Template.Flow", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("9ae90cfe-3fea-4307-b024-3083c2728148"), Code = "automation.View.Template.Flow", Name = "automation.View.Template.Flow", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("93c5d5a7-f8dd-460a-8c7b-e3788440ba3a"), Code = "automation.Create.TemplatePlusFreetext.Flow", Name = "automation.Create.TemplatePlusFreetext.Flow", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("7d7cbceb-4ce7-4835-85cd-59562487298d"), Code = "automation.View.TemplatePlusFreetext.Flow", Name = "automation.View.TemplatePlusFreetext.Flow", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("636b17f2-1c54-4e26-a8cd-dbf561dcb522"), Code = "automation.View.Template.Flow_analytics", Name = "automation.View.Template.Flow_analytics", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("adfa8490-9705-4a36-a86e-d5bff7ddc220"), Code = "automation.View.TemplatePlusFreeText.Flow_analytics", Name = "automation.View.TemplatePlusFreeText.Flow_analytics", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("0dedac5b-81c8-44c3-8cfe-76c58e29c6db"), Code = "automation_trigger_test", Name = "automation_trigger_test", Group = "Automation", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("14ef7f9d-0975-4ab4-b6f1-7d1af8b594ca"), Code = "template.builder.view", Name = "View Template Builder", Group = "TemplateBuilder", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("03eabd97-b196-4603-bbdd-1b2cdd595ead"), Code = "template.builder.create.draft", Name = "Draft Creation", Group = "TemplateBuilder", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("d272be50-ff26-45cf-bd7a-e9db74813699"), Code = "template.builder.approved.templates.view", Name = "View Approved Template", Group = "TemplateBuilder", IsActive = true, CreatedAt = created },
                new Permission { Id = Guid.Parse("3602f49d-dc10-4faa-9a44-4185a669ea0a"), Code = "template.builder.library.browse", Name = "View Template Library", Group = "TemplateBuilder", IsActive = true, CreatedAt = created }
                );

            // ───────────────── Relationships (clean and deduped) ─────────────────

            // Access-control
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role).WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.User).WithMany(u => u.UserPermissions)
                .HasForeignKey(up => up.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.Permission).WithMany(p => p.UserPermissions)
                .HasForeignKey(up => up.PermissionId).OnDelete(DeleteBehavior.Cascade);

            // 🔒 Join-table uniqueness (prevents duplicates)
            modelBuilder.Entity<RolePermission>()
                .HasIndex(x => new { x.RoleId, x.PermissionId })
                .IsUnique()
                .HasDatabaseName("UX_RolePermissions_Role_Permission");

            modelBuilder.Entity<PlanPermission>()
                .HasIndex(x => new { x.PlanId, x.PermissionId })
                .IsUnique()
                .HasDatabaseName("UX_PlanPermissions_Plan_Permission");

            modelBuilder.Entity<PlanPermission>(e =>
            {
                // Fast lookups
                e.HasIndex(x => x.PlanId);
                e.HasIndex(x => x.PermissionId);

                // One row per (Plan, Permission)
                e.HasIndex(x => new { x.PlanId, x.PermissionId }).IsUnique();
            });
            // Campaign core
            modelBuilder.Entity<Campaign>()
                .HasOne(c => c.Business).WithMany(b => b.Campaigns)
                .HasForeignKey(c => c.BusinessId).IsRequired();

            modelBuilder.Entity<Campaign>()
                .HasMany(c => c.MultiButtons).WithOne(b => b.Campaign)
                .HasForeignKey(b => b.CampaignId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Campaign>(e =>
            {
                e.Property(x => x.TemplateSchemaSnapshot).HasColumnType("jsonb");

                // 🔁 MATCHES PATCH: Campaign → Audiences CASCADE
                e.HasMany(c => c.Audiences).WithOne(a => a.Campaign)
                 .HasForeignKey(a => a.CampaignId).OnDelete(DeleteBehavior.Cascade);

                // already cascade
                e.HasMany(c => c.SendLogs).WithOne(s => s.Campaign)
                 .HasForeignKey(s => s.CampaignId).OnDelete(DeleteBehavior.Cascade);

                // 🔁 MATCHES PATCH: Campaign → MessageLogs CASCADE
                e.HasMany(c => c.MessageLogs).WithOne(m => m.SourceCampaign)
                 .HasForeignKey(m => m.CampaignId).OnDelete(DeleteBehavior.Cascade);
            });


            // Audience / CSV
            modelBuilder.Entity<CsvBatch>(e =>
            {
                e.ToTable("CsvBatches");
                e.HasKey(x => x.Id);
                e.Property(x => x.HeadersJson).HasColumnType("jsonb");
                e.HasIndex(x => x.Checksum).HasDatabaseName("ix_csvbatch_checksum");
                e.HasIndex(x => new { x.BusinessId, x.CreatedAt }).HasDatabaseName("ix_csvbatch_biz_created");
                e.HasIndex(x => new { x.BusinessId, x.AudienceId });
                e.HasOne<Audience>().WithMany()
                 .HasForeignKey(x => x.AudienceId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CsvRow>(e =>
            {
                e.ToTable("CsvRows");
                e.HasKey(x => x.Id);
                e.Property(x => x.RowJson).HasColumnType("jsonb");
                e.HasIndex(x => new { x.BatchId, x.RowIndex }).IsUnique().HasDatabaseName("ux_csvrow_batch_rowidx");
                e.HasIndex(x => x.PhoneE164).HasDatabaseName("ix_csvrow_phone");
                e.HasIndex(x => new { x.BusinessId, x.BatchId });
                e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Audience>(e =>
            {
                e.ToTable("Audiences");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.BusinessId, x.IsDeleted }).HasDatabaseName("ix_audiences_biz_deleted");
                e.HasIndex(x => new { x.BusinessId, x.CampaignId });
                e.HasIndex(x => new { x.BusinessId, x.CsvBatchId });
                e.HasOne(x => x.CsvBatch).WithMany().HasForeignKey(x => x.CsvBatchId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AudienceMember>(e =>
            {
                e.ToTable("AudienceMembers");
                e.HasKey(x => x.Id);
                e.Property(x => x.AttributesJson).HasColumnType("jsonb");
                e.HasIndex(x => new { x.AudienceId, x.PhoneE164 }).IsUnique().HasDatabaseName("ux_audmember_audience_phone");
                e.HasIndex(x => x.ContactId).HasDatabaseName("ix_audmember_contact");
                e.HasOne(x => x.Audience).WithMany(a => a.Members)
                 .HasForeignKey(x => x.AudienceId).OnDelete(DeleteBehavior.Cascade);
            });

            // Recipients — OPTIONAL AudienceMember, OPTIONAL Contact
            modelBuilder.Entity<CampaignRecipient>(e =>
            {
                e.ToTable("CampaignRecipients");
                e.HasKey(x => x.Id);

                e.Property(x => x.ResolvedParametersJson).HasColumnType("jsonb");
                e.Property(x => x.ResolvedButtonUrlsJson).HasColumnType("jsonb");
                e.HasIndex(x => x.IdempotencyKey).HasDatabaseName("ix_campaignrecipients_idempotency");
                e.HasIndex(x => new { x.CampaignId, x.ContactId }).HasDatabaseName("ix_recipients_campaign_contact");

                e.HasOne(r => r.AudienceMember)
                 .WithMany()
                 .HasForeignKey(r => r.AudienceMemberId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(r => r.Contact)
                 .WithMany()
                 .HasForeignKey(r => r.ContactId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(r => r.Campaign)
                 .WithMany(c => c.Recipients)
                 .HasForeignKey(r => r.CampaignId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(r => r.Business)
                 .WithMany()
                 .HasForeignKey(r => r.BusinessId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Send logs — OPTIONAL Contact, REQUIRED Campaign
            modelBuilder.Entity<CampaignSendLog>(e =>
            {
                e.ToTable("CampaignSendLogs");
                e.HasKey(x => x.Id);

                e.Property(x => x.BusinessId).IsRequired();
                e.HasIndex(x => x.MessageId);
                e.HasIndex(x => x.RunId);
                e.HasIndex(x => new { x.BusinessId, x.MessageId }).HasDatabaseName("IX_CampaignSendLogs_Business_MessageId");

                e.HasOne(s => s.Recipient).WithMany(r => r.SendLogs)
                 .HasForeignKey(s => s.RecipientId);

                // ✅ allow null ContactId (fixes 23502 once column is nullable)
                e.HasOne(s => s.Contact).WithMany()
                 .HasForeignKey(s => s.ContactId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(s => s.Campaign).WithMany(c => c.SendLogs)
                 .HasForeignKey(s => s.CampaignId)
                 .IsRequired()
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(s => s.MessageLog).WithMany()
                 .HasForeignKey(s => s.MessageLogId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Message logs — helpful indexes + computed column
            modelBuilder.Entity<MessageLog>(b =>
            {
                b.HasIndex(x => x.MessageId);
                b.HasIndex(x => x.RunId);
                b.HasIndex(x => new { x.BusinessId, x.MessageId }).HasDatabaseName("IX_MessageLogs_Business_MessageId");
                b.HasIndex(x => new { x.BusinessId, x.RecipientNumber }).HasDatabaseName("IX_MessageLogs_Business_Recipient");
                b.Property<DateTime?>("MessageTime").HasComputedColumnSql("COALESCE(\"SentAt\", \"CreatedAt\")", stored: true);
                b.HasIndex("BusinessId", "IsIncoming", "ContactId", "MessageTime").HasDatabaseName("ix_msglogs_biz_in_contact_msgtime");
            });

            // QuickReplies
            modelBuilder.Entity<QuickReply>(e =>
            {
                e.HasIndex(x => new { x.BusinessId, x.Scope, x.IsActive });
                e.HasIndex(x => new { x.BusinessId, x.OwnerUserId, x.IsActive });
                e.HasIndex(x => x.UpdatedAt);
                e.Property(x => x.Title).HasMaxLength(120).IsRequired();
                e.Property(x => x.Language).HasMaxLength(8);
                e.Property(q => q.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            // Contacts — uniqueness
            modelBuilder.Entity<Contact>()
                .HasIndex(c => new { c.BusinessId, c.PhoneNumber }).IsUnique();

            modelBuilder.Entity<ContactRead>()
                .HasIndex(cr => new { cr.ContactId, cr.UserId }).IsUnique();

            modelBuilder.Entity<ContactRead>()
                .HasIndex(cr => new { cr.BusinessId, cr.UserId, cr.ContactId })
                .IsUnique().HasDatabaseName("ux_contactreads_biz_user_contact");

            // WhatsApp settings (principal with composite AK)
            modelBuilder.Entity<WhatsAppSettingEntity>(b =>
            {
                b.ToTable("WhatsAppSettings");
                b.HasAlternateKey(s => new { s.BusinessId, s.Provider })
                 .HasName("AK_WhatsAppSettings_BusinessId_Provider");

                b.HasIndex(x => new { x.Provider, x.WabaId }).HasDatabaseName("IX_WhatsAppSettings_Provider_WabaId");
                b.HasIndex(x => new { x.BusinessId, x.Provider, x.IsActive }).HasDatabaseName("IX_WhatsAppSettings_Business_Provider_IsActive");
                b.HasIndex(x => new { x.Provider, x.WebhookCallbackUrl }).HasDatabaseName("IX_WhatsAppSettings_Provider_CallbackUrl");
            });

            modelBuilder.Entity<Business>()
                .HasMany(b => b.WhatsAppSettings).WithOne()
                .HasForeignKey(s => s.BusinessId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WhatsAppPhoneNumber>(e =>
            {
                e.ToTable("WhatsAppPhoneNumbers");
                e.HasKey(x => x.Id);
                e.Property(x => x.Provider).IsRequired();
                e.Property(x => x.PhoneNumberId).IsRequired();

                e.HasOne<WhatsAppSettingEntity>()
                 .WithMany(s => s.WhatsAppBusinessNumbers)
                 .HasForeignKey(x => new { x.BusinessId, x.Provider })
                 .HasPrincipalKey(s => new { s.BusinessId, s.Provider })
                 .OnDelete(DeleteBehavior.Cascade);

                // ✅ keep this UNIQUE index
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.PhoneNumberId })
                 .IsUnique()
                 .HasDatabaseName("UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId");
            });

            // CTA / Tracking misc
            modelBuilder.Entity<CampaignClickLog>(e =>
            {
                e.HasIndex(x => new { x.CampaignId, x.ClickType, x.ClickedAt });
                e.HasIndex(x => new { x.CampaignId, x.ButtonIndex });
                e.HasIndex(x => new { x.CampaignId, x.ContactId });
            });

            modelBuilder.Entity<CampaignClickDailyAgg>(e =>
            {
                e.HasIndex(x => new { x.CampaignId, x.Day, x.ButtonIndex }).IsUnique();
                e.Property(x => x.Day).HasColumnType("date");
            });

            // Flow graph bits
            modelBuilder.Entity<FlowButtonLink>().HasKey(b => b.Id);

            // Auto-reply flows & nodes (CRUD storage for builder)
            modelBuilder.Entity<AutoReplyFlow>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.HasIndex(f => new { f.BusinessId, f.Name });

                entity.HasMany<AutoReplyFlowNode>()
                      .WithOne(n => n.Flow)
                      .HasForeignKey(n => n.FlowId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AutoReplyFlowNode>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.HasIndex(n => new { n.FlowId, n.NodeName });

                // keep coordinates as an owned type
                entity.OwnsOne(n => n.Position);
            });

            // Features/Plans
      
            // Outbound worker
            modelBuilder.Entity<OutboundCampaignJob>(e =>
            {
                e.ToTable("OutboundCampaignJobs");
                e.HasIndex(x => new { x.Status, x.NextAttemptAt });
                e.HasIndex(x => x.CampaignId);
                e.Property(x => x.Status).HasMaxLength(32);
                e.Property(x => x.LastError).HasMaxLength(4000);
            });

            modelBuilder.Entity<Campaign>()
                 .HasOne(c => c.CTAFlowConfig)      // ✅ use the nav that exists on Campaign
                 .WithMany()
                 .HasForeignKey(c => c.CTAFlowConfigId)
                 .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CTAFlowConfig>(e =>
            {
                e.HasMany(f => f.Steps)
                 .WithOne(s => s.Flow)
                 .HasForeignKey(s => s.CTAFlowConfigId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // keep only this one:
            modelBuilder.Entity<CTAFlowConfig>()
                .HasIndex(f => new { f.BusinessId, f.FlowName, f.IsActive })
                .IsUnique();

            modelBuilder.Entity<CTAFlowStep>(e =>
            {
                e.HasMany(s => s.ButtonLinks)
                 .WithOne(b => b.Step)                 // only if FlowButtonLink has a 'Step' nav
                 .HasForeignKey(b => b.CTAFlowStepId)  // ✅ use existing FK name
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Flow execution logs → delete with their Flow
            //modelBuilder.Entity<FlowExecutionLog>(e =>
            //{
            //    e.ToTable("FlowExecutionLogs");
            //    e.HasKey(x => x.Id);
            //    e.HasIndex(x => x.FlowId).HasDatabaseName("IX_FlowExecutionLogs_FlowId");

            //    // Critical: tie logs to Flow and cascade on delete
            //    e.HasOne<CTAFlowConfig>()
            //     .WithMany()                            // no collection needed
            //     .HasForeignKey(x => x.FlowId)
            //     .OnDelete(DeleteBehavior.Cascade);

            //});
            // Flow execution logs → delete with their Flow
            modelBuilder.Entity<FlowExecutionLog>(e =>
            {
                e.ToTable("FlowExecutionLogs");
                e.HasKey(x => x.Id);

                // existing / kept index
                e.HasIndex(x => x.FlowId)
                 .HasDatabaseName("IX_FlowExecutionLogs_FlowId");

                // 🔍 time-based analytics per business
                e.HasIndex(x => new { x.BusinessId, x.ExecutedAt })
                 .HasDatabaseName("ix_flowexec_biz_executedat");

                // 🔍 campaign-origin segmentation
                e.HasIndex(x => new { x.BusinessId, x.Origin, x.CampaignId })
                 .HasDatabaseName("ix_flowexec_biz_origin_campaign");

                // 🔍 autoreply-origin segmentation
                e.HasIndex(x => new { x.BusinessId, x.Origin, x.AutoReplyFlowId })
                 .HasDatabaseName("ix_flowexec_biz_origin_autoreply");

                // tie logs to Flow and cascade on delete
                e.HasOne<CTAFlowConfig>()
                 .WithMany()
                 .HasForeignKey(x => x.FlowId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ----- ProviderBillingEvents (core for billing dedupe + reads) -----
            modelBuilder.Entity<ProviderBillingEvent>(e =>
            {
                // Hard dedupe (webhook replays, same message/event). Filter keeps NULLs out of the unique constraint.
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.ProviderMessageId, x.EventType })
                 .HasDatabaseName("UX_ProviderBillingEvents_UniqueEvent")
                 .IsUnique()
                 .HasFilter("\"ProviderMessageId\" IS NOT NULL");

                // Time-range scans by event type (used by snapshot)
                e.HasIndex(x => new { x.BusinessId, x.EventType, x.OccurredAt })
                 .HasDatabaseName("IX_Billing_BizEventTime");

                // Group/lookup by conversation window
                e.HasIndex(x => new { x.BusinessId, x.ConversationId })
                 .HasDatabaseName("IX_Billing_BizConversation")
                 .HasFilter("\"ConversationId\" IS NOT NULL");

                // Direct lookups by provider message id
                e.HasIndex(x => new { x.BusinessId, x.ProviderMessageId })
                 .HasDatabaseName("IX_Billing_BizProviderMessage")
                 .HasFilter("\"ProviderMessageId\" IS NOT NULL");
            });

            // ----- MessageLogs (snapshot volume + joins from billing) -----
            modelBuilder.Entity<MessageLog>(e =>
            {
                // Period queries
                e.HasIndex(x => new { x.BusinessId, x.CreatedAt })
                 .HasDatabaseName("IX_MessageLogs_BizCreatedAt");

                // Join from billing by provider message id
                e.HasIndex(x => new { x.BusinessId, x.ProviderMessageId })
                 .HasDatabaseName("IX_MessageLogs_BizProviderMessage")
                 .HasFilter("\"ProviderMessageId\" IS NOT NULL");

                // Conversation aggregation / backfills
                e.HasIndex(x => new { x.BusinessId, x.ConversationId })
                 .HasDatabaseName("IX_MessageLogs_BizConversation")
                 .HasFilter("\"ConversationId\" IS NOT NULL");
            });

            // ----- CampaignSendLogs (status updater lookups) -----
            modelBuilder.Entity<CampaignSendLog>(e =>
            {
                e.HasIndex(x => new { x.BusinessId, x.SendStatus, x.SentAt })
                 .HasDatabaseName("IX_CampaignSendLogs_StatusTime");
            });

            // ----- Optional: provider config lookups used during send/status -----

            modelBuilder.Entity<xbytechat.api.Features.CustomeApi.Models.ApiKey>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Prefix).IsUnique();
                e.Property(x => x.SecretHash).IsRequired();
                e.Property(x => x.Scopes).HasMaxLength(512);
            });

            //modelBuilder.Entity<MessageStatusLog>()
            //    .HasOne<Campaign>()                       // (no nav on MessageStatusLog needed)
            //    .WithMany(c => c.MessageStatusLogs)       // uses Campaign.MessageStatusLogs collection
            //    .HasForeignKey(ms => ms.CampaignId)
            //    .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MessageStatusLog>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(ms => ms.Campaign)          // ✅ use nav
                 .WithMany(c => c.MessageStatusLogs) // or .WithMany() if Campaign doesn't expose the collection
                 .HasForeignKey(ms => ms.CampaignId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            // FK indexes used by hard delete / reads
            modelBuilder.Entity<CampaignSendLog>()
                .HasIndex(x => x.CampaignId)
                .HasDatabaseName("IX_CampaignSendLogs_Campaign");

            modelBuilder.Entity<MessageLog>()
                .HasIndex(x => x.CampaignId)
                .HasDatabaseName("IX_MessageLogs_Campaign");

            modelBuilder.Entity<MessageLog>()
                .HasIndex(x => x.ProviderMessageId)
                .HasDatabaseName("IX_MessageLogs_ProviderMessageId")
                .HasFilter("\"ProviderMessageId\" IS NOT NULL");

            modelBuilder.Entity<TrackingLog>(e =>
            {
                e.HasKey(x => x.Id);

                // indexes (keep)
                e.HasIndex(x => x.CampaignId).HasDatabaseName("IX_TrackingLogs_Campaign");
                e.HasIndex(x => x.MessageLogId).HasDatabaseName("IX_TrackingLogs_MessageLog");
                e.HasIndex(x => x.CampaignSendLogId).HasDatabaseName("IX_TrackingLogs_SendLog");

                // ✅ use navs defined on the POCO
                e.HasOne(t => t.Campaign)
                 .WithMany()
                 .HasForeignKey(t => t.CampaignId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(t => t.MessageLog)
                 .WithMany()
                 .HasForeignKey(t => t.MessageLogId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(t => t.CampaignSendLog)
                 .WithMany()
                 .HasForeignKey(t => t.CampaignSendLogId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OutboundMessageJob>(e =>
            {
                e.ToTable("OutboundMessageJobs");

                // Columns
                e.Property(x => x.Status).HasMaxLength(24);
                e.Property(x => x.LastError).HasMaxLength(4000);
                e.Property(x => x.IdempotencyKey).HasMaxLength(1000)
                .IsRequired(false);

                // Helpful indexes
                e.HasIndex(x => x.CampaignId);

                e.HasIndex(x => new { x.BusinessId, x.IdempotencyKey })
                 .IsUnique()
                 .HasFilter("\"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''")
                 .HasDatabaseName("UX_Outbox_Biz_IdemKey");

                // 🔥 Hot-path for producer & reaper (keep this one; remove the 2-col variant)
                e.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt })
                 .HasDatabaseName("IX_Outbox_StatusDueCreated");
            });

            modelBuilder.Entity<WhatsAppTemplate>(e =>
            {
                e.ToTable("WhatsAppTemplates");

                // ── 🔐 HARD DEDUPE: pick one of these keys (both are safe together) ─────────────

                // Prefer external TemplateId when present (provider's primary key)
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.TemplateId })
                 .IsUnique()
                 .HasFilter(@"""TemplateId"" IS NOT NULL")
                 .HasDatabaseName("UX_WAT_BizProvTemplateId");

                // Fallback uniqueness: Name + LanguageCode per provider per business
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.Name, x.LanguageCode })
                 .IsUnique()
                 .HasDatabaseName("UX_WAT_BizProvNameLang");

                // ── 🔎 Your existing read-path indexes (kept) ──────────────────────────────────

                // Core “list for a business” scans (Used by List endpoint + internal dashboards)
                // Filters: BusinessId, Provider?, IsActive; sort by UpdatedAt/LastSyncedAt
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.IsActive, x.UpdatedAt, x.LastSyncedAt })
                 .HasDatabaseName("IX_WAT_Business_Provider_IsActive_Sort")
#if NET7_0_OR_GREATER
                 .IsDescending(false, false, false, true, true) // DESC on UpdatedAt/LastSyncedAt where supported
#endif
                 ;

                // When provider filter isn’t present — still useful for a business-wide “active” list
                e.HasIndex(x => new { x.BusinessId, x.IsActive })
                 .HasDatabaseName("IX_WAT_Business_IsActive");

                // Exact fetch by external TemplateId (preferred lookup when present)
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.TemplateId })
                 .HasDatabaseName("IX_WAT_Business_Provider_TemplateId")
                 .HasFilter(@"""TemplateId"" IS NOT NULL");

                // Fallback fetch used by sync/GetOne: Name + LanguageCode for a given provider
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.Name, x.LanguageCode })
                 .HasDatabaseName("IX_WAT_Business_Provider_Name_Lang");

                // List filters: Status + Language on active rows
                e.HasIndex(x => new { x.BusinessId, x.Provider, x.Status, x.LanguageCode })
                 .HasDatabaseName("IX_WAT_Business_Provider_Status_Lang_Active")
                 .HasFilter(@"""IsActive"" = TRUE");

                // TTL check in sync: ORDER BY LastSyncedAt DESC LIMIT 1 per business
                e.HasIndex(x => new { x.BusinessId, x.LastSyncedAt })
                 .HasDatabaseName("IX_WAT_Business_LastSyncedAt")
#if NET7_0_OR_GREATER
                 .IsDescending(false, true) // DESC LastSyncedAt where supported
#endif
                 ;

                // Helpful single-column lookups/sorts you do frequently
                e.HasIndex(x => x.UpdatedAt).HasDatabaseName("IX_WAT_UpdatedAt");
                e.HasIndex(x => x.LastSyncedAt).HasDatabaseName("IX_WAT_LastSyncedAt");

                // NOTE: for q= search on Name/Body with Contains/ILIKE, add pg_trgm GIN via migration:
                //   CREATE EXTENSION IF NOT EXISTS pg_trgm;
                //   CREATE INDEX IX_WAT_trgm_lower_name ON "WhatsAppTemplates" USING GIN (lower("Name") gin_trgm_ops);
                //   CREATE INDEX IX_WAT_trgm_body        ON "WhatsAppTemplates" USING GIN ("Body" gin_trgm_ops);
                // (done with migrationBuilder.Sql(...))
            });
            //TemplateDrafts
            modelBuilder.Entity<TemplateDraft>(e =>
            {
                // 🔐 One logical draft key per business
                e.HasIndex(x => new { x.BusinessId, x.Key })
                 .IsUnique()
                 .HasDatabaseName("UX_TemplateDrafts_Biz_Key");

                // 🔎 Common scans
                e.HasIndex(x => new { x.BusinessId, x.UpdatedAt })
                 .HasDatabaseName("IX_TemplateDrafts_Biz_UpdatedAt");

                e.HasIndex(x => x.CreatedAt)
                 .HasDatabaseName("IX_TemplateDrafts_CreatedAt");
            });
            //TemplateDraftVariants

            modelBuilder.Entity<TemplateDraftVariant>(e =>
            {
                // 🔐 One language variant per draft
                e.HasIndex(x => new { x.TemplateDraftId, x.Language })
                 .IsUnique()
                 .HasDatabaseName("UX_TDraftVariants_Draft_Language");

                // 🔎 Ready-for-submission filters
                e.HasIndex(x => new { x.TemplateDraftId, x.IsReadyForSubmission })
                 .HasDatabaseName("IX_TDraftVariants_Draft_Ready");

                // 🔎 Language lookups
                e.HasIndex(x => x.Language)
                 .HasDatabaseName("IX_TDraftVariants_Language");
            });

            //TemplateLibraryItems
            modelBuilder.Entity<TemplateLibraryItem>(e =>
            {
                // 🔐 Unique per industry
                e.HasIndex(x => new { x.Industry, x.Key })
                 .IsUnique()
                 .HasDatabaseName("UX_TLibrary_Industry_Key");

                // 🔎 Featured filters per industry
                e.HasIndex(x => new { x.Industry, x.IsFeatured })
                 .HasDatabaseName("IX_TLibrary_Industry_Featured");

                // 🔎 Browse by category
                e.HasIndex(x => x.Category)
                 .HasDatabaseName("IX_TLibrary_Category");
            });

            //TemplateLibraryVariants
            modelBuilder.Entity<TemplateLibraryVariant>(e =>
            {
                // 🔐 One language per library item
                e.HasIndex(x => new { x.LibraryItemId, x.Language })
                 .IsUnique()
                 .HasDatabaseName("UX_TLibraryVariants_Item_Lang");

                // 🔎 Language browsing
                e.HasIndex(x => x.Language)
                 .HasDatabaseName("IX_TLibraryVariants_Language");
            });
            // ESU / Facebook: single-row flags per Business
            modelBuilder.Entity<IntegrationFlags>(b =>
            {
                b.ToTable("IntegrationFlags");
                b.HasKey(x => x.BusinessId);

                b.Property(x => x.BusinessId).ValueGeneratedNever();

                b.Property(x => x.FacebookEsuCompleted)
                 .IsRequired()
                 .HasDefaultValue(false);



                b.Property(x => x.CreatedAtUtc)
                 .HasDefaultValueSql("timezone('utc', now())")
                 .ValueGeneratedOnAdd();

                b.Property(x => x.UpdatedAtUtc)
                 .HasDefaultValueSql("timezone('utc', now())")
                 .ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<EsuToken>(b =>
            {
                b.ToTable("EsuTokens");
                b.HasIndex(x => new { x.BusinessId, x.Provider }).IsUnique();
            });

            modelBuilder.Entity<AccountInsightsAction>(cfg =>
            {
                cfg.ToTable("AccountInsightsActions");

                cfg.HasKey(x => x.Id);

                cfg.Property(x => x.BusinessId)
                    .IsRequired();

                cfg.Property(x => x.ActionType)
                    .HasMaxLength(64)
                    .IsRequired();

                cfg.Property(x => x.Label)
                    .HasMaxLength(256);

                cfg.Property(x => x.Actor)
                    .HasMaxLength(256);

                cfg.Property(x => x.MetaJson)
                    .HasColumnType("text");

                cfg.Property(x => x.CreatedAtUtc)
                    .IsRequired();

                cfg.HasIndex(x => new { x.BusinessId, x.CreatedAtUtc });
            });

            // ----- Payment: Subscription -----
            modelBuilder.Entity<Subscription>(b =>
            {
                b.ToTable("Subscriptions");
                b.HasKey(x => x.Id);

                b.HasOne(x => x.Business)
                    .WithMany() // later you can expose Business.Subscriptions if needed
                    .HasForeignKey(x => x.BusinessId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Plan)
                    .WithMany()
                    .HasForeignKey(x => x.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Store enums as ints for simplicity
                b.Property(x => x.Status)
                    .HasConversion<int>()
                    .IsRequired();

                b.Property(x => x.BillingCycle)
                    .HasConversion<int>()
                    .IsRequired();

                b.Property(x => x.CurrentPeriodStartUtc).IsRequired();
                b.Property(x => x.CurrentPeriodEndUtc).IsRequired();

                b.Property(x => x.GatewayCustomerId).HasMaxLength(200);
                b.Property(x => x.GatewaySubscriptionId).HasMaxLength(200);
            });

            // ----- Payment: PaymentTransaction -----
            modelBuilder.Entity<PaymentTransaction>(b =>
            {
                b.ToTable("PaymentTransactions");
                b.HasKey(x => x.Id);

                b.HasOne(x => x.Business)
                    .WithMany()
                    .HasForeignKey(x => x.BusinessId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Subscription)
                    .WithMany(s => s.Payments)
                    .HasForeignKey(x => x.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(x => x.Invoice)
                    .WithMany(i => i.Payments)
                    .HasForeignKey(x => x.InvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.Property(x => x.Status)
                    .HasConversion<int>()
                    .IsRequired();

                b.Property(x => x.Amount)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.Currency)
                    .HasMaxLength(10)
                    .IsRequired();

                b.Property(x => x.Gateway)
                    .HasMaxLength(50)
                    .IsRequired();

                b.Property(x => x.GatewayPaymentId).HasMaxLength(200);
                b.Property(x => x.GatewayOrderId).HasMaxLength(200);
                b.Property(x => x.GatewaySignature).HasMaxLength(500);

                b.Property(x => x.CreatedAtUtc).IsRequired();
            });
            // ----- Payment: Invoice -----
            modelBuilder.Entity<Invoice>(b =>
            {
                b.ToTable("Invoices");
                b.HasKey(x => x.Id);

                b.HasOne(x => x.Business)
                    .WithMany()
                    .HasForeignKey(x => x.BusinessId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Subscription)
                    .WithMany(s => s.Invoices)
                    .HasForeignKey(x => x.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.Property(x => x.InvoiceNumber)
                    .HasMaxLength(100)
                    .IsRequired();

                b.HasIndex(x => x.InvoiceNumber)
                    .IsUnique();

                b.Property(x => x.Status)
                    .HasConversion<int>()
                    .IsRequired();

                b.Property(x => x.SubtotalAmount)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.TaxAmount)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.DiscountAmount)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.TotalAmount)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.Currency)
                    .HasMaxLength(10)
                    .IsRequired();

                b.Property(x => x.AppliedCouponCode)
                    .HasMaxLength(100);

                b.Property(x => x.TaxBreakdownJson)
                    .HasColumnType("text");

                b.Property(x => x.IssuedAtUtc)
                    .IsRequired();


                b.HasOne(x => x.Plan)
                   .WithMany()
                   .HasForeignKey(x => x.PlanId)
                   .OnDelete(DeleteBehavior.SetNull);

                b.Property(x => x.BillingCycle)
                    .HasConversion<int?>();

                b.Property(x => x.InvoiceNumber)
                    .HasMaxLength(100)
                    .IsRequired();

                b.HasIndex(x => x.InvoiceNumber).IsUnique();
            });
            // ----- Payment: InvoiceLineItem -----
            modelBuilder.Entity<InvoiceLineItem>(b =>
            {
                b.ToTable("InvoiceLineItems");
                b.HasKey(x => x.Id);

                b.HasOne(x => x.Invoice)
                    .WithMany(i => i.LineItems)
                    .HasForeignKey(x => x.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.Property(x => x.Description)
                    .HasMaxLength(500)
                    .IsRequired();

                b.Property(x => x.Quantity)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.UnitPrice)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.LineTotal)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();
            });
            // ----- Payment: Coupon -----
            modelBuilder.Entity<Coupon>(b =>
            {
                b.ToTable("Coupons");
                b.HasKey(x => x.Id);

                b.Property(x => x.Code)
                    .HasMaxLength(100)
                    .IsRequired();

                b.HasIndex(x => x.Code)
                    .IsUnique();

                b.Property(x => x.DiscountType)
                    .HasConversion<int>()
                    .IsRequired();

                b.Property(x => x.DiscountValue)
                    .HasColumnType("numeric(18,2)")
                    .IsRequired();

                b.Property(x => x.IsActive)
                    .IsRequired();

                // Optional plan scope
                b.HasOne<Plan>()
                    .WithMany()
                    .HasForeignKey(x => x.PlanId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            var toUpper = new ValueConverter<string, string>(
                    v => v == null ? null : v.ToUpperInvariant(),
                    v => v
                );

            // --- PlanQuota ---
            modelBuilder.Entity<PlanQuota>(e =>
            {
                e.Property(p => p.QuotaKey)
                    .HasMaxLength(128)
                    .HasConversion(toUpper); // normalize

                // Unique per Plan + QuotaKey
                e.HasIndex(p => new { p.PlanId, p.QuotaKey }).IsUnique();
            });

            // --- BusinessQuotaOverride ---
            modelBuilder.Entity<BusinessQuotaOverride>(e =>
            {
                e.Property(p => p.QuotaKey)
                    .HasMaxLength(128)
                    .HasConversion(toUpper); // normalize

                // Unique per Business + QuotaKey
                e.HasIndex(p => new { p.BusinessId, p.QuotaKey }).IsUnique();
            });

            // --- BusinessUsageCounter ---
            modelBuilder.Entity<BusinessUsageCounter>(e =>
            {
                e.Property(p => p.QuotaKey)
                    .HasMaxLength(128)
                    .HasConversion(toUpper); // normalize

                // One active window per Business + QuotaKey + Period + WindowStart
                e.HasIndex(u => new { u.BusinessId, u.QuotaKey, u.Period, u.WindowStartUtc }).IsUnique();
            });

            // --- Permission ---
            modelBuilder.Entity<Permission>(e =>
            {
                e.Property(p => p.Code)
                    .IsRequired()
                    .HasMaxLength(120)
                    .HasConversion(toUpper); // normalize

                e.HasIndex(p => p.Code).IsUnique(); // enforce uniqueness
            });

        }

    }
}
