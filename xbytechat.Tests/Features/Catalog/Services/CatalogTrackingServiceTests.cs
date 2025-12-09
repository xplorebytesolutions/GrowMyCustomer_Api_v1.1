// File: Features/Catalog/Services/CatalogTrackingServiceTests.cs

using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Features.Catalog.Models;
using xbytechat.api.Features.Catalog.Services;
using xbytechat.api;
using xbytechat.api.Services.Messages.Interfaces;
using xbytechat.api.Models.BusinessModel; // For BusinessPlanInfo
using xbytechat.api.Features.PlanManagement.Models;
using xbytechat.api.Features.CRM.Timelines.Services; // For PlanType

namespace xbytechat.Tests.Features.Catalog.Services
{
    public class CatalogTrackingServiceTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task LogClickAsync_Should_Log_Click_And_Create_Contact_If_Not_Exists()
        {
            // Arrange
            var context = GetInMemoryDbContext();

            // Mock IMessageService and ILeadTimelineService
            var mockMessageService = new Mock<IMessageService>();
            mockMessageService.Setup(m => m.SendFollowUpAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var mockTimelineService = new Mock<ILeadTimelineService>();
            mockTimelineService.Setup(m => m.AddFromCatalogClickAsync(It.IsAny<CatalogClickLog>()))
                .Returns(Task.CompletedTask);

            var service = new CatalogTrackingService(context, mockMessageService.Object, mockTimelineService.Object);

            // Create a dummy business to avoid orphaned foreign key
            var businessId = Guid.NewGuid();
            context.Businesses.Add(new xbytechat.api.Features.BusinessModule.Models.Business
            {
                Id = businessId,
                CompanyName = "Test Business",
                BusinessName = "Test Business Pvt Ltd",
                BusinessEmail = "test@email.com",
                CreatedAt = DateTime.UtcNow,
                BusinessPlanInfo = new BusinessPlanInfo
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Plan = PlanType.Basic,
                    TotalMonthlyQuota = 100,
                    RemainingMessages = 100,
                    QuotaResetDate = DateTime.UtcNow.AddMonths(1),
                    WalletBalance = 0.00m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            });
            await context.SaveChangesAsync();

            var dto = new CatalogClickLogDto
            {
                BusinessId = businessId,
                ProductId = Guid.NewGuid(),
                UserPhone = "1234567890",
                UserName = "Test Lead",
                CTAJourney = "BuyNow",
                ButtonText = "Buy Now",
                TemplateId = "template123"
            };

            // Act
            await service.LogClickAsync(dto);

            // Assert
            var log = await context.CatalogClickLogs.FirstOrDefaultAsync();
            Assert.NotNull(log);
            Assert.Equal(dto.UserPhone, log.UserPhone);
            Assert.Equal(dto.CTAJourney, log.CTAJourney);

            var contact = await context.Contacts.FirstOrDefaultAsync(c => c.PhoneNumber == dto.UserPhone);
            Assert.NotNull(contact);
            Assert.Equal(dto.UserName, contact.Name);

            mockTimelineService.Verify(m => m.AddFromCatalogClickAsync(It.IsAny<CatalogClickLog>()), Times.Once);
        }

        [Fact]
        public async Task LogClickAsync_Should_Send_FollowUp_If_Business_Is_Advanced()
        {
            // Arrange
            var context = GetInMemoryDbContext();

            // Create a business with an Advanced plan
            var businessId = Guid.NewGuid();
            context.Businesses.Add(new xbytechat.api.Features.BusinessModule.Models.Business
            {
                Id = businessId,
                CompanyName = "Test Business",
                BusinessName = "Test Business Pvt Ltd",
                BusinessEmail = "advanced@email.com",
                CreatedAt = DateTime.UtcNow,
                BusinessPlanInfo = new BusinessPlanInfo
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Plan = PlanType.Advanced,
                    TotalMonthlyQuota = 1000,
                    RemainingMessages = 1000,
                    QuotaResetDate = DateTime.UtcNow.AddMonths(1),
                    WalletBalance = 0.00m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            });
            await context.SaveChangesAsync();

            // Mock services
            var mockMessageService = new Mock<IMessageService>();
            mockMessageService
                .Setup(m => m.SendFollowUpAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var mockTimelineService = new Mock<ILeadTimelineService>();
            mockTimelineService.Setup(m => m.AddFromCatalogClickAsync(It.IsAny<CatalogClickLog>()))
                .Returns(Task.CompletedTask);

            var service = new CatalogTrackingService(context, mockMessageService.Object, mockTimelineService.Object);

            var dto = new CatalogClickLogDto
            {
                BusinessId = businessId,
                ProductId = Guid.NewGuid(),
                UserPhone = "9876543210",
                UserName = "Advanced User",
                CTAJourney = "LearnMore",
                ButtonText = "Learn More",
                TemplateId = "tpl-001"
            };

            // Act
            await service.LogClickAsync(dto);

            // Assert
            var contact = await context.Contacts.FirstOrDefaultAsync(c => c.PhoneNumber == dto.UserPhone);
            Assert.NotNull(contact);
            Assert.Equal(dto.UserName, contact.Name);

            var log = await context.CatalogClickLogs.FirstOrDefaultAsync(l => l.UserPhone == dto.UserPhone);
            Assert.NotNull(log);

            mockMessageService.Verify(m => m.SendFollowUpAsync(dto.UserPhone, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task LogClickAsync_Should_Not_Send_FollowUp_If_Business_Is_Not_Advanced()
        {
            // Arrange
            var context = GetInMemoryDbContext();

            // Create a business with a Basic plan
            var businessId = Guid.NewGuid();
            context.Businesses.Add(new xbytechat.api.Features.BusinessModule.Models.Business
            {
                Id = businessId,
                CompanyName = "Basic Business",
                BusinessName = "Basic Business Pvt Ltd",
                BusinessEmail = "basic@email.com",
                CreatedAt = DateTime.UtcNow,
                BusinessPlanInfo = new BusinessPlanInfo
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Plan = PlanType.Basic,
                    TotalMonthlyQuota = 100,
                    RemainingMessages = 100,
                    QuotaResetDate = DateTime.UtcNow.AddMonths(1),
                    WalletBalance = 0.00m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            });
            await context.SaveChangesAsync();

            // Mock services
            var mockMessageService = new Mock<IMessageService>();
            mockMessageService
                .Setup(m => m.SendFollowUpAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var mockTimelineService = new Mock<ILeadTimelineService>();
            mockTimelineService.Setup(m => m.AddFromCatalogClickAsync(It.IsAny<CatalogClickLog>()))
                .Returns(Task.CompletedTask);

            var service = new CatalogTrackingService(context, mockMessageService.Object, mockTimelineService.Object);

            var dto = new CatalogClickLogDto
            {
                BusinessId = businessId,
                ProductId = Guid.NewGuid(),
                UserPhone = "9876543210",
                UserName = "Basic User",
                CTAJourney = "LearnMore",
                ButtonText = "Learn More",
                TemplateId = "tpl-001"
            };

            // Act
            await service.LogClickAsync(dto);

            // Assert
            var contact = await context.Contacts.FirstOrDefaultAsync(c => c.PhoneNumber == dto.UserPhone);
            Assert.NotNull(contact);

            var log = await context.CatalogClickLogs.FirstOrDefaultAsync(l => l.UserPhone == dto.UserPhone);
            Assert.NotNull(log);

            mockMessageService.Verify(m => m.SendFollowUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
