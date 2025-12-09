using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class Payment_InvoicePlanLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PlanId",
                table: "Invoices",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Plans_PlanId",
                table: "Invoices",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Plans_PlanId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PlanId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "Invoices");
        }
    }
}
