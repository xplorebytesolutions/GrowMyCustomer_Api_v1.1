using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.CTAManagement.DTOs;
using xbytechat.api.Features.CTAManagement.Models;

namespace xbytechat.api.Features.CTAManagement.Services
{
    public class CTAManagementService : ICTAManagementService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CTAManagementService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        // 🔄 Get all active CTAs for the current business
        public async Task<List<CTADefinitionDto>> GetAllAsync()
        {
            var businessId = GetBusinessIdFromClaims();

            return await _dbContext.CTADefinitions
                .Where(c => c.IsActive && c.BusinessId == businessId)
                .Select(c => new CTADefinitionDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    ButtonText = c.ButtonText,
                    ButtonType = c.ButtonType,
                    TargetUrl = c.TargetUrl,
                    Description = c.Description,
                    IsActive = c.IsActive
                }).ToListAsync();
        }

        // ✅ Add new CTA
        public async Task<bool> AddAsync(CTADefinitionDto dto)
        {
            var businessId = GetBusinessIdFromClaims();

            var cta = new CTADefinition
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Title = dto.Title,
                ButtonText = dto.ButtonText,
                ButtonType = dto.ButtonType,
                TargetUrl = dto.TargetUrl,
                Description = dto.Description ?? "",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.CTADefinitions.AddAsync(cta);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // 📌 Get CTA by ID
        public async Task<CTADefinitionDto?> GetByIdAsync(Guid id)
        {
            var businessId = GetBusinessIdFromClaims();

            var cta = await _dbContext.CTADefinitions
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && c.BusinessId == businessId);

            if (cta == null) return null;

            return new CTADefinitionDto
            {
                Id = cta.Id,
                Title = cta.Title,
                ButtonText = cta.ButtonText,
                ButtonType = cta.ButtonType,
                TargetUrl = cta.TargetUrl,
                Description = cta.Description,
                IsActive = cta.IsActive
            };
        }


        // ✏️ Update CTA
        public async Task<bool> UpdateAsync(Guid id, CTADefinitionDto dto)
        {
            var cta = await _dbContext.CTADefinitions.FindAsync(id);
            if (cta == null) return false;

            var businessId = GetBusinessIdFromClaims();
            if (cta.BusinessId != businessId) throw new UnauthorizedAccessException("Unauthorized to modify this CTA.");

            cta.Title = dto.Title;
            cta.ButtonText = dto.ButtonText;
            cta.ButtonType = dto.ButtonType;
            cta.TargetUrl = dto.TargetUrl;
            cta.Description = dto.Description ?? "";
            cta.IsActive = dto.IsActive;
            cta.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        // 🗑️ Soft Delete CTA
        public async Task<bool> DeleteAsync(Guid id)
        {
            var cta = await _dbContext.CTADefinitions.FindAsync(id);
            if (cta == null) return false;

            var businessId = GetBusinessIdFromClaims();
            if (cta.BusinessId != businessId) throw new UnauthorizedAccessException("Unauthorized to delete this CTA.");

            cta.IsActive = false;
            cta.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        // 🔐 Reusable method to extract BusinessId
        private Guid GetBusinessIdFromClaims()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("businessId");
            if (claim == null || string.IsNullOrWhiteSpace(claim.Value) || !Guid.TryParse(claim.Value, out var businessId))
                throw new UnauthorizedAccessException("❌ Invalid or missing BusinessId claim.");

            return businessId;
        }

    }
}
