using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.Features.Catalog.Models;
using static xbytechat.api.Features.BusinessModule.Models.Business;
using System.Text.Json;
using xbytechat.api.Helpers;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Features.CRM.Timelines.DTOs;
using xbytechat.api.Features.CRM.Timelines.Models;


namespace xbytechat.api.Features.CRM.Timelines.Services
{
    public class LeadTimelineService : ILeadTimelineService
    {
        private readonly AppDbContext _context;


        public LeadTimelineService(AppDbContext context)
        {
            _context = context;

        }

        public async Task<LeadTimeline> AddTimelineEntryAsync(LeadTimelineDto dto)
        {
            try
            {
                var entry = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    EventType = dto.EventType,
                    Description = dto.Description,
                    Data = dto.Data,
                    ReferenceId = dto.ReferenceId,
                    IsSystemGenerated = dto.IsSystemGenerated,
                    CreatedBy = dto.CreatedBy,
                    Source = dto.Source,
                    Category = dto.Category,
                    CreatedAt = DateTime.UtcNow
                };

                _context.LeadTimelines.Add(entry);
                await _context.SaveChangesAsync();

                Log.Information("✅ Timeline entry added for ContactId: {ContactId}", dto.ContactId);

                return entry;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error adding timeline entry for ContactId: {ContactId}", dto.ContactId);
                throw; // Let global middleware handle this
            }
        }

        public async Task<List<LeadTimeline>> GetTimelineByContactIdAsync(Guid contactId)
        {
            try
            {
                var results = await _context.LeadTimelines
                    .Where(x => x.ContactId == contactId)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();

                Log.Information("📄 Fetched {Count} timeline entries for ContactId: {ContactId}", results.Count, contactId);

                return results;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to fetch timeline for ContactId: {ContactId}", contactId);
                throw;
            }
        }

        public async Task<List<LeadTimelineDto>> GetAllTimelinesAsync()
        {
            try
            {
                var entries = await _context.LeadTimelines
                    .Include(t => t.Contact) // for Contact Name/Phone
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();

                var dtoList = entries.Select(entry => new LeadTimelineDto
                {
                    ContactId = entry.ContactId,
                    EventType = entry.EventType,
                    Description = entry.Description,
                    Data = entry.Data,
                    ReferenceId = entry.ReferenceId,
                    CreatedAt = entry.CreatedAt,
                    CreatedBy = entry.CreatedBy,
                    Source = entry.Source,
                    Category = entry.Category,
                    IsSystemGenerated = entry.IsSystemGenerated,
                    ContactName = entry.Contact?.Name,
                    ContactNumber = entry.Contact?.PhoneNumber
                }).ToList();

                Log.Information("📄 Loaded {Count} total timeline entries", dtoList.Count);
                return dtoList;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to fetch all timeline entries");
                throw;
            }
        }
        public async Task AddFromCatalogClickAsync(CatalogClickLog log)
        {
            if (log == null)
            {
                Log.Warning("CatalogClickLog is null. Skipping timeline creation.");
                return;
            }

            try
            {
                var business = await _context.Businesses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == log.BusinessId);

                if (business == null)
                {
                    Log.Warning("Business not found for ID: {BusinessId}. Skipping timeline creation.", log.BusinessId);
                    return;
                }

                // if (business.Plan == PlanType.Advanced)
                if (business?.BusinessPlanInfo?.Plan == PlanType.Advanced)
                {
                    Log.Information("Timeline skipped for Basic Plan - BusinessId: {BusinessId}", business.Id);
                    return;
                }


                var description = $"{log.ProductBrowsed} | {log.CTAJourney}";

                var timelineEntry = new LeadTimeline
                {
                    BusinessId = log.BusinessId,
                    ContactId = log.ContactId ?? Guid.Empty,
                    EventType = "CatalogCTA",
                    Description = description,
                    Data = JsonSerializer.Serialize(log),
                    ReferenceId = null,
                    CreatedBy = "system",
                    IsSystemGenerated = true,
                    Source = "Catalog",
                    Category = log.CategoryBrowsed,
                    CreatedAt = DateTime.UtcNow
                };

                _context.LeadTimelines.Add(timelineEntry);
                await _context.SaveChangesAsync();

                Log.Information("📈 Timeline entry created from CatalogClick for UserId: {UserId}", log.UserId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Error creating timeline entry from CatalogClick for UserId: {UserId}", log.UserId);
                // Safe swallow
            }
        }

        public async Task<ResponseResult> LogCampaignSendAsync(CampaignTimelineLogDto dto)
        {
            try
            {
                var timeline = new LeadTimeline
                {
                    ContactId = dto.ContactId,
                    BusinessId = dto.BusinessId,
                    EventType = "CampaignSend",
                    Description = $"Campaign '{dto.CampaignName}' was sent.", // ✅ Timeline me readable text
                    ReferenceId = dto.CampaignId, // ✅ Linking to campaign record
                    IsSystemGenerated = false,    // ✅ Default (campaign sending is manual action)
                    CreatedBy = "system",         // ✅ Or actual user email if needed later
                    Source = "CampaignModule",    // ✅ Source field for clarity
                    Category = "Messaging",       // ✅ Logical grouping
                    CreatedAt = dto.Timestamp ?? DateTime.UtcNow // ✅ Use given Timestamp or fallback to now
                };

                _context.LeadTimelines.Add(timeline);
                await _context.SaveChangesAsync();

                return ResponseResult.SuccessInfo("✅ Campaign send event logged into timeline.");
            }
            catch (Exception ex)
            {
                return ResponseResult.ErrorInfo("❌ Failed to log campaign send event: " + ex.Message);
            }
        }


    }


}

