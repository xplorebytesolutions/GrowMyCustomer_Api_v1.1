using Microsoft.EntityFrameworkCore.Migrations;

namespace xbytechat.api.Migrations
{
    public partial class IdempotencyKeyNullable_FilteredUq : Migration
    {
        //protected override void Up(MigrationBuilder migrationBuilder)
        //{
        //    // Drop legacy indexes if any (names may differ in your DB, keep the try/catch)
        //    try { migrationBuilder.DropIndex("IX_OutboundMessageJobs_IdempotencyKey", "OutboundMessageJobs"); } catch { }
        //    try { migrationBuilder.DropIndex("UX_Outbox_Biz_IdemKey", "OutboundMessageJobs"); } catch { }

        //    // Make column nullable (drop any implicit default)
        //    migrationBuilder.AlterColumn<string>(
        //        name: "IdempotencyKey",
        //        table: "OutboundMessageJobs",
        //        type: "text",
        //        nullable: true,
        //        oldClrType: typeof(string),
        //        oldType: "text");

        //    // Normalize legacy empty strings to NULL so they don't trip the filtered unique
        //    migrationBuilder.Sql("""
        //        UPDATE "OutboundMessageJobs"
        //        SET "IdempotencyKey" = NULL
        //        WHERE "IdempotencyKey" IS NOT NULL AND btrim("IdempotencyKey") = '';
        //    """);

        //    // Recreate your filtered unique (per BusinessId) exactly as in your model
        //    migrationBuilder.CreateIndex(
        //        name: "UX_Outbox_Biz_IdemKey",
        //        table: "OutboundMessageJobs",
        //        columns: new[] { "BusinessId", "IdempotencyKey" },
        //        unique: true,
        //        filter: "\"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''");

        //    // Safety: hot-path indexes (no-op if already exist)
        //    migrationBuilder.CreateIndex(
        //        name: "IX_Outbox_StatusDueCreated",
        //        table: "OutboundMessageJobs",
        //        columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });
        //}

        //protected override void Down(MigrationBuilder migrationBuilder)
        //{
        //    migrationBuilder.DropIndex("UX_Outbox_Biz_IdemKey", "OutboundMessageJobs");

        //    migrationBuilder.AlterColumn<string>(
        //        name: "IdempotencyKey",
        //        table: "OutboundMessageJobs",
        //        type: "text",
        //        nullable: false,
        //        defaultValue: "");

        //    migrationBuilder.CreateIndex(
        //        name: "IX_OutboundMessageJobs_IdempotencyKey",
        //        table: "OutboundMessageJobs",
        //        column: "IdempotencyKey",
        //        unique: true);
        //}
    }
}