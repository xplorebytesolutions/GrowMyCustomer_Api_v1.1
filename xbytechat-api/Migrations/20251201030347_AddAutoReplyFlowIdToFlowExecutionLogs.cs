using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoReplyFlowIdToFlowExecutionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutoReplyFlowId",
                table: "FlowExecutionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "FlowExecutionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "FlowExecutionLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_flowexec_biz_executedat",
                table: "FlowExecutionLogs",
                columns: new[] { "BusinessId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_flowexec_biz_origin_autoreply",
                table: "FlowExecutionLogs",
                columns: new[] { "BusinessId", "Origin", "AutoReplyFlowId" });

            migrationBuilder.CreateIndex(
                name: "ix_flowexec_biz_origin_campaign",
                table: "FlowExecutionLogs",
                columns: new[] { "BusinessId", "Origin", "CampaignId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flowexec_biz_executedat",
                table: "FlowExecutionLogs");

            migrationBuilder.DropIndex(
                name: "ix_flowexec_biz_origin_autoreply",
                table: "FlowExecutionLogs");

            migrationBuilder.DropIndex(
                name: "ix_flowexec_biz_origin_campaign",
                table: "FlowExecutionLogs");

            migrationBuilder.DropColumn(
                name: "AutoReplyFlowId",
                table: "FlowExecutionLogs");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "FlowExecutionLogs");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "FlowExecutionLogs");
        }
    }
}
