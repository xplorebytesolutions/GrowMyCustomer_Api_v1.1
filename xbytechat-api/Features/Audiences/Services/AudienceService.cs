using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.Audiences.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.api.Features.Audiences.Services
{
    public interface IAudienceService
    {
        Task<Guid> CreateAsync(Guid businessId, AudienceCreateDto dto, string createdBy);
        Task<List<AudienceSummaryDto>> ListAsync(Guid businessId);
        Task<bool> AssignAsync(Guid businessId, Guid audienceId, AudienceAssignDto dto, string createdBy);
        Task<List<AudienceMemberDto>> GetMembersAsync(Guid businessId, Guid audienceId, int page = 1, int pageSize = 50);
    }

    public class AudienceService : IAudienceService
    {
        private readonly AppDbContext _db;

        public AudienceService(AppDbContext db) { _db = db; }

        public async Task<Guid> CreateAsync(Guid businessId, AudienceCreateDto dto, string createdBy)
        {
            var id = Guid.NewGuid();
            try
            {
                var now = DateTime.UtcNow;
                Guid? createdByUserId = null;
                if (Guid.TryParse(createdBy, out var parsed)) createdByUserId = parsed;

                var model = new Audience
                {
                    Id = id,
                    BusinessId = businessId,
                    Name = dto?.Name?.Trim() ?? "Untitled Audience",
                    Description = dto?.Description,
                    CsvBatchId = null,
                    IsDeleted = false,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _db.Set<Audience>().Add(model);
                await _db.SaveChangesAsync();

                Log.Information("✅ Audience created | biz={Biz} id={Id} name={Name}", businessId, id, model.Name);
                return id;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed creating audience | biz={Biz}", businessId);
                throw;
            }
        }

        public async Task<List<AudienceSummaryDto>> ListAsync(Guid businessId)
        {
            var audiences = _db.Set<Audience>()
                .AsNoTracking()
                .Where(a => a.BusinessId == businessId && !a.IsDeleted);

            var members = _db.Set<AudienceMember>();

            var items = await audiences
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AudienceSummaryDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    MemberCount = members.Count(m => m.BusinessId == businessId && m.AudienceId == a.Id && !m.IsDeleted),
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return items;
        }

        public async Task<bool> AssignAsync(Guid businessId, Guid audienceId, AudienceAssignDto dto, string createdBy)
        {
            var audience = await _db.Set<Audience>()
                .FirstOrDefaultAsync(a => a.Id == audienceId && a.BusinessId == businessId && !a.IsDeleted);

            if (audience == null) return false;

            var now = DateTime.UtcNow;

            // 1) Assign CRM contacts (if provided)
            if (dto?.ContactIds != null && dto.ContactIds.Count > 0)
            {
                var contacts = await _db.Set<Contact>()
                    .Where(c => c.BusinessId == businessId && dto.ContactIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name, c.PhoneNumber, c.Email })
                    .ToListAsync();

                var newMembers = contacts.Select(c =>
                {
                    var phoneRaw = (c.PhoneNumber ?? "").Trim();
                    var phoneE164 = ToE164OrNull(phoneRaw);

                    return new AudienceMember
                    {
                        Id = Guid.NewGuid(),
                        AudienceId = audienceId,
                        BusinessId = businessId,
                        ContactId = c.Id,
                        Name = c.Name,
                        Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email,
                        PhoneRaw = phoneRaw,
                        PhoneE164 = phoneE164,
                        AttributesJson = null,            // keep as null unless you want to pack extra vars
                        IsTransientContact = false,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                });

                await _db.Set<AudienceMember>().AddRangeAsync(newMembers);
            }

            // 2) Optionally link a CSV batch
            if (dto?.CsvBatchId.HasValue == true && dto.CsvBatchId.Value != Guid.Empty)
            {
                var batch = await _db.Set<CsvBatch>()
                    .FirstOrDefaultAsync(b => b.Id == dto.CsvBatchId.Value && b.BusinessId == businessId);

                if (batch != null)
                {
                    audience.CsvBatchId = batch.Id;
                }
            }

            audience.UpdatedAt = now;

            await _db.SaveChangesAsync();

            Log.Information("👥 Audience assigned | biz={Biz} audience={AudienceId} contacts={Contacts} batch={Batch}",
                businessId, audienceId, dto?.ContactIds?.Count ?? 0, dto?.CsvBatchId);

            return true;
        }

        public async Task<List<AudienceMemberDto>> GetMembersAsync(Guid businessId, Guid audienceId, int page = 1, int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Clamp(pageSize, 10, 200);

            var q = _db.Set<AudienceMember>()
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.AudienceId == audienceId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt);

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new AudienceMemberDto
                {
                    Id = m.Id,
                    ContactId = m.ContactId,
                    Name = m.Name,
                    PhoneNumber = string.IsNullOrWhiteSpace(m.PhoneE164) ? m.PhoneRaw : m.PhoneE164,
                    Email = m.Email,
                    VariablesJson = m.AttributesJson,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return items;
        }

        // ---- helpers ----

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string? ToE164OrNull(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digits)) return null;

            // naive normalization: ensure leading +
            if (raw.Trim().StartsWith("+")) return "+" + digits;
            return "+" + digits;
        }
    }
}
