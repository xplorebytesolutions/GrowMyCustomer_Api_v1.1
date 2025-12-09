using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class FixNavMappings2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }


     
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CsvBatches_Audiences_AudienceId",
                table: "CsvBatches");

            migrationBuilder.DropTable(
                name: "ApiKeys");

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
                name: "ContactJourneyStates");

            migrationBuilder.DropTable(
                name: "ContactReads");

            migrationBuilder.DropTable(
                name: "ContactTags");

            migrationBuilder.DropTable(
                name: "CsvRows");

            migrationBuilder.DropTable(
                name: "CustomerWebhookConfigs");

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
