using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserName = table.Column<string>(type: "text", nullable: true),
                    RoleAtTime = table.Column<string>(type: "text", nullable: true),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IPAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: false),
                    NodesJson = table.Column<string>(type: "text", nullable: false),
                    EdgesJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoReplyFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NodesJson = table.Column<string>(type: "text", nullable: false),
                    EdgesJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IndustryTag = table.Column<string>(type: "text", nullable: true),
                    UseCase = table.Column<string>(type: "text", nullable: true),
                    IsDefaultTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    Keyword = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoReplyFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoReplyLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    ReplyContent = table.Column<string>(type: "text", nullable: false),
                    FlowName = table.Column<string>(type: "text", nullable: true),
                    MessageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoReplyLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignClickDailyAgg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateTime>(type: "date", nullable: false),
                    ButtonIndex = table.Column<int>(type: "integer", nullable: false),
                    Clicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignClickDailyAgg", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignClickLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignSendLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ButtonIndex = table.Column<int>(type: "integer", nullable: false),
                    ButtonTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClickType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Destination = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignClickLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogClickLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    UserPhone = table.Column<string>(type: "text", nullable: true),
                    BotId = table.Column<string>(type: "text", nullable: true),
                    CategoryBrowsed = table.Column<string>(type: "text", nullable: true),
                    ProductBrowsed = table.Column<string>(type: "text", nullable: true),
                    CTAJourney = table.Column<string>(type: "text", nullable: true),
                    TemplateId = table.Column<string>(type: "text", nullable: false),
                    RefMessageId = table.Column<string>(type: "text", nullable: false),
                    ButtonText = table.Column<string>(type: "text", nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CampaignSendLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    FollowUpSent = table.Column<bool>(type: "boolean", nullable: false),
                    LastInteractionType = table.Column<string>(type: "text", nullable: true),
                    MessageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanSnapshot = table.Column<string>(type: "text", nullable: true),
                    CtaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogClickLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessionStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactReads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CTADefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ButtonText = table.Column<string>(type: "text", nullable: false),
                    ButtonType = table.Column<string>(type: "text", nullable: false),
                    TargetUrl = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTADefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CTAFlowConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTAFlowConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedWebhookLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    SourceModule = table.Column<string>(type: "text", nullable: true),
                    FailureType = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedWebhookLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureAccess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureMaster",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureMaster", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlowExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignSendLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackingLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    TriggeredByButton = table.Column<string>(type: "text", nullable: true),
                    TemplateName = table.Column<string>(type: "text", nullable: true),
                    TemplateType = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RawResponse = table.Column<string>(type: "text", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ButtonIndex = table.Column<short>(type: "smallint", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundCampaignJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundCampaignJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Group = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatureMatrix",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatureMatrix", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalClicks = table.Column<int>(type: "integer", nullable: false),
                    LastClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MostClickedCTA = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderBillingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    ConversationCategory = table.Column<string>(type: "text", nullable: true),
                    IsChargeable = table.Column<bool>(type: "boolean", nullable: true),
                    PriceAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    PriceCurrency = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderBillingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuickReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    TagsCsv = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickReplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReminderType = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    RecurrencePattern = table.Column<string>(type: "text", nullable: true),
                    SendWhatsappNotification = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedCampaign = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCTAType = table.Column<string>(type: "text", nullable: true),
                    LastClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FollowUpSent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsSystemDefined = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ColorHex = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsSystemTag = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserFeatureAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFeatureAccess", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutoCleanupEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastCleanupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    HasImageHeader = table.Column<bool>(type: "boolean", nullable: false),
                    PlaceholderCount = table.Column<int>(type: "integer", nullable: false),
                    ButtonsJson = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoReplyFlowEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<string>(type: "text", nullable: false),
                    TargetNodeId = table.Column<string>(type: "text", nullable: false),
                    SourceHandle = table.Column<string>(type: "text", nullable: true),
                    TargetHandle = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoReplyFlowEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoReplyFlowEdges_AutoReplyFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "AutoReplyFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutoReplyFlowNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    NodeName = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    Position_X = table.Column<double>(type: "double precision", nullable: false),
                    Position_Y = table.Column<double>(type: "double precision", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoReplyFlowNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoReplyFlowNodes_AutoReplyFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "AutoReplyFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutoReplyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: false),
                    ReplyMessage = table.Column<string>(type: "text", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FlowName = table.Column<string>(type: "text", nullable: true),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IndustryTag = table.Column<string>(type: "text", nullable: true),
                    SourceChannel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoReplyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoReplyRules_AutoReplyFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "AutoReplyFlows",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CTAFlowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CTAFlowConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerButtonText = table.Column<string>(type: "text", nullable: false),
                    TriggerButtonType = table.Column<string>(type: "text", nullable: false),
                    TemplateToSend = table.Column<string>(type: "text", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    RequiredTag = table.Column<string>(type: "text", nullable: true),
                    RequiredSource = table.Column<string>(type: "text", nullable: true),
                    PositionX = table.Column<float>(type: "real", nullable: true),
                    PositionY = table.Column<float>(type: "real", nullable: true),
                    TemplateType = table.Column<string>(type: "text", nullable: true),
                    UseProfileName = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileNameSlot = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CTAFlowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CTAFlowSteps_CTAFlowConfigs_CTAFlowConfigId",
                        column: x => x.CTAFlowConfigId,
                        principalTable: "CTAFlowConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Businesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: true),
                    BusinessName = table.Column<string>(type: "text", nullable: false),
                    BusinessEmail = table.Column<string>(type: "text", nullable: false),
                    RepresentativeName = table.Column<string>(type: "text", nullable: true),
                    CreatedByPartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    CompanyPhone = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Businesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Businesses_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlanPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanPermissions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowButtonLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ButtonText = table.Column<string>(type: "text", nullable: false),
                    NextStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ButtonType = table.Column<string>(type: "text", nullable: false),
                    ButtonSubType = table.Column<string>(type: "text", nullable: false),
                    ButtonValue = table.Column<string>(type: "text", nullable: false),
                    CTAFlowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ButtonIndex = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowButtonLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowButtonLinks_CTAFlowSteps_CTAFlowStepId",
                        column: x => x.CTAFlowStepId,
                        principalTable: "CTAFlowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessPlanInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    TotalMonthlyQuota = table.Column<int>(type: "integer", nullable: false),
                    RemainingMessages = table.Column<int>(type: "integer", nullable: false),
                    QuotaResetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WalletBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPlanInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPlanInfos_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MessageTemplate = table.Column<string>(type: "text", nullable: false),
                    TemplateId = table.Column<string>(type: "text", nullable: true),
                    MessageBody = table.Column<string>(type: "text", nullable: true),
                    FollowUpTemplateId = table.Column<string>(type: "text", nullable: true),
                    CampaignType = table.Column<string>(type: "text", nullable: true),
                    CtaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CTAFlowConfigId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    ImageCaption = table.Column<string>(type: "text", nullable: true),
                    TemplateParameters = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberId = table.Column<string>(type: "text", nullable: true),
                    TemplateSchemaSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    AudienceId = table.Column<Guid>(type: "uuid", nullable: true),
                    VideoUrl = table.Column<string>(type: "text", nullable: true),
                    DocumentUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Campaigns_CTADefinitions_CtaId",
                        column: x => x.CtaId,
                        principalTable: "CTADefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Campaigns_CTAFlowConfigs_CTAFlowConfigId",
                        column: x => x.CTAFlowConfigId,
                        principalTable: "CTAFlowConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Campaigns_Campaigns_SourceCampaignId",
                        column: x => x.SourceCampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeadSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Tags = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastContactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextFollowUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCTAInteraction = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCTAType = table.Column<string>(type: "text", nullable: true),
                    LastClickedProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAutomationPaused = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    Group = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileName = table.Column<string>(type: "text", nullable: true),
                    ProfileNameUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    WhatsAppBusinessNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PhoneNumberId = table.Column<string>(type: "text", nullable: true),
                    WabaId = table.Column<string>(type: "text", nullable: true),
                    SenderDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WebhookSecret = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WebhookVerifyToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WebhookCallbackUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppSettings", x => x.Id);
                    table.UniqueConstraint("AK_WhatsAppSettings_BusinessId_Provider", x => new { x.BusinessId, x.Provider });
                    table.ForeignKey(
                        name: "FK_WhatsAppSettings_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignButtons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsFromTemplate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignButtons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignButtons_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignFlowOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ButtonText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OverrideNextTemplate = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignFlowOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignFlowOverrides_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignVariableMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Component = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StaticValue = table.Column<string>(type: "text", nullable: true),
                    Expression = table.Column<string>(type: "text", nullable: true),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignVariableMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignVariableMaps_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactTags_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContactTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadTimelines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CTAType = table.Column<string>(type: "text", nullable: true),
                    CTASourceType = table.Column<string>(type: "text", nullable: true),
                    CTASourceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTimelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadTimelines_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadTimelines_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientNumber = table.Column<string>(type: "text", nullable: false),
                    MessageContent = table.Column<string>(type: "text", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RawResponse = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CTAFlowConfigId = table.Column<Guid>(type: "uuid", nullable: true),
                    CTAFlowStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    FlowVersion = table.Column<int>(type: "integer", nullable: true),
                    ButtonBundleJson = table.Column<string>(type: "text", nullable: true),
                    IsIncoming = table.Column<bool>(type: "boolean", nullable: false),
                    RenderedBody = table.Column<string>(type: "text", nullable: true),
                    RefMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    IsChargeable = table.Column<bool>(type: "boolean", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    ConversationCategory = table.Column<string>(type: "text", nullable: true),
                    ConversationStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PriceAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    PriceCurrency = table.Column<string>(type: "text", nullable: true),
                    MessageTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, computedColumnSql: "COALESCE(\"SentAt\", \"CreatedAt\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MessageStatusLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerProfileName = table.Column<string>(type: "text", nullable: true),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    TemplateName = table.Column<string>(type: "text", nullable: true),
                    TemplateCategory = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    IsSessionOpen = table.Column<bool>(type: "boolean", nullable: false),
                    MetaTimestamp = table.Column<long>(type: "bigint", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<int>(type: "integer", nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageStatusLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageStatusLogs_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageStatusLogs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageStatusLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsGranted = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppPhoneNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "text", nullable: false),
                    WhatsAppBusinessNumber = table.Column<string>(type: "text", nullable: false),
                    SenderDisplayName = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppPhoneNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppPhoneNumbers_WhatsAppSettings_BusinessId_Provider",
                        columns: x => new { x.BusinessId, x.Provider },
                        principalTable: "WhatsAppSettings",
                        principalColumns: new[] { "BusinessId", "Provider" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudienceMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneRaw = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PhoneE164 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AttributesJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsTransientContact = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PromotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BotId = table.Column<string>(type: "text", nullable: true),
                    MessagePreview = table.Column<string>(type: "text", nullable: true),
                    ClickedCTA = table.Column<string>(type: "text", nullable: true),
                    CategoryBrowsed = table.Column<string>(type: "text", nullable: true),
                    ProductBrowsed = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAutoTagged = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedParametersJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResolvedButtonUrlsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    MaterializedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignRecipients_AudienceMembers_AudienceMemberId",
                        column: x => x.AudienceMemberId,
                        principalTable: "AudienceMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CampaignRecipients_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignRecipients_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignRecipients_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CampaignSendLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageBody = table.Column<string>(type: "text", nullable: false),
                    TemplateId = table.Column<string>(type: "text", nullable: true),
                    SendStatus = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    DeviceInfo = table.Column<string>(type: "text", nullable: true),
                    MacAddress = table.Column<string>(type: "text", nullable: true),
                    SourceChannel = table.Column<string>(type: "text", nullable: true),
                    DeviceType = table.Column<string>(type: "text", nullable: true),
                    Browser = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    IsClicked = table.Column<bool>(type: "boolean", nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClickType = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRetryStatus = table.Column<string>(type: "text", nullable: true),
                    AllowRetry = table.Column<bool>(type: "boolean", nullable: false),
                    MessageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CTAFlowConfigId = table.Column<Guid>(type: "uuid", nullable: true),
                    CTAFlowStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ButtonBundleJson = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignSendLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignSendLogs_CampaignRecipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "CampaignRecipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignSendLogs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignSendLogs_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CampaignSendLogs_MessageLogs_MessageLogId",
                        column: x => x.MessageLogId,
                        principalTable: "MessageLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampaignSendLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TrackingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignSendLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ButtonText = table.Column<string>(type: "text", nullable: true),
                    CTAType = table.Column<string>(type: "text", nullable: true),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    TemplateId = table.Column<string>(type: "text", nullable: true),
                    MessageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClickedVia = table.Column<string>(type: "text", nullable: true),
                    Referrer = table.Column<string>(type: "text", nullable: true),
                    ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IPAddress = table.Column<string>(type: "text", nullable: true),
                    DeviceType = table.Column<string>(type: "text", nullable: true),
                    Browser = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    FollowUpSent = table.Column<bool>(type: "boolean", nullable: false),
                    LastInteractionType = table.Column<string>(type: "text", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    StepId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId",
                        column: x => x.CampaignSendLogId,
                        principalTable: "CampaignSendLogs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrackingLogs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrackingLogs_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrackingLogs_MessageLogs_MessageLogId",
                        column: x => x.MessageLogId,
                        principalTable: "MessageLogs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Audiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    CsvBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audiences_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CsvBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsvBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsvBatches_Audiences_AudienceId",
                        column: x => x.AudienceId,
                        principalTable: "Audiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CsvRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    PhoneRaw = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PhoneE164 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RowJson = table.Column<string>(type: "jsonb", nullable: false),
                    ValidationError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsvRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsvRows_CsvBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CsvBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "Group", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("0485154c-dde5-4732-a7aa-a379c77a5b27"), "messaging.send.template", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.send.template" },
                    { new Guid("0dedac5b-81c8-44c3-8cfe-76c58e29c6db"), "automation_trigger_test", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation_trigger_test" },
                    { new Guid("205b87c7-b008-4e51-9fea-798c2dc4f9c2"), "admin.whatsappsettings.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Admin", true, "admin.whatsappsettings.view" },
                    { new Guid("29461562-ef9c-48c0-a606-482ff57b8f95"), "messaging.send", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.send" },
                    { new Guid("30000000-0000-0000-0000-000000000000"), "dashboard.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Dashboard", true, "dashboard.view" },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "campaign.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Campaign", true, "campaign.view" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "campaign.create", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Campaign", true, "campaign.create" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "campaign.delete", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Campaign", true, "campaign.delete" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "product.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Catalog", true, "product.view" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "product.create", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Catalog", true, "product.create" },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "product.delete", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Catalog", true, "product.delete" },
                    { new Guid("30000000-0000-0000-0000-000000000007"), "contacts.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "CRM", true, "contacts.view" },
                    { new Guid("30000000-0000-0000-0000-000000000008"), "tags.edit", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "tags.edit" },
                    { new Guid("30000000-0000-0000-0000-000000000009"), "admin.business.approve", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Admin", true, "admin.business.approve" },
                    { new Guid("30000000-0000-0000-0000-000000000010"), "admin.logs.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "admin.logs.view" },
                    { new Guid("30000000-0000-0000-0000-000000000011"), "admin.plans.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Admin", true, "admin.plans.view" },
                    { new Guid("30000000-0000-0000-0000-000000000012"), "admin.plans.create", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Admin", true, "admin.plans.create" },
                    { new Guid("30000000-0000-0000-0000-000000000013"), "admin.plans.update", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Admin", true, "admin.plans.update" },
                    { new Guid("30000000-0000-0000-0000-000000000014"), "admin.plans.delete", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Admin", true, "admin.plans.delete" },
                    { new Guid("636b17f2-1c54-4e26-a8cd-dbf561dcb522"), "automation.View.Template.Flow_analytics", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.View.Template.Flow_analytics" },
                    { new Guid("6e4d3a86-7cf9-4ac2-b8a7-ed10c9f0173d"), "settings.whatsapp.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Settings", true, "Settings - WhatsApp View" },
                    { new Guid("74828fc0-e358-4cfc-b924-13719a0d9f50"), "inbox.menu", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Inbox", true, "inbox.menu" },
                    { new Guid("74c8034f-d9cb-4a17-8578-a9f765bd845c"), "messaging.report.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.report.view" },
                    { new Guid("7d7cbceb-4ce7-4835-85cd-59562487298d"), "automation.View.TemplatePlusFreetext.Flow", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.View.TemplatePlusFreetext.Flow" },
                    { new Guid("821480c6-1464-415e-bba8-066fcb4e7e63"), "automation.menu", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.menu" },
                    { new Guid("918a61d0-5ab6-46af-a3d3-41e37b7710f9"), "automation.Create.Template.Flow", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.Create.Template.Flow" },
                    { new Guid("93c5d5a7-f8dd-460a-8c7b-e3788440ba3a"), "automation.Create.TemplatePlusFreetext.Flow", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.Create.TemplatePlusFreetext.Flow" },
                    { new Guid("974af1f9-3caa-4857-a1a7-48462c389332"), "messaging.send.text", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.send.text" },
                    { new Guid("98572fe7-d142-475a-b990-f248641809e2"), "settings.profile.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Settings", true, "settings.profile.view" },
                    { new Guid("9ae90cfe-3fea-4307-b024-3083c2728148"), "automation.View.Template.Flow", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.View.Template.Flow" },
                    { new Guid("ad36cdb7-5221-448b-a6a6-c35c9f88d021"), "inbox.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Inbox", true, "inbox.view" },
                    { new Guid("adfa8490-9705-4a36-a86e-d5bff7ddc220"), "automation.View.TemplatePlusFreeText.Flow_analytics", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Automation", true, "automation.View.TemplatePlusFreeText.Flow_analytics" },
                    { new Guid("bbc5202a-eac9-40bb-aa78-176c677dbf5b"), "messaging.whatsappsettings.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.whatsappsettings.view" },
                    { new Guid("c819f1bd-422d-4609-916c-cc185fe44ab0"), "messaging.status.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.status.view" },
                    { new Guid("eecd0fac-223c-4dba-9fa1-2a6e973d61d1"), "messaging.inbox.view", new DateTime(2025, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), null, "Messaging", true, "messaging.inbox.view" }
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[] { new Guid("5f9f5de1-a0b2-48ba-b03d-77b27345613f"), "basic", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Default free plan", true, "Basic" });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "IsSystemDefined", "Name" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Super Admin", true, false, "admin" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Business Partner", true, false, "partner" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Reseller Partner", true, false, "reseller" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Business Owner", true, false, "business" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Staff", true, false, "staff" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "BusinessId", "CreatedAt", "DeletedAt", "Email", "IsDeleted", "Name", "PasswordHash", "RefreshToken", "RefreshTokenExpiry", "RoleId", "Status" },
                values: new object[] { new Guid("62858aa2-3a54-4fd5-8696-c343d9af7634"), null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@xbytechat.com", false, "Super Admin", "JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=", null, null, new Guid("00000000-0000-0000-0000-000000000001"), "active" });

            migrationBuilder.CreateIndex(
                name: "ix_audmember_contact",
                table: "AudienceMembers",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ux_audmember_audience_phone",
                table: "AudienceMembers",
                columns: new[] { "AudienceId", "PhoneE164" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audiences_biz_deleted",
                table: "Audiences",
                columns: new[] { "BusinessId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_BusinessId_CampaignId",
                table: "Audiences",
                columns: new[] { "BusinessId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_BusinessId_CsvBatchId",
                table: "Audiences",
                columns: new[] { "BusinessId", "CsvBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_CampaignId",
                table: "Audiences",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Audiences_CsvBatchId",
                table: "Audiences",
                column: "CsvBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyFlowEdges_FlowId",
                table: "AutoReplyFlowEdges",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyFlowNodes_FlowId",
                table: "AutoReplyFlowNodes",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyRules_FlowId",
                table: "AutoReplyRules",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_PlanId",
                table: "Businesses",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPlanInfos_BusinessId",
                table: "BusinessPlanInfos",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignButtons_CampaignId",
                table: "CampaignButtons",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignClickDailyAgg_CampaignId_Day_ButtonIndex",
                table: "CampaignClickDailyAgg",
                columns: new[] { "CampaignId", "Day", "ButtonIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignClickLogs_CampaignId_ButtonIndex",
                table: "CampaignClickLogs",
                columns: new[] { "CampaignId", "ButtonIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignClickLogs_CampaignId_ClickType_ClickedAt",
                table: "CampaignClickLogs",
                columns: new[] { "CampaignId", "ClickType", "ClickedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignClickLogs_CampaignId_ContactId",
                table: "CampaignClickLogs",
                columns: new[] { "CampaignId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignFlowOverrides_CampaignId",
                table: "CampaignFlowOverrides",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_AudienceMemberId",
                table: "CampaignRecipients",
                column: "AudienceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_BusinessId",
                table: "CampaignRecipients",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignRecipients_ContactId",
                table: "CampaignRecipients",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_campaignrecipients_idempotency",
                table: "CampaignRecipients",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "ix_recipients_campaign_contact",
                table: "CampaignRecipients",
                columns: new[] { "CampaignId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_BusinessId",
                table: "Campaigns",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CTAFlowConfigId",
                table: "Campaigns",
                column: "CTAFlowConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CtaId",
                table: "Campaigns",
                column: "CtaId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_SourceCampaignId",
                table: "Campaigns",
                column: "SourceCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_Business_MessageId",
                table: "CampaignSendLogs",
                columns: new[] { "BusinessId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_CampaignId",
                table: "CampaignSendLogs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_ContactId",
                table: "CampaignSendLogs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_MessageId",
                table: "CampaignSendLogs",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_MessageLogId",
                table: "CampaignSendLogs",
                column: "MessageLogId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_RecipientId",
                table: "CampaignSendLogs",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_RunId",
                table: "CampaignSendLogs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_UserId",
                table: "CampaignSendLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignVariableMaps_CampaignId",
                table: "CampaignVariableMaps",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactReads_ContactId_UserId",
                table: "ContactReads",
                columns: new[] { "ContactId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_contactreads_biz_user_contact",
                table: "ContactReads",
                columns: new[] { "BusinessId", "UserId", "ContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_BusinessId_PhoneNumber",
                table: "Contacts",
                columns: new[] { "BusinessId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactTags_ContactId",
                table: "ContactTags",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactTags_TagId",
                table: "ContactTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "ix_csvbatch_biz_created",
                table: "CsvBatches",
                columns: new[] { "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_csvbatch_checksum",
                table: "CsvBatches",
                column: "Checksum");

            migrationBuilder.CreateIndex(
                name: "IX_CsvBatches_AudienceId",
                table: "CsvBatches",
                column: "AudienceId");

            migrationBuilder.CreateIndex(
                name: "IX_CsvBatches_BusinessId_AudienceId",
                table: "CsvBatches",
                columns: new[] { "BusinessId", "AudienceId" });

            migrationBuilder.CreateIndex(
                name: "ix_csvrow_phone",
                table: "CsvRows",
                column: "PhoneE164");

            migrationBuilder.CreateIndex(
                name: "IX_CsvRows_BusinessId_BatchId",
                table: "CsvRows",
                columns: new[] { "BusinessId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "ux_csvrow_batch_rowidx",
                table: "CsvRows",
                columns: new[] { "BatchId", "RowIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ctaflowconfigs_biz_active_name",
                table: "CTAFlowConfigs",
                columns: new[] { "BusinessId", "IsActive", "FlowName" });

            migrationBuilder.CreateIndex(
                name: "IX_CTAFlowConfigs_BusinessId_FlowName_IsActive",
                table: "CTAFlowConfigs",
                columns: new[] { "BusinessId", "FlowName", "IsActive" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CTAFlowSteps_CTAFlowConfigId",
                table: "CTAFlowSteps",
                column: "CTAFlowConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureAccess_BusinessId_FeatureName",
                table: "FeatureAccess",
                columns: new[] { "BusinessId", "FeatureName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlowButtonLinks_CTAFlowStepId",
                table: "FlowButtonLinks",
                column: "CTAFlowStepId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTimelines_BusinessId",
                table: "LeadTimelines",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTimelines_ContactId",
                table: "LeadTimelines",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_Business_MessageId",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_Business_Recipient",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "RecipientNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_CampaignId",
                table: "MessageLogs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ContactId",
                table: "MessageLogs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_MessageId",
                table: "MessageLogs",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_RunId",
                table: "MessageLogs",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "ix_msglogs_biz_in_contact_msgtime",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "IsIncoming", "ContactId", "MessageTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageStatusLogs_BusinessId",
                table: "MessageStatusLogs",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageStatusLogs_CampaignId",
                table: "MessageStatusLogs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageStatusLogs_UserId",
                table: "MessageStatusLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundCampaignJobs_CampaignId",
                table: "OutboundCampaignJobs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundCampaignJobs_Status_NextAttemptAt",
                table: "OutboundCampaignJobs",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanPermissions_PermissionId",
                table: "PlanPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPermissions_PlanId",
                table: "PlanPermissions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplies_BusinessId_OwnerUserId_IsActive",
                table: "QuickReplies",
                columns: new[] { "BusinessId", "OwnerUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplies_BusinessId_Scope_IsActive",
                table: "QuickReplies",
                columns: new[] { "BusinessId", "Scope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplies_UpdatedAt",
                table: "QuickReplies",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_CampaignId",
                table: "TrackingLogs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_CampaignSendLogId",
                table: "TrackingLogs",
                column: "CampaignSendLogId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_ContactId",
                table: "TrackingLogs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingLogs_MessageLogId",
                table: "TrackingLogs",
                column: "MessageLogId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId",
                table: "UserPermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BusinessId",
                table: "Users",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId",
                table: "WhatsAppPhoneNumbers",
                columns: new[] { "BusinessId", "Provider", "PhoneNumberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppSettings_Business_Provider_IsActive",
                table: "WhatsAppSettings",
                columns: new[] { "BusinessId", "Provider", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppSettings_Provider_BusinessNumber",
                table: "WhatsAppSettings",
                columns: new[] { "Provider", "WhatsAppBusinessNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppSettings_Provider_CallbackUrl",
                table: "WhatsAppSettings",
                columns: new[] { "Provider", "WebhookCallbackUrl" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppSettings_Provider_PhoneNumberId",
                table: "WhatsAppSettings",
                columns: new[] { "Provider", "PhoneNumberId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppSettings_Provider_WabaId",
                table: "WhatsAppSettings",
                columns: new[] { "Provider", "WabaId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Name",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Name_Language_Provider",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Name", "Language", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_BusinessId_Provider",
                table: "WhatsAppTemplates",
                columns: new[] { "BusinessId", "Provider" });

            migrationBuilder.AddForeignKey(
                name: "FK_AudienceMembers_Audiences_AudienceId",
                table: "AudienceMembers",
                column: "AudienceId",
                principalTable: "Audiences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Audiences_CsvBatches_CsvBatchId",
                table: "Audiences",
                column: "CsvBatchId",
                principalTable: "CsvBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CsvBatches_Audiences_AudienceId",
                table: "CsvBatches");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AutomationFlows");

            migrationBuilder.DropTable(
                name: "AutoReplyFlowEdges");

            migrationBuilder.DropTable(
                name: "AutoReplyFlowNodes");

            migrationBuilder.DropTable(
                name: "AutoReplyLogs");

            migrationBuilder.DropTable(
                name: "AutoReplyRules");

            migrationBuilder.DropTable(
                name: "BusinessPlanInfos");

            migrationBuilder.DropTable(
                name: "CampaignButtons");

            migrationBuilder.DropTable(
                name: "CampaignClickDailyAgg");

            migrationBuilder.DropTable(
                name: "CampaignClickLogs");

            migrationBuilder.DropTable(
                name: "CampaignFlowOverrides");

            migrationBuilder.DropTable(
                name: "CampaignVariableMaps");

            migrationBuilder.DropTable(
                name: "CatalogClickLogs");

            migrationBuilder.DropTable(
                name: "ChatSessionStates");

            migrationBuilder.DropTable(
                name: "ContactReads");

            migrationBuilder.DropTable(
                name: "ContactTags");

            migrationBuilder.DropTable(
                name: "CsvRows");

            migrationBuilder.DropTable(
                name: "FailedWebhookLogs");

            migrationBuilder.DropTable(
                name: "FeatureAccess");

            migrationBuilder.DropTable(
                name: "FeatureMaster");

            migrationBuilder.DropTable(
                name: "FlowButtonLinks");

            migrationBuilder.DropTable(
                name: "FlowExecutionLogs");

            migrationBuilder.DropTable(
                name: "LeadTimelines");

            migrationBuilder.DropTable(
                name: "MessageStatusLogs");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "OutboundCampaignJobs");

            migrationBuilder.DropTable(
                name: "PlanFeatureMatrix");

            migrationBuilder.DropTable(
                name: "PlanPermissions");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ProviderBillingEvents");

            migrationBuilder.DropTable(
                name: "QuickReplies");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "TrackingLogs");

            migrationBuilder.DropTable(
                name: "UserFeatureAccess");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "WebhookSettings");

            migrationBuilder.DropTable(
                name: "WhatsAppPhoneNumbers");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplates");

            migrationBuilder.DropTable(
                name: "AutoReplyFlows");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "CTAFlowSteps");

            migrationBuilder.DropTable(
                name: "CampaignSendLogs");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "WhatsAppSettings");

            migrationBuilder.DropTable(
                name: "CampaignRecipients");

            migrationBuilder.DropTable(
                name: "MessageLogs");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AudienceMembers");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Audiences");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "CsvBatches");

            migrationBuilder.DropTable(
                name: "Businesses");

            migrationBuilder.DropTable(
                name: "CTADefinitions");

            migrationBuilder.DropTable(
                name: "CTAFlowConfigs");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
