using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using xbytechat.api.CRM.Dtos;
using xbytechat.api.CRM.Models;
using xbytechat.api.CRM.Services;


namespace xbytechat.api.Tests.Services
{
    public class ContactServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public ContactServiceTests()
        {
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"ContactTestDb_{Guid.NewGuid()}")
                .Options;
        }

        [Fact]
        public async Task AddContactAsync_ShouldAddNewContact()
        {
            // Arrange
            var businessId = Guid.NewGuid();
            var dto = new ContactDto
            {
                Name = "John Tester",
                PhoneNumber = "9876543210",
                Email = "john@example.com",
                LeadSource = "WhatsApp",
                Tags = "lead,new"
            };

            using var context = new AppDbContext(_dbOptions);
            var service = new ContactService(context);

            // Act
            var result = await service.AddContactAsync(businessId, dto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("John Tester");
            result.PhoneNumber.Should().Be("9876543210");

            var saved = await context.Contacts.FirstOrDefaultAsync();
            saved.Should().NotBeNull();
            saved.BusinessId.Should().Be(businessId);
            saved.Email.Should().Be("john@example.com");
        }

        [Fact]
        public async Task GetAllContactsAsync_ShouldReturnAllContactsForBusiness()
        {
            // Arrange
            var businessId1 = Guid.NewGuid();
            var businessId2 = Guid.NewGuid();

            var contact1 = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId1,
                Name = "Alice",
                PhoneNumber = "1111111111",
                CreatedAt = DateTime.UtcNow
            };

            var contact2 = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId1,
                Name = "Bob",
                PhoneNumber = "2222222222",
                CreatedAt = DateTime.UtcNow
            };

            var contactOtherBusiness = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId2,
                Name = "Charlie",
                PhoneNumber = "3333333333",
                CreatedAt = DateTime.UtcNow
            };

            using (var context = new AppDbContext(_dbOptions))
            {
                await context.Contacts.AddRangeAsync(contact1, contact2, contactOtherBusiness);
                await context.SaveChangesAsync();
            }

            using (var context = new AppDbContext(_dbOptions))
            {
                var service = new ContactService(context);

                // Act
                var result = await service.GetAllContactsAsync(businessId1);

                // Assert
                result.Should().NotBeNull();
                result.Should().HaveCount(2);
                result.Should().OnlyContain(c => c.PhoneNumber == "1111111111" || c.PhoneNumber == "2222222222");
            }
        }

        [Fact]
        public async Task UpdateContactAsync_ShouldUpdateFields()
        {
            // Arrange
            var businessId = Guid.NewGuid();
            var originalContact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Old Name",
                PhoneNumber = "9999999999",
                Email = "old@example.com",
                CreatedAt = DateTime.UtcNow
            };

            using (var context = new AppDbContext(_dbOptions))
            {
                // Seed the original contact into the database
                await context.Contacts.AddAsync(originalContact);
                await context.SaveChangesAsync();
            }

            // Create a DTO with updated data to simulate what frontend sends
            var updatedDto = new ContactDto
            {
                Id = originalContact.Id,
                Name = "New Name",
                PhoneNumber = "1234567890",
                Email = "new@example.com",
                LeadSource = "Google",
                Tags = "priority,updated",
                LastContactedAt = DateTime.UtcNow,
                Notes = "This is a test update"
            };

            using (var context = new AppDbContext(_dbOptions))
            {
                var service = new ContactService(context);

                // Act: Call the update method
                var success = await service.UpdateContactAsync(businessId, updatedDto);

                // Assert 1: Ensure the method returns true
                success.Should().BeTrue();

                // Fetch the updated contact


            }
        }


        // Step 1: Seed the DB with one contact to delete
        // This simulates a real record in the system
        // Step 2: Call the DeleteContactAsync method from the service
        // This should remove the record with the matching ID + BusinessId
        // Step 3: Assert deletion was successful (true returned)
        // Then re-query the DB to ensure the contact no longer exists

        [Fact]
        public async Task DeleteContactAsync_ShouldRemoveRecord()
        {
            // Arrange
            var businessId = Guid.NewGuid();
            var contactToDelete = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Delete Me",
                PhoneNumber = "7777777777",
                CreatedAt = DateTime.UtcNow
            };

            using (var context = new AppDbContext(_dbOptions))
            {
                // Add the contact to the in-memory DB
                await context.Contacts.AddAsync(contactToDelete);
                await context.SaveChangesAsync();
            }

            using (var context = new AppDbContext(_dbOptions))
            {
                var service = new ContactService(context);

                // Act: Attempt to delete the contact
                var result = await service.DeleteContactAsync(businessId, contactToDelete.Id);

                // Assert 1: Deletion should return true
                result.Should().BeTrue();

                // Assert 2: Contact should not exist in the database anymore
                var deleted = await context.Contacts.FindAsync(contactToDelete.Id);
                deleted.Should().BeNull();
            }
        }

    }
}
