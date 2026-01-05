using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using xbytechat.api;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.ChatInbox.Services;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.Inbox.Hubs;

namespace xbytechat.Tests.Features.ChatInbox
{
    public sealed class ChatInboxAssignmentServiceTests
    {
        private static AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new AppDbContext(options);
        }

        private static IChatInboxAssignmentService CreateService(AppDbContext db)
        {
            var logger = new Mock<ILogger<ChatInboxAssignmentService>>();

            var hub = new Mock<IHubContext<InboxHub>>();
            var clients = new Mock<IHubClients>();
            var proxy = new Mock<IClientProxy>();

            proxy
                .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
                .Returns(Task.CompletedTask);

            clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
            hub.SetupGet(h => h.Clients).Returns(clients.Object);

            return new ChatInboxAssignmentService(db, logger.Object, hub.Object);
        }

        [Fact]
        public async Task AssignAsync_Allows_Self_Assign()
        {
            var db = GetInMemoryDbContext();
            var svc = CreateService(db);

            var businessId = Guid.NewGuid();

            var role = new Role { Id = Guid.NewGuid(), Name = "Staff" };
            var actor = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Actor",
                Email = "actor@test.com",
                PasswordHash = "x",
                Status = "Active",
                IsDeleted = false,
                RoleId = role.Id,
                Role = role
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Customer",
                PhoneNumber = "+10000000000",
                IsActive = true,
                IsArchived = false,
                InboxStatus = "Open"
            };

            db.Roles.Add(role);
            db.Users.Add(actor);
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            await svc.AssignAsync(businessId, contact.Id, actor.Id, actor.Id);

            var updated = await db.Contacts.FindAsync(contact.Id);
            updated!.AssignedAgentId.Should().Be(actor.Id);
        }

        [Fact]
        public async Task AssignAsync_Assigning_To_Other_Agent_Requires_Permission()
        {
            var db = GetInMemoryDbContext();
            var svc = CreateService(db);

            var businessId = Guid.NewGuid();

            var role = new Role { Id = Guid.NewGuid(), Name = "Staff" };
            var actor = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Actor",
                Email = "actor@test.com",
                PasswordHash = "x",
                Status = "Active",
                IsDeleted = false,
                RoleId = role.Id,
                Role = role
            };

            var other = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Other",
                Email = "other@test.com",
                PasswordHash = "x",
                Status = "Active",
                IsDeleted = false,
                RoleId = role.Id,
                Role = role
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Customer",
                PhoneNumber = "+10000000000",
                IsActive = true,
                IsArchived = false,
                InboxStatus = "Open"
            };

            db.Roles.Add(role);
            db.Users.AddRange(actor, other);
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.AssignAsync(businessId, contact.Id, other.Id, actor.Id));
        }

        [Fact]
        public async Task SetStatusAsync_Only_Allows_Assignee()
        {
            var db = GetInMemoryDbContext();
            var svc = CreateService(db);

            var businessId = Guid.NewGuid();

            var role = new Role { Id = Guid.NewGuid(), Name = "Staff" };
            var assignee = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Assignee",
                Email = "assignee@test.com",
                PasswordHash = "x",
                Status = "Active",
                IsDeleted = false,
                RoleId = role.Id,
                Role = role
            };

            var other = new User
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Other",
                Email = "other@test.com",
                PasswordHash = "x",
                Status = "Active",
                IsDeleted = false,
                RoleId = role.Id,
                Role = role
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Customer",
                PhoneNumber = "+10000000000",
                IsActive = true,
                IsArchived = false,
                InboxStatus = "Open",
                AssignedAgentId = assignee.Id
            };

            db.Roles.Add(role);
            db.Users.AddRange(assignee, other);
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.SetStatusAsync(businessId, contact.Id, "Closed", other.Id));

            await svc.SetStatusAsync(businessId, contact.Id, "Closed", assignee.Id);

            var updated = await db.Contacts.FindAsync(contact.Id);
            updated!.InboxStatus.Should().Be("Closed");
            updated.IsArchived.Should().BeTrue();
            updated.IsActive.Should().BeFalse();
        }
    }
}

