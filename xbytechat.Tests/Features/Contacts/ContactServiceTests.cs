using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using xbytechat.api;
using xbytechat.api.CRM.Dtos;
using xbytechat.api.CRM.Models;
using xbytechat.api.CRM.Services;
using xbytechat.api.Helpers;

namespace xbytechat.Tests.CRM.Services
{
    public class ContactServiceTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly Mock<ILogger<ContactService>> _loggerMock;
        private readonly ContactService _contactService;

        public ContactServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _dbContext = new AppDbContext(options);
            _loggerMock = new Mock<ILogger<ContactService>>();
            _contactService = new ContactService(_dbContext, _loggerMock.Object);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }

        [Fact]
        public async Task AddContactAsync_ShouldAddContact()
        {
            var dto = new ContactDto
            {
                Name = "Test User",
                PhoneNumber = "1234567890",
                Email = "test@example.com"
            };
            var businessId = Guid.NewGuid();

            var result = await _contactService.AddContactAsync(businessId, dto);

            Assert.NotNull(result);
            Assert.Equal(dto.Name, result.Name);
            Assert.Equal(dto.PhoneNumber, result.PhoneNumber);

            var contactInDb = await _dbContext.Contacts.FindAsync(result.Id);
            Assert.NotNull(contactInDb);
            Assert.Equal(businessId, contactInDb.BusinessId);
            Assert.True(contactInDb.IsActive);
        }

        [Fact]
        public async Task GetContactByIdAsync_ShouldReturnContact_WhenExists()
        {
            var businessId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Existing User",
                PhoneNumber = "0987654321",
                IsActive = true
            };
            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync();

            var result = await _contactService.GetContactByIdAsync(businessId, contact.Id);

            Assert.NotNull(result);
            Assert.Equal(contact.Name, result.Name);
        }

        [Fact]
        public async Task GetContactByIdAsync_ShouldReturnNull_WhenInactive()
        {
            var businessId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Inactive User",
                PhoneNumber = "0000000000",
                IsActive = false
            };
            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync();

            var result = await _contactService.GetContactByIdAsync(businessId, contact.Id);

            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateContactAsync_ShouldReturnFalse_WhenNotFound()
        {
            var businessId = Guid.NewGuid();
            var dto = new ContactDto { Id = Guid.NewGuid(), Name = "New Name" };

            var success = await _contactService.UpdateContactAsync(businessId, dto);

            Assert.False(success);
        }

        [Fact]
        public async Task UpdateContactAsync_ShouldUpdateContact_WhenFound()
        {
            var businessId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Old Name",
                PhoneNumber = "1112223333",
                IsActive = true
            };
            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync();

            var dto = new ContactDto
            {
                Id = contact.Id,
                Name = "Updated Name",
                PhoneNumber = contact.PhoneNumber
            };

            var success = await _contactService.UpdateContactAsync(businessId, dto);

            Assert.True(success);

            var updated = await _dbContext.Contacts.FindAsync(contact.Id);
            Assert.Equal("Updated Name", updated.Name);
        }

        [Fact]
        public async Task DeleteContactAsync_ShouldSoftDelete_WhenFound()
        {
            var businessId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "To Delete",
                PhoneNumber = "9998887777",
                IsActive = true
            };
            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync();

            var success = await _contactService.DeleteContactAsync(businessId, contact.Id);

            Assert.True(success);

            var deleted = await _dbContext.Contacts.FindAsync(contact.Id);
            Assert.False(deleted.IsActive);
        }

        [Fact]
        public async Task GetPagedContactsAsync_ShouldReturnCorrectPage()
        {
            var businessId = Guid.NewGuid();

            for (int i = 1; i <= 50; i++)
            {
                _dbContext.Contacts.Add(new Contact
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = $"User {i}",
                    PhoneNumber = $"100000000{i}",
                    IsActive = true
                });
            }
            await _dbContext.SaveChangesAsync();

            //var page1 = await _contactService.GetPagedContactsAsync(businessId, page: 1, pageSize: 10);
            //var page2 = await _contactService.GetPagedContactsAsync(businessId, page: 2, pageSize: 10);

            Assert.Equal(50, page1.TotalCount);
            Assert.Equal(10, page1.Items.Count);
            Assert.Equal("User 1", page1.Items.First().Name);

            Assert.Equal(10, page2.Items.Count);
            Assert.Equal("User 11", page2.Items.First().Name);
        }

        [Fact]
        public async Task ToggleFavoriteAsync_ShouldToggleValue()
        {
            var businessId = Guid.NewGuid();
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Fav User",
                PhoneNumber = "5554443333",
                IsFavorite = false,
                IsActive = true
            };
            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync();

            var firstToggle = await _contactService.ToggleFavoriteAsync(businessId, contact.Id);
            Assert.True(firstToggle);

            var updated = await _dbContext.Contacts.FindAsync(contact.Id);
            Assert.True(updated.IsFavorite);

            var secondToggle = await _contactService.ToggleFavoriteAsync(businessId, contact.Id);
            Assert.True(secondToggle);

            updated = await _dbContext.Contacts.FindAsync(contact.Id);
            Assert.False(updated.IsFavorite);
        }

        [Fact]
        public async Task ParseCsvToContactsAsync_ShouldReturnSuccessAndErrors()
        {
            var csvContent = "Name,PhoneNumber,Email\nJohn Doe,1234567890,john@example.com\nInvalid Line\nJane Doe,0987654321,jane@example.com\n";
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));

            var result = await _contactService.ParseCsvToContactsAsync(Guid.NewGuid(), stream);

            Assert.NotNull(result);
            Assert.Equal(2, result.SuccessRecords.Count);
            Assert.Single(result.Errors);
            Assert.Contains("RowNumber", result.Errors[0].RowNumber.ToString());
        }
    }
}
