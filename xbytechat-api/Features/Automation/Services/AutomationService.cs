using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using xbytechat.api.Features.Automation.Models;
using xbytechat.api.Features.Automation.Repositories;
using xbytechat.api.Features.CRM.Interfaces;
using xbytechat.api.Shared;

namespace xbytechat.api.Features.Automation.Services
{
    public class AutomationService : IAutomationService
    {
        private readonly IAutomationFlowRepository _flowRepository;
        private readonly IAutomationRunner _runner;
        private readonly IContactService _contactService;
        private readonly ILogger<AutomationService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AutomationService(
            IAutomationFlowRepository flowRepository,
            IAutomationRunner runner,
            IContactService contactService,
            ILogger<AutomationService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _flowRepository = flowRepository;
            _runner = runner;
            _contactService = contactService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<AutomationFlow?> GetFlowByIdAsync(Guid flowId, Guid businessId)
        {
            return await _flowRepository.GetByIdAsync(flowId, businessId);
        }

        public async Task<AutomationFlow?> GetFlowByKeywordAsync(Guid businessId, string keyword)
        {
            return await _flowRepository.GetByKeywordAsync(businessId, keyword);
        }

        public async Task<AutomationFlowRunResult> RunFlowAsync(
            AutomationFlow flow,
            Guid businessId,
            Guid contactId,
            string phone,
            string sourceChannel,
            string industryTag)
        {
            return await _runner.RunFlowAsync(flow, businessId, contactId, phone, sourceChannel, industryTag);
        }

        public async Task<IEnumerable<AutomationFlow>> GetAllFlowsAsync(Guid businessId)
        {
            return await _flowRepository.GetAllByBusinessAsync(businessId);
        }

        public async Task<AutomationFlow> CreateFlowAsync(Guid businessId, AutomationFlow flow)
        {
            flow.BusinessId = businessId;
            return await _flowRepository.CreateAsync(flow);
        }

        public async Task<bool> DeleteFlowAsync(Guid flowId, Guid businessId)
        {
            return await _flowRepository.DeleteAsync(flowId, businessId);
        }

        public async Task RunByKeywordAsync(string messageText, string phoneNumber, string sourceChannel = "whatsapp")
        {
            var businessId = _httpContextAccessor.HttpContext?.User?.GetBusinessId()
                ?? throw new UnauthorizedAccessException("BusinessId could not be resolved from context.");

            var flow = await _flowRepository.GetByKeywordAsync(businessId, messageText);
            if (flow == null)
            {
                _logger.LogInformation("No matching automation flow for keyword: {Keyword}", messageText);
                return;
            }

            var contact = await _contactService.FindOrCreateAsync(businessId, phoneNumber);
            await _runner.RunFlowAsync(flow, businessId, contact.Id, contact.PhoneNumber, sourceChannel, industryTag: "default");
        }

        public async Task<bool> TryRunFlowByKeywordAsync(
         Guid businessId,
         string messageText,
         string userPhone,
         string sourceChannel,
         string industryTag)
        {
            try
            {
                // 🔍 Normalize keyword
                var normalizedKeyword = messageText.Trim().ToLower();

                // ✅ Fetch flow by trigger keyword
                var flow = await _flowRepository.GetByKeywordAsync(businessId, normalizedKeyword);
                if (flow == null)
                {
                    _logger.LogInformation("TryRun: No matching automation flow found for keyword: '{Keyword}'", normalizedKeyword);
                    return false;
                }

                // 👤 Ensure contact exists
                var contact = await _contactService.FindOrCreateAsync(businessId, userPhone);
                if (contact == null)
                {
                    _logger.LogWarning("❌ TryRun: Failed to resolve or create contact for phone: {Phone}", userPhone);
                    return false;
                }

                // ▶️ Run automation flow
                _logger.LogInformation("🚀 Running flow '{FlowName}' for keyword '{Keyword}'", flow.Name, normalizedKeyword);
                await _runner.RunFlowAsync(flow, businessId, contact.Id, contact.PhoneNumber, sourceChannel, industryTag);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ TryRun: Exception while executing flow for keyword '{Keyword}'", messageText);
                return false;
            }
        }

    }
}
