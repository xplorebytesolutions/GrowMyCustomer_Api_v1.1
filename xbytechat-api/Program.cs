using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Exceptions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using xbytechat.api;
using xbytechat.api.AuthModule.Services;
using xbytechat.api.Features.AccessControl.Services;
using xbytechat.api.Features.AuditTrail.Services;
using xbytechat.api.Features.CampaignModule.Services;
using xbytechat.api.Features.CampaignTracking.Services;
using xbytechat.api.Features.Catalog.Services;
using xbytechat.api.Features.MessageManagement.Services;
using xbytechat.api.Features.MessagesEngine.PayloadBuilders;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Features.PlanManagement.Services;
using xbytechat.api.Features.TemplateModule.Services;
using xbytechat.api.Features.Webhooks.Services;
using xbytechat.api.Features.Webhooks.Services.Processors;
using xbytechat.api.Features.Webhooks.Services.Resolvers;
using xbytechat.api.Helpers;
using xbytechat.api.Middlewares;
using xbytechat.api.PayloadBuilders;
using xbytechat.api.Repositories.Implementations;
using xbytechat.api.Repositories.Interfaces;
using xbytechat.api.Services;
using xbytechat.api.Services.Messages.Implementations;
using xbytechat.api.Services.Messages.Interfaces;
using xbytechat_api.WhatsAppSettings.Services;
using xbytechat_api.WhatsAppSettings.Validators;
using EnginePayloadBuilders = xbytechat.api.Features.MessagesEngine.PayloadBuilders;
using xbytechat.api.Features.CTAManagement.Services;
using xbytechat.api.Features.Tracking.Services;
using xbytechat.api.Features.Webhooks.BackgroundWorkers;
using xbytechat.api.Features.CTAFlowBuilder.Services;
using xbytechat.api.Features.FlowAnalytics.Services;
using xbytechat.api.Features.Inbox.Repositories;
using xbytechat.api.Features.Inbox.Services;
using xbytechat.api.Features.Inbox.Hubs;
using Microsoft.AspNetCore.SignalR;
using xbytechat.api.SignalR;
using xbytechat.api.Features.AutoReplyBuilder.Repositories;
using xbytechat.api.Features.AutoReplyBuilder.Services;
using xbytechat.api.Features.AutoReplyBuilder.Flows.Repositories;
using xbytechat.api.Features.BusinessModule.Services;
using xbytechat.api.Features.ReportingModule.Services;
using xbytechat.api.Features.Automation.Repositories;
using xbytechat.api.Features.Automation.Services;
using Npgsql;
using System.Net;
using xbytechat.api.WhatsAppSettings.Providers;
using xbytechat.api.Features.CampaignTracking.Config;
using xbytechat.api.Features.CampaignTracking.Worker;
using xbytechat.api.Infrastructure.Flows;
using xbytechat.api.Features.Webhooks.Pinnacle.Services.Adapters;
using xbytechat.api.Features.Webhooks.Directory;
using xbytechat.api.Features.Webhooks.Status;
using xbytechat.api.Features.WhatsAppSettings.Services;
using xbytechat.api.WhatsAppSettings.Services;
using xbytechat_api.Features.Billing.Services;
using xbytechat.api.Features.Audiences.Services;
using xbytechat.api.Features.CampaignModule.Helpers;
using Microsoft.AspNetCore.HttpOverrides;
using xbytechat.api.Features.CustomeApi.Services;
using Microsoft.AspNetCore.Authentication;
using xbytechat.api.Features.CustomeApi.Auth;
using Microsoft.OpenApi.Models;
using xbytechat.api.Infrastructure.Schema;
using xbytechat.api.Features.CampaignModule.Workers;
using xbytechat.api.Infrastructure.Observability;
using xbytechat.api.Features.CampaignTracking.Logging;
using xbytechat.api.Infrastructure.RateLimiting;
using xbytechat.api.Features.CampaignModule.SendEngine;
using xbytechat.api.Features.MessageLogging.Services;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using xbytechat.api.Features.CampaignModule.CountryCodes;
using xbytechat.api.Features.CampaignModule.Import;
using xbytechat.api.Features.TemplateModule.Abstractions;
using xbytechat.api.Features.TemplateModule.Config;
using xbytechat.api.Features.ESU.Shared;
using xbytechat.api.Features.ESU.Facebook.Services;
using xbytechat.api.Features.ESU.Facebook.Options;
using Microsoft.Extensions.Options;
using xbytechat.api.Features.ESU.Facebook.Abstractions;
using xbytechat.api.Features.ESU.Facebook.Clients;
using xbytechat.api.Features.ESU.Shared.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using xbytechat.api.Features.Billing.Security;
using Microsoft.AspNetCore.Identity;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccountInsights.Services;
using xbytechat.api.Features.Payment.Services;
using xbytechat.api.Features.Payment.Options;
using xbytechat.api.Features.Entitlements.Services;
using xbytechat.api.Features.Auditing.FlowExecutions.Services;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Features.CRM.Services;
using xbytechat.api.Features.CRM.Timelines.Services;
using xbytechat.api.Features.ChatInbox.Services;
using xbytechat.api.Features.TeamStaff.Services;
using xbytechat.api.Features.AccessControl.BusinessRoles.Services;
using xbytechat.api.Features.AccessControl.BusinessRolePermissions.Services;
using xbytechat.api.Infrastructure.Security;
using xbytechat.api.Features.CustomFields.Services;
using xbytechat.api.Infrastructure.Swagger;


var builder = WebApplication.CreateBuilder(args);

#region Local overrides configuration
// Load optional local secrets overrides (never committed) before env variables
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();
#endregion

#region 🔷 Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .Enrich.WithExceptionDetails()
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();
#endregion

#region 🔷 Database Setup (PostgreSQL)
//var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseNpgsql(connStr).EnableSensitiveDataLogging()
//);

// 🔷 Database Setup (PostgreSQL + UpdatedAtUtc interceptor)
builder.Services.AddSingleton<UpdatedAtUtcInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var connection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connection) || connection.Contains("<set-via-env", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured. Set it via environment variable (ConnectionStrings__DefaultConnection) or appsettings.Local.json.");
    }

    options.UseNpgsql(connection);

    // register UpdatedAt interceptor
    options.AddInterceptors(sp.GetRequiredService<UpdatedAtUtcInterceptor>());

    if (!env.IsDevelopment())
    {
        options.EnableSensitiveDataLogging(false);
        options.EnableDetailedErrors(false);
    }
});


//Console.WriteLine($"[DEBUG] Using Connection String: {connStr}");
#endregion

#region 🔷 Generic Repository Pattern
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
#endregion

#region 🔷 Core Modules (Business/Auth)
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
#endregion


// Policy not implemented yet
#region 🔷 Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminOrOwner, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var user = ctx.User;
            return user.IsAdminLike() || user.IsBusinessOwnerLike();
        });
    });
});
#endregion
#region 🔷 Messaging Services & WhatsApp

builder.Services.AddHttpClient<IMessageService, MessageService>();
builder.Services.AddScoped<WhatsAppService>();
builder.Services.AddScoped<IMessageStatusService, MessageStatusService>();
builder.Services.AddScoped<ITemplateMessageSender, TemplateMessageSender>();
#endregion
builder.Services.AddHttpClient();


#region 🔷 Payload Builders
builder.Services.AddScoped<xbytechat.api.PayloadBuilders.IWhatsAppPayloadBuilder, xbytechat.api.PayloadBuilders.TextMessagePayloadBuilder>();
builder.Services.AddScoped<xbytechat.api.PayloadBuilders.IWhatsAppPayloadBuilder, xbytechat.api.PayloadBuilders.ImageMessagePayloadBuilder>();
builder.Services.AddScoped<xbytechat.api.PayloadBuilders.IWhatsAppPayloadBuilder, xbytechat.api.PayloadBuilders.TemplateMessagePayloadBuilder>();
#endregion

#region 🔷 Catalog & CRM Modules
builder.Services.AddScoped<IContactSummaryService, ContactSummaryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICatalogTrackingService, CatalogTrackingService>();
builder.Services.AddScoped<ICatalogDashboardService, CatalogDashboardService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<IContactTagService, ContactTagService>();
builder.Services.AddScoped<ICustomFieldsService, CustomFieldsService>();
#endregion

#region 🔷 Billing 
builder.Services.AddScoped<IBillingIngestService, BillingIngestService>();
builder.Services.AddScoped<IBillingReadService, BillingReadService>();
builder.Services.AddSingleton<IMetaSignatureValidator, MetaSignatureValidator>();
#endregion

#region 🔷 Campaign Management
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ICampaignSendLogService, CampaignSendLogService>();
builder.Services.AddScoped<ICampaignSendLogEnricher, CampaignSendLogEnricher>();
builder.Services.AddScoped<ICampaignAnalyticsService, CampaignAnalyticsService>();
builder.Services.AddScoped<ICampaignRetryService, CampaignRetryService>();
builder.Services.AddScoped<ICampaignTrackingRetryService, CampaignTrackingRetryService>();
builder.Services.AddHttpClient<IWhatsAppTemplateService, WhatsAppTemplateService>();
builder.Services.AddScoped<ICampaignRecipientService, CampaignRecipientService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ITemplatePreviewService, TemplatePreviewService>();
builder.Services.AddScoped<IOutboundCampaignQueueService, OutboundCampaignQueueService>();
builder.Services.AddScoped<ICampaignPreviewService, CampaignPreviewService>();
builder.Services.AddScoped<IAudienceService, AudienceService>();
builder.Services.AddScoped<ICampaignVariableMapService, CampaignVariableMapService>();
builder.Services.AddScoped<IAudienceImportService, AudienceImportService>();
builder.Services.AddScoped<ICampaignMaterializationService, CampaignMaterializationService>();
builder.Services.AddScoped<ICampaignDispatchPlannerService, CampaignDispatchPlannerService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();

builder.Services.AddScoped<ICampaignDryRunService, CampaignDryRunService>();
builder.Services.AddScoped<ICampaignRetargetService, CampaignRetargetService>();

// API
builder.Services.AddScoped<ICustomApiService, CustomApiService>();
// CSV ingest
builder.Services.AddScoped<CampaignCsvSchemaBuilder>();
builder.Services.AddScoped<ICsvBatchService, CsvBatchService>();
builder.Services.AddScoped<IVariableResolver, VariableResolver>();
builder.Services.AddScoped<ICampaignMaterializer, CampaignMaterializer>();
builder.Services.AddScoped<ICampaignAudienceAttachmentService, CampaignAudienceAttachmentService>();
builder.Services.AddScoped<ICampaignDispatcher, CampaignDispatcher>();
builder.Services.AddScoped<IVariableMappingService, NoopVariableMappingService>();
//builder.Services.AddScoped<IOutboundCampaignQueueService, NoopOutboundCampaignQueueService>();
builder.Services.AddScoped<IMappingSuggestionService, MappingSuggestionService>();

builder.Services.AddHostedService<OutboundSenderWorker>();
//Refactoring
builder.Services.Configure<xbytechat.api.Features.CampaignTracking.Logging.BatchingOptions>(
    builder.Configuration.GetSection("Batching"));

builder.Services.AddSingleton<ICampaignLogSink, CampaignLogSink>();
builder.Services.AddHostedService<CampaignLogFlushWorker>();
MetricsRegistry.Configure(builder.Configuration);
builder.Services.AddSingleton<IPhoneNumberRateLimiter, PhoneNumberRateLimiter>();
builder.Services.AddSingleton<ITemplatePayloadBuilder, TemplatePayloadBuilder>();


// Provider payload mappers
builder.Services.AddSingleton<MetaCloudPayloadMapper>();
builder.Services.AddSingleton<PinnaclePayloadMapper>();

builder.Services.Configure<MessageLogSinkOptions>(
    builder.Configuration.GetSection("MessageLogSink"));

builder.Services.AddSingleton<PostgresCopyMessageLogSink>();
builder.Services.AddSingleton<IMessageLogSink>(sp => sp.GetRequiredService<PostgresCopyMessageLogSink>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PostgresCopyMessageLogSink>());

builder.Services.AddScoped<ICampaignSendValidator, CampaignSendValidator>();

builder.Services.AddScoped<IJourneyExportService, JourneyExportService>();

#endregion

#region 🔷 Webhook Management
builder.Services.AddHostedService<OutboxReaperWorker>();

builder.Services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
builder.Services.AddScoped<IWhatsAppWebhookDispatcher, WhatsAppWebhookDispatcher>();

builder.Services.AddScoped<ITemplateWebhookProcessor, TemplateWebhookProcessor>();
builder.Services.AddScoped<IMessageIdResolver, MessageIdResolver>();
builder.Services.AddScoped<IClickWebhookProcessor, ClickWebhookProcessor>();
builder.Services.AddScoped<ILeadTimelineService, LeadTimelineService>();
builder.Services.AddScoped<IFailedWebhookLogService, FailedWebhookLogService>();
builder.Services.AddSingleton<IWebhookQueueService, WebhookQueueService>();
builder.Services.AddHostedService<WebhookQueueWorker>();
builder.Services.AddHostedService<FailedWebhookLogCleanupService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddHostedService<WebhookAutoCleanupWorker>();
builder.Services.AddScoped<IProviderDirectory, ProviderDirectory>();
builder.Services.AddScoped<IMessageStatusUpdater, MessageStatusUpdater>();
builder.Services.AddScoped<IPinnacleToMetaAdapter, PinnacleToMetaAdapter>();


#endregion

#region 🔷 Access Control & Permission
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddScoped<ITeamStaffService,TeamStaffService>();
// BusinessesRole
builder.Services.AddScoped<IBusinessRoleService, BusinessRoleService>();
// BusinessesRolePermissions
builder.Services.AddScoped<IBusinessRolePermissionsService, BusinessRolePermissionsService>();


#endregion

#region 🔷 Tracking
builder.Services.AddScoped<ITrackingService, TrackingService>();
builder.Services.AddScoped<IMessageAnalyticsService, MessageAnalyticsService>();
builder.Services.AddScoped<IUrlBuilderService, UrlBuilderService>();
builder.Services.AddScoped<IContactJourneyService, ContactJourneyService>();

builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection("Tracking"));
builder.Services.AddSingleton<IClickTokenService, ClickTokenService>();
builder.Services.AddSingleton<IClickEventQueue, InProcessClickEventQueue>();
builder.Services.AddHostedService<ClickLogWorker>();

builder.Services.AddScoped<IMessageLogsReportService, MessageLogsReportService>();

#endregion

#region Template Creation
builder.Services.AddScoped<ITemplateDraftService, TemplateDraftService>();
builder.Services.AddScoped<ITemplateSubmissionService, TemplateSubmissionService>();
builder.Services.AddScoped<ITemplateLibraryService, TemplateLibraryService>();
builder.Services.AddScoped<IMetaTemplateClient, MetaTemplateClient>();
builder.Services.AddScoped<IWhatsAppTemplatesSyncBridge, WhatsAppTemplatesSyncBridge>();
builder.Services.AddScoped<IMetaCredentialsResolver, MetaCredentialsResolver>();
builder.Services.AddScoped<IMetaTemplateClient, MetaTemplateClient>();
builder.Services.AddScoped<ITemplateStatusService, TemplateStatusService>();
builder.Services.Configure<UploadLimitsOptions>(
    builder.Configuration.GetSection("TemplateBuilder:UploadLimits"));

builder.Services.AddScoped<IMetaUploadService, MetaUploadService>();
builder.Services.AddScoped<ITemplateNameCheckService, TemplateNameCheckService>();
builder.Services.AddScoped<ITemplateDraftLifecycleService, TemplateDraftLifecycleService>();

#endregion
#region 🔷 Flow Builder
builder.Services.AddScoped<ICTAFlowService, CTAFlowService>();

//builder.Services.Configure<FlowClickTokenOptions>(
//    builder.Configuration.GetSection("FlowClickTokens"));

builder.Services.AddOptions<FlowClickTokenOptions>()
    .BindConfiguration("FlowClickTokens")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && o.Secret.Length >= 32,
              "Secret required (≥32 chars).")
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps,
              "BaseUrl must be an absolute https URL.")
    .Validate(o => o.TtlHours > 0, "TtlHours must be positive.")
    .ValidateOnStart();

builder.Services.AddSingleton<IFlowClickTokenService, FlowClickTokenService>();
builder.Services.AddScoped<IFlowRuntimeService, FlowRuntimeService>();  //
builder.Services.AddScoped<ICtaFlowRuntimeService, CtaFlowRuntimeService>();

#endregion

#region 🔷 Audit Trail Logging
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
#endregion
builder.Services.AddScoped<IContactProfileService, ContactProfileService>();
builder.Services.AddScoped<IFlowExecutionQueryService, FlowExecutionQueryService>();
#region 🔷 WhatsApp settings
builder.Services.AddScoped<IWhatsAppSettingsService, WhatsAppSettingsService>();
builder.Services.AddValidatorsFromAssemblyContaining<SaveWhatsAppSettingValidator>();
builder.Services.AddHttpClient<IMessageEngineService, MessageEngineService>();
builder.Services.AddScoped<IWhatsAppTemplateFetcherService, WhatsAppTemplateFetcherService>();
builder.Services.AddScoped<EnginePayloadBuilders.TextMessagePayloadBuilder>();
builder.Services.AddScoped<EnginePayloadBuilders.ImageMessagePayloadBuilder>();
builder.Services.AddScoped<EnginePayloadBuilders.TemplateMessagePayloadBuilder>();
builder.Services.AddScoped<EnginePayloadBuilders.CtaMessagePayloadBuilder>();
builder.Services.AddScoped<IPlanManager, PlanManager>();
builder.Services.AddScoped<ICTAManagementService, CTAManagementService>();
builder.Services.AddScoped<IWabaSubscriptionClient, WabaSubscriptionClient>();
//builder.Services.AddScoped<IWhatsAppProviderFactory, WhatsAppProviderFactory>();
builder.Services.AddScoped<xbytechat.api.Features.MessagesEngine.Factory.IWhatsAppProviderFactory,
                           xbytechat.api.Features.MessagesEngine.Factory.WhatsAppProviderFactory>();
builder.Services.AddScoped<IWhatsAppSenderService, WhatsAppSenderService>();

builder.Services.AddHttpClient("wa:pincale", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddHttpClient("wa:meta_cloud", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<MetaTemplateCatalogProvider>();
builder.Services.AddScoped<PinnacleTemplateCatalogProvider>();
builder.Services.AddScoped<ITemplateSyncService, TemplateSyncService>();

// WhatsApp phone number management
builder.Services.AddScoped<IWhatsAppPhoneNumberService, WhatsAppPhoneNumberService>();

#endregion

#region Worker
builder.Services.AddHostedService<TemplateSyncWorker>();
//builder.Services.AddHostedService<OutboundCampaignSendWorker>();

#endregion
#region 🔷 Inbox
builder.Services.AddScoped<IUnreadCountService, UnreadCountService>();

builder.Services.AddScoped<IFlowAnalyticsService, FlowAnalyticsService>();
builder.Services.AddScoped<IInboxService, InboxService>();
builder.Services.AddScoped<IInboundMessageProcessor, InboundMessageProcessor>();
builder.Services.AddScoped<IInboxRepository, InboxRepository>();
builder.Services.AddScoped<IQuickReplyService, QuickReplyService>();
// ChatInbox
builder.Services.AddScoped<IChatInboxQueryService, ChatInboxQueryService>();
builder.Services.AddScoped<IChatInboxCommandService, ChatInboxCommandService>();
builder.Services.AddScoped<IChatInboxAssignmentService, ChatInboxAssignmentService>();
builder.Services.AddScoped<IChatInboxMediaUploadService, ChatInboxMediaUploadService>();
builder.Services.AddScoped<IChatInboxMediaContentService, ChatInboxMediaContentService>();

#endregion

#region 🔷 Access Control
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();
builder.Services.AddScoped<IBusinessPermissionOverrideService, BusinessPermissionOverrideService>();

#endregion

#region 🔷 AutoReplyBuilder Module
//builder.Services.AddScoped<IAutoReplyRepository, AutoReplyRepository>();
//builder.Services.AddScoped<IAutoReplyService, AutoReplyService>();
builder.Services.AddScoped<IAutoReplyFlowRepository, AutoReplyFlowRepository>();
builder.Services.AddScoped<IAutoReplyFlowService, AutoReplyFlowService>();
builder.Services.AddScoped<IAutoReplyRuntimeService, AutoReplyRuntimeService>();
builder.Services.AddScoped<IChatSessionStateService, ChatSessionStateService>();
builder.Services.AddScoped<IAgentAssignmentService, AgentAssignmentService>();
builder.Services.AddScoped<IAutoReplyLogService, AutoReplyLogService>();
builder.Services.AddScoped<IFlowExecutionLogger, FlowExecutionLogger>();
#endregion

#region 🔷 Automation Module
builder.Services.AddScoped<IAutomationFlowRepository, AutomationFlowRepository>();
builder.Services.AddScoped<IAutomationRunner, AutomationRunner>();
builder.Services.AddScoped<IAutomationService, AutomationService>();
#endregion


#region 🔐 JWT Authentication (Bearer token only, no cookies)

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? string.Empty)
            ),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    // ✅ Tell the pipeline we are handling this
                    context.NoResult();

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["x-token-expired"] = "true";

                    var payload =
                        "{\"success\":false,\"code\":\"TOKEN_EXPIRED\",\"message\":\"Token expired. Please login again.\"}";

                    return context.Response.WriteAsync(payload);
                }

                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/api/hubs/inbox"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


builder.Services.AddAuthorization();
#endregion
builder.Services.Configure<StaticApiKeyOptions>(
    builder.Configuration.GetSection("ApiKeys:Static"));

#region 🌐 CORS Setup (Bearer mode, no credentials)

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    var raw = builder.Configuration["Cors:AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(raw))
        allowedOrigins = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
Console.WriteLine("[CORS] Allowed origins => " + string.Join(", ", allowedOrigins ?? Array.Empty<string>()));



builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
#endregion

#region ✅ MVC + Swagger + Middleware
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();

//builder.Services.AddSwaggerGen(options =>
//{
//    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
//    {
//        Title = "xByteChat API",
//        Version = "v1",
//        Description = "API documentation for xByteChat project"
//    });
//});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "xByteChat API",
        Version = "v1",
        Description = "API documentation for xByteChat project"
    });
    
    options.OperationFilter<FileUploadOperationFilter>();
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Paste your static key (no quotes). Example: api_live_DEV_xxx",
        In = ParameterLocation.Header,
        Name = "X-Auth-Key",
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        },
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

#endregion

#region ✅ Embedded Signup

// 1) Options binding (use consistent path/casing)
builder.Services.Configure<EsuOptions>(builder.Configuration.GetSection("EmbeddedSignup"));

// 2) Infra
builder.Services.AddHttpClient(); // harmless global client registration

// 3) ESU core services
builder.Services.AddScoped<IEsuStateStore, MemoryEsuStateStore>();
builder.Services.AddScoped<IEsuFlagStore, EsuFlagStore>();
builder.Services.AddScoped<IFacebookEsuService, FacebookEsuService>();
builder.Services.AddScoped<IEsuTokenStore, EsuTokenStore>();
// Environment-aware validation
var isDev = builder.Environment.IsDevelopment();

builder.Services
    .AddOptions<xbytechat.api.Features.ESU.Facebook.Options.FacebookOauthOptions>()
    .Bind(builder.Configuration.GetSection("EmbeddedSignup:Facebook"))
    .PostConfigure(o =>
    {
        o.GraphBaseUrl = string.IsNullOrWhiteSpace(o.GraphBaseUrl)
            ? "https://graph.facebook.com"
            : o.GraphBaseUrl.TrimEnd('/');
        o.GraphApiVersion = string.IsNullOrWhiteSpace(o.GraphApiVersion)
            ? "v20.0"
            : o.GraphApiVersion.Trim('/');
    })
    // ✅ AppId must always exist
    .Validate(o => !string.IsNullOrWhiteSpace(o.AppId),
        "EmbeddedSignup:Facebook:AppId is required.")
    // ✅ AppSecret required only in non-Development
    .Validate(o => isDev || !string.IsNullOrWhiteSpace(o.AppSecret),
        "EmbeddedSignup:Facebook:AppSecret is required in non-Development.")
    // ✅ RedirectUri must be absolute (https required outside dev)
    .Validate(o =>
    {
        if (string.IsNullOrWhiteSpace(o.RedirectUri)) return false;
        if (!Uri.TryCreate(o.RedirectUri, UriKind.Absolute, out var uri)) return false;
        return isDev || uri.Scheme == Uri.UriSchemeHttps;
    }, "EmbeddedSignup:Facebook:RedirectUri must be an absolute HTTPS URL.")
    .ValidateOnStart();

// 4) Typed HttpClient for OAuth token exchange
builder.Services.AddHttpClient<IFacebookOauthClient,FacebookOauthClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// 5) ESU flag cache configuration
builder.Services.AddOptions<EsuFlagCacheOptions>()
    .Bind(builder.Configuration.GetSection("EmbeddedSignup:FlagCache"))
    .PostConfigure(o =>
    {
        if (o.TtlSeconds <= 0) o.TtlSeconds = 30;
        if (o.MissTtlSeconds <= 0) o.MissTtlSeconds = 5;
    });

// 6) Token retrieval service
builder.Services.AddScoped<IFacebookTokenService, FacebookTokenService>();


builder.Services.AddHttpClient<IFacebookGraphClient, FacebookGraphClient>(client =>
 {
     client.Timeout = TimeSpan.FromSeconds(15);
 });

builder.Services.AddScoped<IEsuStatusService, EsuStatusService>();



builder.Services.Configure<FacebookOptions>(builder.Configuration.GetSection("EmbeddedSignup:Facebook"));

builder.Services.Configure<UiOptions>(builder.Configuration.GetSection("Ui"));
#endregion

#region Rate Limiter
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("GlobalLimiter", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    o.AddFixedWindowLimiter("AuthLimiter", opt =>
    {
        opt.PermitLimit = 10;   // login/signup attempts per minute
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

#endregion

#region Account Insignts
builder.Services.AddScoped<IAccountInsightsService, AccountInsightsService>();
builder.Services.AddScoped<IAccountInsightsAlertService, AccountInsightsAlertService>();

#endregion

#region payment module
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IAccessGuard, AccessGuard>();
builder.Services.AddHttpClient<IPaymentGatewayService, RazorpayPaymentGatewayService>();
builder.Services.AddScoped<ISubscriptionCheckoutService, SubscriptionCheckoutService>();
builder.Services.AddScoped<PaymentOverviewService>();
builder.Services.AddScoped<IAccessGuard, AccessGuard>();
builder.Services.AddScoped<SubscriptionLifecycleService>();
builder.Services.Configure<SubscriptionLifecycleOptions>(
    builder.Configuration.GetSection("SubscriptionLifecycle"));

#endregion

#region Quota Module (Entitlement)
builder.Services.AddScoped<IQuotaService, QuotaService>();
#endregion
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

#region SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();
#endregion

builder.Services.AddHttpClient("customapi-webhooks", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ICtaJourneyPublisher, CtaJourneyPublisher>();
builder.Services.AddScoped<CtaJourneyPublisher>();

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Log.Error(e.ExceptionObject as Exception, "Unhandled exception (AppDomain)");


TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.Error(e.Exception, "Unobserved task exception");
    e.SetObserved();
};
var app = builder.Build();



app.MapGet("/api/debug/cors", () => Results.Ok(new
{
    Allowed = app.Services.GetRequiredService<IConfiguration>()
              .GetSection("Cors:AllowedOrigins").Get<string[]>()
}));
app.MapGet("/api/debug/db", async (AppDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();
        return Results.Ok("ok");
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});
app.MapGet("/api/debug/_dbping", async (IConfiguration cfg) =>
{
    try
    {
        var cs = cfg.GetConnectionString("DefaultConnection");
        await using var conn = new Npgsql.NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("select version()", conn);
        var ver = (string?)await cmd.ExecuteScalarAsync();
        return Results.Ok(new { ok = true, version = ver });
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "DB ping failed", detail: ex.ToString(), statusCode: 500);
    }
});
app.MapGet("/api/debug/conn", (IConfiguration cfg) =>
{
    var cs = cfg.GetConnectionString("DefaultConnection") ?? "";
    var b = new NpgsqlConnectionStringBuilder(cs);
    return Results.Ok(new
    {
        host = b.Host,
        port = b.Port,
        database = b.Database,
        username = b.Username,
        sslmode = b.SslMode.ToString(),
        hasPassword = !string.IsNullOrEmpty(b.Password)
    });
});
// Try DNS resolution of the DB host that /api/debug/conn reports
app.MapGet("/api/debug/dns", (IConfiguration cfg) =>
{
    var cs = cfg.GetConnectionString("DefaultConnection") ?? "";
    var b = new NpgsqlConnectionStringBuilder(cs);
    try
    {
        var ips = Dns.GetHostAddresses(b.Host);
        return Results.Ok(new { host = b.Host, addresses = ips.Select(i => i.ToString()).ToArray() });
    }
    catch (Exception ex)
    {
        return Results.Problem($"DNS failed for host '{b.Host}': {ex.Message}");
    }
});


#region 🌐 Middleware Pipeline Setup 

#region CSP SEcurity

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/swagger"))
    {
        // Swagger UI needs inline script & style
        ctx.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +   // relaxed only for /swagger
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self';";
    }
    else
    {
        // Keep stricter policy for the rest of the app
        ctx.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +                    // no inline scripts outside Swagger
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self';";
    }

    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});



#endregion
AuditLoggingHelper.Configure(app.Services);

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Dev-specific configs
}

app.UseSwagger();

//app.UseSwaggerUI();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "xbytechat.api v1");
    c.RoutePrefix = "swagger";
});
if (!app.Environment.IsDevelopment())
{

    app.UseHttpsRedirection();
    app.UseHsts();
}

// Security headers
//app.Use(async (context, next) =>
//{
//    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
//    context.Response.Headers["X-Frame-Options"] = "DENY";
//    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
//    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
//    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
//    await next();
//});

app.UseRouting();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers()
     .RequireRateLimiting("GlobalLimiter");


//app.MapHub<InboxHub>("/hubs/inbox");
app.MapHub<InboxHub>("/api/hubs/inbox");
// Seed global template library
await TemplateLibrarySeeder.SeedAsync(app.Services);

app.Run();
#endregion






