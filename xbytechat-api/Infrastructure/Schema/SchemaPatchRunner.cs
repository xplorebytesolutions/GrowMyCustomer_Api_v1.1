using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace xbytechat.api.Infrastructure.Schema
{
    public static class SchemaPatchRunner
    {
        /// <summary>
        /// Ensures a patch is applied exactly once. Creates the tracking table if needed.
        /// </summary>
        /// <param name="db">AppDbContext</param>
        /// <param name="patchId">A unique string, e.g. "20251003_campaign_cascade"</param>
        /// <param name="absoluteSqlPath">Absolute path to the .sql file</param>
        public static async Task EnsurePatchAsync(DbContext db, string patchId, string absoluteSqlPath)
        {
            if (!File.Exists(absoluteSqlPath))
                throw new FileNotFoundException($"Schema patch not found: {absoluteSqlPath}");

            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                // 1) Create tracking table if not exists
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS __schema_patches (
    patch_id   text PRIMARY KEY,
    applied_at timestamp with time zone NOT NULL DEFAULT now()
);";
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2) Check if already applied
                bool alreadyApplied;
                await using (var check = conn.CreateCommand())
                {
                    check.Transaction = tx;
                    check.CommandText = "SELECT 1 FROM __schema_patches WHERE patch_id = @p LIMIT 1;";
                    var p = check.CreateParameter();
                    p.ParameterName = "@p";
                    p.Value = patchId;
                    check.Parameters.Add(p);

                    await using var reader = await check.ExecuteReaderAsync();
                    alreadyApplied = await reader.ReadAsync();
                }

                if (!alreadyApplied)
                {
                    // 3) Run SQL file
                    var sql = await File.ReadAllTextAsync(absoluteSqlPath);

                    await using (var exec = conn.CreateCommand())
                    {
                        exec.Transaction = tx;
                        exec.CommandText = sql;
                        await exec.ExecuteNonQueryAsync();
                    }

                    // 4) Record success
                    await using (var ins = conn.CreateCommand())
                    {
                        ins.Transaction = tx;
                        ins.CommandText = "INSERT INTO __schema_patches (patch_id) VALUES (@p);";
                        var p = ins.CreateParameter();
                        p.ParameterName = "@p";
                        p.Value = patchId;
                        ins.Parameters.Add(p);
                        await ins.ExecuteNonQueryAsync();
                    }
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}
