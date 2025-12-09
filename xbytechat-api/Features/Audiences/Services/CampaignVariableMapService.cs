using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;

namespace xbytechat.api.Features.CampaignModule.Services
{
    public interface ICampaignVariableMapService
    {
        Task<bool> SaveAsync(Guid businessId, CampaignVariableMapDto dto, string updatedBy);
        Task<CampaignVariableMapDto?> GetAsync(Guid businessId, Guid campaignId);
    }

    public class CampaignVariableMapService : ICampaignVariableMapService
    {
        private readonly AppDbContext _db;

        public CampaignVariableMapService(AppDbContext db) { _db = db; }

        public async Task<bool> SaveAsync(Guid businessId, CampaignVariableMapDto dto, string updatedBy)
        {
            try
            {
                if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.CampaignId == Guid.Empty) throw new ArgumentException("CampaignId is required.", nameof(dto));

                // Ensure campaign ownership
                var owns = await _db.Campaigns
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == dto.CampaignId && c.BusinessId == businessId);
                if (!owns) return false;

                // Load existing rows for this campaign
                var existing = await _db.CampaignVariableMaps
                    .Where(m => m.BusinessId == businessId && m.CampaignId == dto.CampaignId)
                    .ToListAsync();

                // Normalize incoming:
                // - tolerate null Items
                // - default Component → "BODY" if missing
                // - trim strings
                // - keep last occurrence per (Component, Index)
                var incoming = (dto.Items?.AsEnumerable() ?? Enumerable.Empty<CampaignVariableMapItemDto>())
                    .Where(i => i != null && i.Index >= 1)
                    .Select(i => new
                    {
                        Component = string.IsNullOrWhiteSpace(i.Component) ? "BODY" : i.Component!.Trim(),
                        i.Index,
                        SourceType = string.IsNullOrWhiteSpace(i.SourceType) ? "Static" : i.SourceType!.Trim(),
                        SourceKey = string.IsNullOrWhiteSpace(i.SourceKey) ? null : i.SourceKey!.Trim(),
                        StaticValue = i.StaticValue,
                        Expression = i.Expression,
                        DefaultValue = i.DefaultValue,
                        i.IsRequired
                    })
                    .GroupBy(x => new { x.Component, x.Index })
                    .Select(g => g.Last())
                    .ToList();

                var incomingKeySet = incoming
                    .Select(i => (i.Component, i.Index))
                    .ToHashSet();

                // Upsert each incoming row
                foreach (var item in incoming)
                {
                    var row = existing.FirstOrDefault(x => x.Component == item.Component && x.Index == item.Index);
                    if (row == null)
                    {
                        row = new CampaignVariableMap
                        {
                            Id = Guid.NewGuid(),
                            BusinessId = businessId,
                            CampaignId = dto.CampaignId,
                            Component = item.Component,
                            Index = item.Index
                        };
                        _db.CampaignVariableMaps.Add(row);
                        existing.Add(row); // keep local cache in sync in case of duplicates
                    }

                    row.SourceType = item.SourceType;
                    row.SourceKey = item.SourceKey;
                    row.StaticValue = item.StaticValue;
                    row.Expression = item.Expression;
                    row.DefaultValue = item.DefaultValue;
                    row.IsRequired = item.IsRequired;
                }

                // Remove deleted mappings (anything not present in incoming)
                var toRemove = existing.Where(x => !incomingKeySet.Contains((x.Component, x.Index))).ToList();
                if (toRemove.Count > 0)
                    _db.CampaignVariableMaps.RemoveRange(toRemove);

                await _db.SaveChangesAsync();

                var upserted = incoming.Count;
                var removed = toRemove.Count;

                Log.Information("✅ Variable map saved | biz={Biz} campaign={Campaign} upserted={Up} removed={Rm}",
                    businessId, dto.CampaignId, upserted, removed);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed saving campaign variable map | biz={Biz} campaign={Campaign}", businessId, dto?.CampaignId);
                throw;
            }
        }
        public async Task<CampaignVariableMapDto?> GetAsync(Guid businessId, Guid campaignId)
        {
            var rows = await _db.CampaignVariableMaps
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.CampaignId == campaignId)
                .OrderBy(m => m.Component).ThenBy(m => m.Index)
                .ToListAsync();

            var items = rows.Select(r => new CampaignVariableMapItemDto
            {
                Component = r.Component,
                Index = r.Index,
                SourceType = r.SourceType,
                SourceKey = r.SourceKey,
                StaticValue = r.StaticValue,
                Expression = r.Expression,
                DefaultValue = r.DefaultValue,
                IsRequired = r.IsRequired
            }).ToList();

            return new CampaignVariableMapDto
            {
                CampaignId = campaignId,
                Items = items
            };
        }
    }
}
