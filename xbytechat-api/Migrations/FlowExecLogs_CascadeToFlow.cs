using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class FlowExecLogs_CascadeToFlow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_FlowExecutionLogs_FlowId""
ON ""FlowExecutionLogs"" (""FlowId"");
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE constraint_type='FOREIGN KEY'
      AND table_name='FlowExecutionLogs'
      AND constraint_name='FK_FlowExecutionLogs_CTAFlowConfigs_FlowId'
  ) THEN
    ALTER TABLE ""FlowExecutionLogs""
      DROP CONSTRAINT ""FK_FlowExecutionLogs_CTAFlowConfigs_FlowId"";
  END IF;
END$$;
");

        migrationBuilder.Sql(@"
ALTER TABLE ""FlowExecutionLogs""
ADD CONSTRAINT ""FK_FlowExecutionLogs_CTAFlowConfigs_FlowId""
FOREIGN KEY (""FlowId"") REFERENCES ""CTAFlowConfigs""(""Id"")
ON DELETE CASCADE;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE constraint_type='FOREIGN KEY'
      AND table_name='FlowExecutionLogs'
      AND constraint_name='FK_FlowExecutionLogs_CTAFlowConfigs_FlowId'
  ) THEN
    ALTER TABLE ""FlowExecutionLogs""
      DROP CONSTRAINT ""FK_FlowExecutionLogs_CTAFlowConfigs_FlowId"";
  END IF;
END$$;
");

        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_FlowExecutionLogs_FlowId"";");
    }
}
