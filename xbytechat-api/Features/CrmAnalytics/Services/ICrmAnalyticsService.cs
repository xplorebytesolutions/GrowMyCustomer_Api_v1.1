using System.Threading.Tasks;
using xbytechat.api.Features.CrmAnalytics.DTOs;

namespace xbytechat.api.Features.CrmAnalytics.Services
{
    /// <summary>
    /// Defines the contract for CRM Analytics services.
    /// Handles lead-level analytics, summary metrics, and dashboard insights.
    /// </summary>
    public interface ICrmAnalyticsService
    {
        /// <summary>
        /// Returns a summarized view of CRM statistics for a specific business.
        /// This is used to power the CRM analytics dashboard.
        /// </summary>
        /// <param name="businessId">The unique identifier of the business (tenant).</param>
        /// <returns>A summary DTO containing contact, tag, note, and reminder insights.</returns>
        Task<CrmAnalyticsSummaryDto> GetSummaryAsync(Guid businessId);
        Task<List<ContactTrendsDto>> GetContactTrendsAsync(Guid businessId);

    }
}
