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
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Audiences.Services
{
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

                // ✅ Normalize name (trim + lower for comparison)
                var name = (dto?.Name?.Trim() ?? "Untitled Audience").Trim();
                var nameKey = name.ToLowerInvariant();

                // ✅ Enforce uniqueness at service level (active audiences only)
                var exists = await _db.Set<Audience>()
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.BusinessId == businessId &&
                        !a.IsDeleted &&
                        a.Name.ToLower() == nameKey);

                if (exists)
                {
                    // Prefer returning a domain exception that your middleware maps to 409.
                    // If you don't have that yet, throwing is still better than duplicating silently.
                    throw new InvalidOperationException($"Audience name '{name}' already exists.");
                }

                var model = new Audience
                {
                    Id = id,
                    BusinessId = businessId,
                    Name = name,
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

                // ✅ Load existing members for this audience to support:
                // - skip duplicates
                // - reactivate soft-deleted members
                var existing = await _db.Set<AudienceMember>()
                    .Where(m => m.BusinessId == businessId && m.AudienceId == audienceId)
                    .ToListAsync();

                // Map by normalized phone (canonical identity)
                var byPhone = existing
                    .Where(m => !string.IsNullOrWhiteSpace(m.PhoneE164))
                    .GroupBy(m => m.PhoneE164)
                    .ToDictionary(g => g.Key, g => g.First());

                var toAdd = new List<AudienceMember>();

                foreach (var c in contacts)
                {
                    var phoneRaw = (c.PhoneNumber ?? "").Trim();

                    // ✅ Canonical digits-only E.164 (NO '+'), same as CRM
                    var phoneE164 = PhoneNumberNormalizer.NormalizeToE164Digits(phoneRaw, "IN");

                    // If phone can't normalize, skip creating identity records
                    if (string.IsNullOrWhiteSpace(phoneE164))
                        continue;

                    if (byPhone.TryGetValue(phoneE164, out var existingMember))
                    {
                        // ✅ Reactivate if it was soft-deleted
                        if (existingMember.IsDeleted)
                        {
                            existingMember.IsDeleted = false;
                            existingMember.UpdatedAt = now;
                        }

                        // ✅ Refresh basic fields (optional but usually correct)
                        existingMember.ContactId = c.Id;
                        existingMember.Name = c.Name;
                        existingMember.Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email;
                        existingMember.PhoneRaw = phoneRaw;

                        continue; // no new insert
                    }

                    // ✅ Add new member
                    var member = new AudienceMember
                    {
                        Id = Guid.NewGuid(),
                        AudienceId = audienceId,
                        BusinessId = businessId,
                        ContactId = c.Id,
                        Name = c.Name,
                        Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email,
                        PhoneRaw = phoneRaw,
                        PhoneE164 = phoneE164,
                        AttributesJson = null,
                        IsTransientContact = false,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    toAdd.Add(member);
                    byPhone[phoneE164] = member; // prevent duplicates inside same request too
                }

                if (toAdd.Count > 0)
                    await _db.Set<AudienceMember>().AddRangeAsync(toAdd);
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
    }
}
