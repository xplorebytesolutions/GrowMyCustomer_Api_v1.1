using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace xbytechat.api.Migrations
{
    /// <inheritdoc />
    public partial class AccessControl_Permission_Dedupe_Once : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Normalize codes to UPPER/TRIM
            migrationBuilder.Sql(@"
        UPDATE ""Permissions""
        SET ""Code"" = UPPER(TRIM(""Code""))
        WHERE ""Code"" IS NOT NULL;
    ");

            // 2) Deactivate later duplicates (keep oldest by CreatedAt/Id)
            migrationBuilder.Sql(@"
        WITH ranked AS (
            SELECT ""Id"", ""Code"",
                   ROW_NUMBER() OVER (PARTITION BY ""Code"" ORDER BY COALESCE(""CreatedAt"", '2000-01-01'), ""Id"") AS rn
            FROM ""Permissions""
            WHERE ""IsActive"" = TRUE
        )
        UPDATE ""Permissions"" p
        SET ""IsActive"" = FALSE,
            ""Description"" = COALESCE(p.""Description"", '') || ' [AUTO-DEACTIVATED: DUPLICATE CODE]'
        FROM ranked r
        WHERE p.""Id"" = r.""Id"" AND r.rn > 1;
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
