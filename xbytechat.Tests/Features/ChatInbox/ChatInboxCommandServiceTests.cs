using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using xbytechat.api;
using xbytechat.api.Features.ChatInbox.DTOs;
using xbytechat.api.Features.ChatInbox.Services;
using xbytechat.api.Features.CRM.Models;
using xbytechat.api.Features.MessagesEngine.DTOs;
using xbytechat.api.Features.MessagesEngine.Services;

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
                Text = "Hello"
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SendAgentMessageAsync(request));
            ex.Message.Should().Contain("closed");

            engine.Verify(x => x.SendTextDirectAsync(It.IsAny<TextMessageSendDto>()), Times.Never);
        }
    }
}

