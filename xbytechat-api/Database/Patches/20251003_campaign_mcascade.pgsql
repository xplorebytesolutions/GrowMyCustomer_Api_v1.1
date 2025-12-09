-- 20251003_campaign_cascade.sql
-- Idempotent FK + index patch for Campaign deletion semantics (PostgreSQL)

-- Drop known FK constraints if they exist (safe no-ops if names differ)
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT tc.constraint_name, tc.table_name
        FROM information_schema.table_constraints tc
        WHERE tc.constraint_type = 'FOREIGN KEY'
          AND tc.constraint_name IN (
              'FK_CampaignRecipients_Campaigns_CampaignId',
              'FK_CampaignSendLogs_Campaigns_CampaignId',
              'FK_MessageLogs_Campaigns_CampaignId',
              'FK_MessageStatusLogs_Campaigns_CampaignId',
              'FK_CampaignVariableMaps_Campaigns_CampaignId',
              'FK_CampaignButtons_Campaigns_CampaignId',
              'FK_Audiences_Campaigns_CampaignId',
              'FK_AudiencesMembers_Audiences_AudienceId',
              'FK_TrackingLogs_Campaigns_CampaignId',
              'FK_TrackingLogs_MessageLogs_MessageLogId',
              'FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId'
          )
    LOOP
        EXECUTE format('ALTER TABLE %I DROP CONSTRAINT IF EXISTS %I;', r.table_name, r.constraint_name);
    END LOOP;
END$$;

-- Re-add with desired behaviors

-- Campaign → Children (CASCADE)
ALTER TABLE "CampaignRecipients"
    ADD CONSTRAINT "FK_CampaignRecipients_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

ALTER TABLE "CampaignSendLogs"
    ADD CONSTRAINT "FK_CampaignSendLogs_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

ALTER TABLE "MessageLogs"
    ADD CONSTRAINT "FK_MessageLogs_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

ALTER TABLE "MessageStatusLogs"
    ADD CONSTRAINT "FK_MessageStatusLogs_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

ALTER TABLE "CampaignVariableMaps"
    ADD CONSTRAINT "FK_CampaignVariableMaps_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

ALTER TABLE "CampaignButtons"
    ADD CONSTRAINT "FK_CampaignButtons_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

ALTER TABLE "Audiences"
    ADD CONSTRAINT "FK_Audiences_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE;

-- Audience → Members (CASCADE)
ALTER TABLE "AudiencesMembers"
    ADD CONSTRAINT "FK_AudiencesMembers_Audiences_AudienceId"
    FOREIGN KEY ("AudienceId") REFERENCES "Audiences" ("Id") ON DELETE CASCADE;

-- Tracking stays RESTRICT (we delete these first in service to avoid multi-cascade-paths)
ALTER TABLE "
"
    ADD CONSTRAINT "FK_TrackingLogs_Campaigns_CampaignId"
    FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE RESTRICT;

ALTER TABLE "TrackingLogs"
    ADD CONSTRAINT "FK_TrackingLogs_MessageLogs_MessageLogId"
    FOREIGN KEY ("MessageLogId") REFERENCES "MessageLogs" ("Id") ON DELETE RESTRICT;

ALTER TABLE "TrackingLogs"
    ADD CONSTRAINT "FK_TrackingLogs_CampaignSendLogs_CampaignSendLogId"
    FOREIGN KEY ("CampaignSendLogId") REFERENCES "CampaignSendLogs" ("Id") ON DELETE RESTRICT;

-- Helpful FK indexes (idempotent)
CREATE INDEX IF NOT EXISTS "IX_CampaignSendLogs_Campaign" ON "CampaignSendLogs" ("CampaignId");
CREATE INDEX IF NOT EXISTS "IX_MessageLogs_Campaign"      ON "MessageLogs"      ("CampaignId");
CREATE INDEX IF NOT EXISTS "IX_Audiences_Campaign"        ON "Audiences"        ("CampaignId");
CREATE INDEX IF NOT EXISTS "IX_TrackingLogs_Campaign"     ON "TrackingLogs"     ("CampaignId");
CREATE INDEX IF NOT EXISTS "IX_TrackingLogs_MessageLog"   ON "TrackingLogs"     ("MessageLogId");
CREATE INDEX IF NOT EXISTS "IX_TrackingLogs_SendLog"      ON "TrackingLogs"     ("CampaignSendLogId");
