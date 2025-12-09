using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AutoReplyFlows_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AutoReplyFlowNodes_AutoReplyFlows_FlowId",
                table: "AutoReplyFlowNodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AutoReplyFlowNodes",
                table: "AutoReplyFlowNodes");

            migrationBuilder.DropIndex(
                name: "IX_AutoReplyFlowNodes_FlowId",
                table: "AutoReplyFlowNodes");

            migrationBuilder.RenameTable(
                name: "AutoReplyFlowNodes",
                newName: "AutoReplyFlowNode");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AutoReplyFlowNode",
                table: "AutoReplyFlowNode",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyFlows_BusinessId_Name",
                table: "AutoReplyFlows",
                columns: new[] { "BusinessId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyFlowNode_FlowId_NodeName",
                table: "AutoReplyFlowNode",
                columns: new[] { "FlowId", "NodeName" });

            migrationBuilder.AddForeignKey(
                name: "FK_AutoReplyFlowNode_AutoReplyFlows_FlowId",
                table: "AutoReplyFlowNode",
                column: "FlowId",
                principalTable: "AutoReplyFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AutoReplyFlowNode_AutoReplyFlows_FlowId",
                table: "AutoReplyFlowNode");

            migrationBuilder.DropIndex(
                name: "IX_AutoReplyFlows_BusinessId_Name",
                table: "AutoReplyFlows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AutoReplyFlowNode",
                table: "AutoReplyFlowNode");

            migrationBuilder.DropIndex(
                name: "IX_AutoReplyFlowNode_FlowId_NodeName",
                table: "AutoReplyFlowNode");

            migrationBuilder.RenameTable(
                name: "AutoReplyFlowNode",
                newName: "AutoReplyFlowNodes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AutoReplyFlowNodes",
                table: "AutoReplyFlowNodes",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AutoReplyFlowNodes_FlowId",
                table: "AutoReplyFlowNodes",
                column: "FlowId");

            migrationBuilder.AddForeignKey(
                name: "FK_AutoReplyFlowNodes_AutoReplyFlows_FlowId",
                table: "AutoReplyFlowNodes",
                column: "FlowId",
                principalTable: "AutoReplyFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
