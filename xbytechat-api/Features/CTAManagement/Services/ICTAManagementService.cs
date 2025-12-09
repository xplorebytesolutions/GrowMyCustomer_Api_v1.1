using xbytechat.api.Features.CTAManagement.DTOs;

namespace xbytechat.api.Features.CTAManagement.Services
{
    public interface ICTAManagementService
    {
        /// <summary>Returns all active CTAs for the current business.</summary>
        Task<List<CTADefinitionDto>> GetAllAsync();

        /// <summary>Returns a single CTA by ID (if exists).</summary>
        Task<CTADefinitionDto?> GetByIdAsync(Guid id);

        /// <summary>Adds a new CTA for the logged-in business.</summary>
        Task<bool> AddAsync(CTADefinitionDto dto);

        /// <summary>Updates an existing CTA if it belongs to the business.</summary>
        Task<bool> UpdateAsync(Guid id, CTADefinitionDto dto);

        /// <summary>Soft deletes (deactivates) a CTA entry.</summary>
        Task<bool> DeleteAsync(Guid id);
    }
}
