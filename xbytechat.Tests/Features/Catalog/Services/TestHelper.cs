// File: xbytechat.Tests/TestHelper.cs

using System;
using xbytechat.api.Features.BusinessModule.Models;
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Models.BusinessModel;
using xbytechat.api.Features.CRM.Models;

namespace xbytechat.Tests
{
    public static class TestHelper
    {
        /// <summary>
        /// Creates a ready-to-use Business entity, including BusinessPlanInfo.
        /// </summary>
        public static Business CreateTestBusiness(
            Guid? id = null,
            string? companyName = null,
            string? businessEmail = null,
            PlanType plan = PlanType.Basic)
        {
            var businessId = id ?? Guid.NewGuid();
            return new Business
            {
                Id = businessId,
                CompanyName = companyName ?? "TestCompany",
                BusinessName = companyName ?? "TestCompany Pvt Ltd",
                BusinessEmail = businessEmail ?? "info@testcompany.com",
                CreatedAt = DateTime.UtcNow,
                BusinessPlanInfo = new BusinessPlanInfo
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Plan = plan,
                    TotalMonthlyQuota = 1000,
                    RemainingMessages = 1000,
                    QuotaResetDate = DateTime.UtcNow.AddMonths(1),
                    WalletBalance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
        }

        /// <summary>
        /// Creates a Contact for a given business.
        /// </summary>
        public static Contact CreateTestContact(
            Guid? id = null,
            Guid? businessId = null,
            string? name = null,
            string? phone = null)
        {
            return new Contact
            {
                Id = id ?? Guid.NewGuid(),
                BusinessId = businessId ?? Guid.NewGuid(),
                Name = name ?? "Test User",
                PhoneNumber = phone ?? "9990001111",
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
