using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookIdempotencyGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BizProviderMessage",
                table: "MessageLogs");

            migrationBuilder.AddColumn<string>(
                name: "ProviderEventId",
                table: "FlowExecutionLogs",
                type: "text",
                nullable: true);

            // Pre-clean duplicates so unique index creation is safe on existing replayed webhook data.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT ctid,
           ROW_NUMBER() OVER (
               PARTITION BY ""BusinessId"", ""ProviderMessageId""
               ORDER BY ""CreatedAt"" ASC, ""Id"" ASC
           ) AS rn
    FROM ""MessageLogs""
    WHERE ""ProviderMessageId"" IS NOT NULL
)
DELETE FROM ""MessageLogs"" m
USING ranked r
WHERE m.ctid = r.ctid
  AND r.rn > 1;");

            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT ctid,
           ROW_NUMBER() OVER (
               PARTITION BY ""MessageId"", ""Status"", ""MetaTimestamp""
               ORDER BY ""CreatedAt"" ASC, ""Id"" ASC
           ) AS rn
    FROM ""MessageStatusLogs""
    WHERE ""MessageId"" IS NOT NULL
)
DELETE FROM ""MessageStatusLogs"" s
USING ranked r
WHERE s.ctid = r.ctid
  AND r.rn > 1;");

            migrationBuilder.CreateIndex(
                name: "UX_MessageStatusLogs_Message_Status_Timestamp",
                table: "MessageStatusLogs",
                columns: new[] { "MessageId", "Status", "MetaTimestamp" },
                unique: true,
                filter: "\"MessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BizProviderMessage",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ProviderMessageId" },
                unique: true,
                filter: "\"ProviderMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_FlowExecutionLogs_Business_ProviderEventId",
                table: "FlowExecutionLogs",
                columns: new[] { "BusinessId", "ProviderEventId" },
                unique: true,
                filter: "\"ProviderEventId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_MessageStatusLogs_Message_Status_Timestamp",
                table: "MessageStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_BizProviderMessage",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "UX_FlowExecutionLogs_Business_ProviderEventId",
                table: "FlowExecutionLogs");

            migrationBuilder.DropColumn(
                name: "ProviderEventId",
                table: "FlowExecutionLogs");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_BizProviderMessage",
                table: "MessageLogs",
                columns: new[] { "BusinessId", "ProviderMessageId" },
                filter: "\"ProviderMessageId\" IS NOT NULL");
        }
    }
}
