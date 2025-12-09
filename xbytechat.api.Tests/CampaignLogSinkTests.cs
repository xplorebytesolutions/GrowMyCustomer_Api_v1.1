using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using xbytechat.api;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CampaignTracking.Logging;
using xbytechat.api.Features.CampaignTracking.Models;
using Xunit;

namespace xbytechat.api.Tests
{
    public class CampaignLogSinkTests
    {
        private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
        {
            private readonly T _current;
            public TestOptionsMonitor(T current) => _current = current;
            public T CurrentValue => _current;
            public T Get(string? name) => _current;
            public IDisposable OnChange(Action<T, string> listener) => NullDisposable.Instance;

            private sealed class NullDisposable : IDisposable
            {
                public static readonly NullDisposable Instance = new();
                public void Dispose() { }
            }
        }

        private static ServiceProvider BuildProvider(string dbName)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName));
            return services.BuildServiceProvider();
        }

        private static CampaignLogSink CreateSink(ServiceProvider sp)
        {
            var opts = new BatchingOptions
            {
                CampaignLog = new BatchingOptions.CampaignLogOptions
                {
                    MaxBatchSize = 100,
                    UseCopy = false
                }
            };
            return new CampaignLogSink(
                NullLogger<CampaignLogSink>.Instance,
                sp,
                new TestOptionsMonitor<BatchingOptions>(opts));
        }

        [Fact]
        public async Task FlushAsync_WaitsForMissingMessageLogs_ThenInserts()
        {
            var dbName = Guid.NewGuid().ToString();
            using var sp = BuildProvider(dbName);
            var sink = CreateSink(sp);

            var msgId1 = Guid.NewGuid();
            var msgId2 = Guid.NewGuid(); // missing initially
            var businessId = Guid.NewGuid();
            var campaignId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();

            // Seed only the first message log
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.MessageLogs.Add(new MessageLog
                {
                    Id = msgId1,
                    BusinessId = businessId,
                    CampaignId = campaignId,
                    RecipientNumber = "123",
                    MessageContent = "hi",
                    Status = "Sent",
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            sink.Enqueue(NewRecord(businessId, campaignId, recipientId, msgId1));
            sink.Enqueue(NewRecord(businessId, campaignId, recipientId, msgId2)); // missing

            // First flush: should defer due to missing msgId2
            await sink.FlushAsync();
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.CampaignSendLogs.Count().Should().Be(0);
            }

            // Add the missing message log
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.MessageLogs.Add(new MessageLog
                {
                    Id = msgId2,
                    BusinessId = businessId,
                    CampaignId = campaignId,
                    RecipientNumber = "456",
                    MessageContent = "hi",
                    Status = "Sent",
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            // Second flush: should insert both send logs
            await sink.FlushAsync();
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.CampaignSendLogs.Count().Should().Be(2);
                db.CampaignSendLogs.Select(x => x.MessageLogId).Should().BeEquivalentTo(new[] { msgId1, msgId2 });
            }
        }

        [Fact]
        public async Task FlushAsync_DropsBatchAfterMaxRetries_WhenMessageLogsMissing()
        {
            var dbName = Guid.NewGuid().ToString();
            using var sp = BuildProvider(dbName);
            var sink = CreateSink(sp);

            var missingId = Guid.NewGuid();
            var businessId = Guid.NewGuid();
            var campaignId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();

            sink.Enqueue(NewRecord(businessId, campaignId, recipientId, missingId));

            // Exceed max attempts (3) by flushing 4 times
            for (int i = 0; i < 4; i++)
            {
                await sink.FlushAsync();
            }

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CampaignSendLogs.Count().Should().Be(0);
        }

        private static CampaignLogRecord NewRecord(Guid businessId, Guid campaignId, Guid recipientId, Guid? messageLogId)
        {
            var now = DateTime.UtcNow;
            return new CampaignLogRecord(
                Id: Guid.NewGuid(),
                RunId: Guid.NewGuid(),
                MessageId: "mid",
                CampaignId: campaignId,
                ContactId: null,
                RecipientId: recipientId,
                MessageBody: "body",
                TemplateId: "tpl",
                SendStatus: "Sent",
                ErrorMessage: null,
                CreatedAt: now,
                CreatedBy: "test",
                SentAt: now,
                DeliveredAt: null,
                ReadAt: null,
                IpAddress: null,
                DeviceInfo: null,
                MacAddress: null,
                SourceChannel: "campaign",
                DeviceType: null,
                Browser: null,
                Country: null,
                City: null,
                IsClicked: false,
                ClickedAt: null,
                ClickType: null,
                RetryCount: 0,
                LastRetryAt: null,
                LastRetryStatus: null,
                AllowRetry: true,
                MessageLogId: messageLogId,
                BusinessId: businessId,
                CTAFlowConfigId: null,
                CTAFlowStepId: null,
                ButtonBundleJson: null
            );
        }
    }
}
