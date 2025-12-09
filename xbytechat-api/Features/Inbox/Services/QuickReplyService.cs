using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api.Features.Inbox.DTOs;
using xbytechat.api.Features.Inbox.Models;
using xbytechat.api.Helpers;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.Inbox.Services
{
    public class QuickReplyService : IQuickReplyService
    {
        private readonly AppDbContext _db;

        public QuickReplyService(AppDbContext db) => _db = db;

        public async Task<List<QuickReplyDto>> GetAllAsync(Guid businessId, Guid userId,
            string? search = null, bool includeBusiness = true, bool includePersonal = true)
        {
            var q = _db.Set<QuickReply>()
                .AsNoTracking()
                .Where(qr => qr.BusinessId == businessId && !qr.IsDeleted && qr.IsActive);

            if (!includeBusiness) q = q.Where(x => x.Scope == QuickReplyScope.Personal);
            if (!includePersonal) q = q.Where(x => x.Scope == QuickReplyScope.Business);
            if (includePersonal && includeBusiness == false)
                q = q.Where(x => x.OwnerUserId == userId || x.Scope == QuickReplyScope.Business);
            else if (includePersonal)
                q = q.Where(x => x.Scope == QuickReplyScope.Business || x.OwnerUserId == userId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(x =>
                    x.Title.ToLower().Contains(s) ||
                    x.Body.ToLower().Contains(s) ||
                    (x.TagsCsv != null && x.TagsCsv.ToLower().Contains(s)));
            }

            return await q
                .OrderByDescending(x => x.Scope)
                .ThenBy(x => x.Title)
                .Select(x => new QuickReplyDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    OwnerUserId = x.OwnerUserId,
                    Scope = x.Scope,
                    Title = x.Title,
                    Body = x.Body,
                    TagsCsv = x.TagsCsv,
                    Language = x.Language,
                    IsActive = x.IsActive,
                    UpdatedAt = x.UpdatedAt
                }).ToListAsync();
        }

        public async Task<ResponseResult> CreateAsync(Guid businessId, Guid userId, string actor, QuickReplyCreateDto dto)
        {
            try
            {
                var entity = new QuickReply
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    OwnerUserId = dto.Scope == QuickReplyScope.Personal ? userId : null,
                    Scope = dto.Scope,
                    Title = dto.Title.Trim(),
                    Body = dto.Body,
                    TagsCsv = dto.TagsCsv,
                    Language = dto.Language,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = actor,
                    UpdatedBy = actor
                };

                _db.Add(entity);
                await _db.SaveChangesAsync();

                Log.Information("QuickReply created {@QuickReplyId} for business {@BusinessId} by {@Actor}",
                    entity.Id, businessId, actor);

                return ResponseResult.SuccessInfo("✅ Quick reply created.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating quick reply for business {BusinessId}", businessId);
                return ResponseResult.ErrorInfo("❌ Failed to create quick reply.", ex.ToString()); // pattern like Campaign. :contentReference[oaicite:3]{index=3}
            }
        }

        public async Task<ResponseResult> UpdateAsync(Guid businessId, Guid userId, string actor, Guid id, QuickReplyUpdateDto dto)
        {
            try
            {
                var entity = await _db.Set<QuickReply>()
                    .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && !x.IsDeleted);

                if (entity == null)
                    return ResponseResult.ErrorInfo("❌ Quick reply not found.");

                // Only owner can edit personal; business-scope allowed for now
                if (entity.Scope == QuickReplyScope.Personal && entity.OwnerUserId != userId)
                    return ResponseResult.ErrorInfo("⛔ You cannot edit another user's personal quick reply.");

                entity.Title = dto.Title.Trim();
                entity.Body = dto.Body;
                entity.TagsCsv = dto.TagsCsv;
                entity.Language = dto.Language;
                entity.IsActive = dto.IsActive;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = actor;

                await _db.SaveChangesAsync();

                Log.Information("QuickReply updated {@QuickReplyId} for business {@BusinessId} by {@Actor}",
                    id, businessId, actor);

                return ResponseResult.SuccessInfo("✅ Quick reply updated.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating quick reply {@QuickReplyId} for business {BusinessId}", id, businessId);
                return ResponseResult.ErrorInfo("❌ Failed to update quick reply.", ex.ToString()); // campaign-style. :contentReference[oaicite:4]{index=4}
            }
        }

        public async Task<ResponseResult> ToggleActiveAsync(Guid businessId, Guid userId, string actor, Guid id, bool isActive)
        {
            try
            {
                var entity = await _db.Set<QuickReply>()
                    .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && !x.IsDeleted);

                if (entity == null)
                    return ResponseResult.ErrorInfo("❌ Quick reply not found.");

                if (entity.Scope == QuickReplyScope.Personal && entity.OwnerUserId != userId)
                    return ResponseResult.ErrorInfo("⛔ You cannot modify another user's personal quick reply.");

                entity.IsActive = isActive;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = actor;
                await _db.SaveChangesAsync();

                Log.Information("QuickReply toggled {@QuickReplyId} -> {IsActive} by {@Actor}",
                    id, isActive, actor);

                return ResponseResult.SuccessInfo(isActive ? "✅ Enabled." : "✅ Disabled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error toggling quick reply {@QuickReplyId}", id);
                return ResponseResult.ErrorInfo("❌ Failed to toggle quick reply.", ex.ToString());
            }
        }

        public async Task<ResponseResult> DeleteAsync(Guid businessId, Guid userId, string actor, Guid id)
        {
            try
            {
                var entity = await _db.Set<QuickReply>()
                    .FirstOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId && !x.IsDeleted);

                if (entity == null)
                    return ResponseResult.ErrorInfo("❌ Quick reply not found.");

                if (entity.Scope == QuickReplyScope.Personal && entity.OwnerUserId != userId)
                    return ResponseResult.ErrorInfo("⛔ You cannot delete another user's personal quick reply.");

                entity.IsDeleted = true;
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = actor;

                await _db.SaveChangesAsync();

                Log.Information("QuickReply soft-deleted {@QuickReplyId} by {@Actor}", id, actor);
                return ResponseResult.SuccessInfo("🗑️ Deleted.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting quick reply {@QuickReplyId}", id);
                return ResponseResult.ErrorInfo("❌ Failed to delete quick reply.", ex.ToString());
            }
        }
    }
}
