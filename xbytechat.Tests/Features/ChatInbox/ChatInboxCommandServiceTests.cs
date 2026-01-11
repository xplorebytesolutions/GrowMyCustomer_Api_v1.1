using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using xbytechat.api;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.ChatInbox.Services;
using xbytechat.api.Features.AccessControl.Models;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;
using xbytechat.api.Helpers;

namespace xbytechat.Tests.Features.ChatInbox
{
    public sealed class ChatInboxCommandServiceTests
    {
        private static AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task SendAgentMessageAsync_Blocks_When_Conversation_Is_Closed()
        {
            var db = GetInMemoryDbContext();
            var engine = new Mock<IMessageEngineService>(MockBehavior.Strict);
            var svc = new ChatInboxCommandService(db, engine.Object);

            var businessId = Guid.NewGuid();
            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = "staff"
            };

            var actorUserId = Guid.NewGuid();
            db.Roles.Add(role);
            db.Users.Add(new xbytechat.api.AuthModule.Models.User
            {
                Id = actorUserId,
                BusinessId = businessId,
                Name = "Agent",
                Email = "agent@example.com",
                PasswordHash = "x",
                Status = "Active",
                RoleId = role.Id,
                Role = role,
                IsDeleted = false
            });

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Customer",
                PhoneNumber = "+10000000000",
                InboxStatus = "Closed",
                IsArchived = true,
                IsActive = false
            };

            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            var request = new ChatInboxSendMessageRequestDto
            {
                BusinessId = businessId,
                ContactId = contact.Id,
                To = contact.PhoneNumber,
                Text = "Hello",
                ActorUserId = actorUserId
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SendAgentMessageAsync(request));
            ex.Message.Should().Contain("closed");

            engine.Verify(x => x.SendTextDirectAsync(It.IsAny<TextMessageSendDto>()), Times.Never);
        }

        [Fact]
        public async Task SendAgentMessageAsync_Sends_Image_When_MediaId_Present()
        {
            var db = GetInMemoryDbContext();
            var engine = new Mock<IMessageEngineService>(MockBehavior.Strict);
            var svc = new ChatInboxCommandService(db, engine.Object);

            var businessId = Guid.NewGuid();
            var role = new Role { Id = Guid.NewGuid(), Name = "staff" };
            var actorUserId = Guid.NewGuid();

            db.Roles.Add(role);
            db.Users.Add(new xbytechat.api.AuthModule.Models.User
            {
                Id = actorUserId,
                BusinessId = businessId,
                Name = "Agent",
                Email = "agent@example.com",
                PasswordHash = "x",
                Status = "Active",
                RoleId = role.Id,
                Role = role,
                IsDeleted = false
            });

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Customer",
                PhoneNumber = "+10000000000",
                InboxStatus = "Open",
                IsArchived = false,
                IsActive = true,
                AssignedAgentId = actorUserId
            };
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            engine
                .Setup(x => x.SendImageDirectAsync(It.Is<MediaMessageSendDto>(d =>
                    d.BusinessId == businessId &&
                    d.ContactId == contact.Id &&
                    d.RecipientNumber == contact.PhoneNumber &&
                    d.MediaId == "mid-123" &&
                    d.Caption == "caption")))
                .ReturnsAsync(new ResponseResult { Success = true, Message = "OK" });

            var request = new ChatInboxSendMessageRequestDto
            {
                BusinessId = businessId,
                ContactId = contact.Id,
                To = contact.PhoneNumber,
                ActorUserId = actorUserId,
                Text = "caption",
                MediaId = "mid-123",
                MediaType = "image",
                FileName = "pic.jpg",
                MimeType = "image/jpeg"
            };

            await svc.SendAgentMessageAsync(request);

            engine.Verify(x => x.SendImageDirectAsync(It.IsAny<MediaMessageSendDto>()), Times.Once);
            engine.Verify(x => x.SendDocumentDirectAsync(It.IsAny<MediaMessageSendDto>()), Times.Never);
            engine.Verify(x => x.SendTextDirectAsync(It.IsAny<TextMessageSendDto>()), Times.Never);
        }

        [Fact]
        public async Task SendAgentMessageAsync_Sends_Document_When_MediaId_Present()
        {
            var db = GetInMemoryDbContext();
            var engine = new Mock<IMessageEngineService>(MockBehavior.Strict);
            var svc = new ChatInboxCommandService(db, engine.Object);

            var businessId = Guid.NewGuid();
            var role = new Role { Id = Guid.NewGuid(), Name = "staff" };
            var actorUserId = Guid.NewGuid();

            db.Roles.Add(role);
            db.Users.Add(new xbytechat.api.AuthModule.Models.User
            {
                Id = actorUserId,
                BusinessId = businessId,
                Name = "Agent",
                Email = "agent@example.com",
                PasswordHash = "x",
                Status = "Active",
                RoleId = role.Id,
                Role = role,
                IsDeleted = false
            });

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Customer",
                PhoneNumber = "+10000000000",
                InboxStatus = "Open",
                IsArchived = false,
                IsActive = true,
                AssignedAgentId = actorUserId
            };
            db.Contacts.Add(contact);
            await db.SaveChangesAsync();

            engine
                .Setup(x => x.SendDocumentDirectAsync(It.Is<MediaMessageSendDto>(d =>
                    d.MediaId == "mid-999" &&
                    d.FileName == "doc.pdf")))
                .ReturnsAsync(new ResponseResult { Success = true, Message = "OK" });

            var request = new ChatInboxSendMessageRequestDto
            {
                BusinessId = businessId,
                ContactId = contact.Id,
                To = contact.PhoneNumber,
                ActorUserId = actorUserId,
                MediaId = "mid-999",
                MediaType = "document",
                FileName = "doc.pdf",
                MimeType = "application/pdf"
            };

            await svc.SendAgentMessageAsync(request);

            engine.Verify(x => x.SendDocumentDirectAsync(It.IsAny<MediaMessageSendDto>()), Times.Once);
            engine.Verify(x => x.SendImageDirectAsync(It.IsAny<MediaMessageSendDto>()), Times.Never);
            engine.Verify(x => x.SendTextDirectAsync(It.IsAny<TextMessageSendDto>()), Times.Never);
        }
    }
}
