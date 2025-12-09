using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_WhatsAppSettings_Business_Provider_IsActive",
                table: "WhatsAppSettings",
                newName: "IX_WhatsAppSettings_BizProviderActive");

            migrationBuilder.RenameIndex(
                name: "UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId",
                table: "WhatsAppPhoneNumbers",
                newName: "IX_WhatsAppPhoneNumbers_BizProviderPhone");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignSendLogs_Business_MessageId",
                table: "CampaignSendLogs",
                newName: "IX_CampaignSendLogs_BizMessage");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_BizConversation",
                table: "ProviderBillingEvents",
                columns: new[] { "BusinessId", "ConversationId" },
                filter: "\"ConversationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Billing_BizEventTime",
                table: "ProviderBillingEvents",
                columns: new[] { "BusinessId", "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Billing_BizProviderMessage",
                table: "ProviderBillingEvents",
                columns: new[] { "BusinessId", "ProviderMessageId" },
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProviderBillingEvents_UniqueEvent",
                table: "ProviderBillingEvents",
                columns: new[] { "BusinessId", "Provider", "ProviderMessageId", "EventType" },
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BizConversation",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ConversationId" },
                filter: "\"ConversationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BizCreatedAt",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BizProviderMessage",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ProviderMessageId" },
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignSendLogs_StatusTime",
                table: "CampaignSendLogs",
                columns: new[] { "BusinessId", "SendStatus", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Billing_BizConversation",
                table: "ProviderBillingEvents");

            migrationBuilder.DropIndex(
                name: "IX_Billing_BizEventTime",
                table: "ProviderBillingEvents");

            migrationBuilder.DropIndex(
                name: "IX_Billing_BizProviderMessage",
                table: "ProviderBillingEvents");

            migrationBuilder.DropIndex(
                name: "UX_ProviderBillingEvents_UniqueEvent",
                table: "ProviderBillingEvents");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BizConversation",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BizCreatedAt",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BizProviderMessage",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_CampaignSendLogs_StatusTime",
                table: "CampaignSendLogs");

            migrationBuilder.RenameIndex(
                name: "IX_WhatsAppSettings_BizProviderActive",
                table: "WhatsAppSettings",
                newName: "IX_WhatsAppSettings_Business_Provider_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_WhatsAppPhoneNumbers_BizProviderPhone",
                table: "WhatsAppPhoneNumbers",
                newName: "UX_WhatsappPhoneNumbers_Bus_Prov_PhoneId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignSendLogs_BizMessage",
                table: "CampaignSendLogs",
                newName: "IX_CampaignSendLogs_Business_MessageId");
        }
    }
}
