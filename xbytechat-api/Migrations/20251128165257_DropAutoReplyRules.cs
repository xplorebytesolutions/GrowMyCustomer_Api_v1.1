using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class DropAutoReplyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoReplyRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoReplyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FlowName = table.Column<string>(type: "text", nullable: true),
                    IndustryTag = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ReplyMessage = table.Column<string>(type: "text", nullable: false),
                    SourceChannel = table.Column<string>(type: "text", nullable: true),
                    TriggerKeyword = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyRules_FlowId",
                table: "AutoReplyRules",
                column: "FlowId");
        }
    }
}
