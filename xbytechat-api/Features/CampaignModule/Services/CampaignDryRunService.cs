using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using xbytechat.api;
using xbytechat.api.Features.CampaignModule.DTOs;
using xbytechat.api.Features.CampaignModule.Models;
using xbytechat.api.Shared.utility;
using xbytechat.api.WhatsAppSettings.Services;
using xbytechat_api.WhatsAppSettings.Services;

namespace xbytechat.api.Features.CampaignModule.Services
{
    /// <summary>
    /// Dry-run validator for campaigns. Checks template existence, parameter counts,
    /// dynamic button placeholders, and recipient phone presence/shape.
    /// </summary>
    public class CampaignDryRunService : ICampaignDryRunService
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppTemplateFetcherService _templateFetcher;

        private static readonly Regex PlaceholderRe = new(@"\{\{\s*(\d+)\s*\}\}", RegexOptions.Compiled);

        public CampaignDryRunService(AppDbContext db, IWhatsAppTemplateFetcherService templateFetcher)
        {
            _db = db;
            _templateFetcher = templateFetcher;
        }

        public async Task<CampaignDryRunResultDto> ValidateAsync(
            Guid businessId,
            Guid campaignId,
            int limit = 200,
            CancellationToken ct = default)
        {
            if (businessId == Guid.Empty) throw new UnauthorizedAccessException("Invalid business id.");
            if (campaignId == Guid.Empty) throw new ArgumentException("campaignId is required.");

            // Load campaign + recipients(+contacts) + variable maps + buttons (read-only)
            var campaign = await _db.Campaigns
                .AsNoTracking()
                .Include(c => c.MultiButtons)
                .Include(c => c.VariableMaps)
                .FirstOrDefaultAsync(c => c.Id == campaignId && c.BusinessId == businessId, ct);

            if (campaign == null)
                throw new KeyNotFoundException("Campaign not found.");

            // Recipients (contact needed for phone)
            var recipients = await _db.CampaignRecipients
                .AsNoTracking()
                .Include(r => r.Contact)
                .Where(r => r.CampaignId == campaignId && r.BusinessId == businessId)
                .OrderBy(r => r.UpdatedAt)
                .Take(limit)
                .ToListAsync(ct);

            // Determine template name (prefer TemplateId → MessageTemplate) and fetch metadata
            var templateName = (campaign.TemplateId ?? campaign.MessageTemplate ?? "").Trim();
            if (string.IsNullOrWhiteSpace(templateName))
            {
                // No template at all — return result with a single global error across all recipients
                return BuildResult(
                    campaignId,
                    templateName: "",
                    language: "en_US",
                    placeholderCount: 0,
                    recipients: recipients,
                    globalError: "Template name is missing on campaign."
                );
            }

            var meta = await _templateFetcher.GetTemplateByNameAsync(businessId, templateName, includeButtons: true);
            if (meta == null)
            {
                return BuildResult(
                    campaignId,
                    templateName,
                    language: "en_US",
                    placeholderCount: 0,
                    recipients: recipients,
                    globalError: $"Template '{templateName}' not found for business."
                );
            }

            var language = (meta.Language ?? "en_US").Trim();
            var placeholderCount = Math.Max(0, meta.PlaceholderCount);

            // Campaign-stored parameters: if supplied, compare counts
            var storedParams = TemplateParameterHelper.ParseTemplateParams(campaign.TemplateParameters);
            bool paramCountMismatch = storedParams.Count > 0 && storedParams.Count != placeholderCount;

            // Validate buttons for dynamic placeholders ({{n}})
            var dynamicButtonIssues = new List<string>();
            var buttonPlaceholdersNeeded = new HashSet<int>();

            foreach (var b in (campaign.MultiButtons ?? Enumerable.Empty<CampaignButton>()))
            {
                var value = b.Value ?? "";
                foreach (Match m in PlaceholderRe.Matches(value))
                {
                    if (int.TryParse(m.Groups[1].Value, out var n))
                    {
                        buttonPlaceholdersNeeded.Add(n);
                    }
                }
            }

            foreach (var n in buttonPlaceholdersNeeded)
            {
                if (storedParams.Count > 0 && (n < 1 || n > storedParams.Count))
                {
                    dynamicButtonIssues.Add($"Button needs placeholder {{%{n}%}} but campaign parameters only provide {storedParams.Count} value(s).");
                }
                if (placeholderCount > 0 && (n < 1 || n > placeholderCount))
                {
                    dynamicButtonIssues.Add($"Button needs placeholder {{%{n}%}} but template defines only {placeholderCount} placeholder(s).");
                }
            }

            // Build per-recipient issues
            var issues = new List<CampaignDryRunIssueDto>();
            foreach (var r in recipients)
            {
                var phone = r.Contact?.PhoneNumber?.Trim();

                if (string.IsNullOrWhiteSpace(phone))
                {
                    issues.Add(new CampaignDryRunIssueDto
                    {
                        RecipientId = r.Id,
                        ContactId = r.ContactId,
                        Phone = phone,
                        Severity = "error",
                        Message = "Phone is missing."
                    });
                }
                else if (!IsLikelyPhone(phone))
                {
                    issues.Add(new CampaignDryRunIssueDto
                    {
                        RecipientId = r.Id,
                        ContactId = r.ContactId,
                        Phone = phone,
                        Severity = "warning",
                        Message = "Phone format looks unusual."
                    });
                }
            }

            // Add global-ish issues once (we’ll attribute them to a null recipient)
            if (paramCountMismatch)
            {
                issues.Add(new CampaignDryRunIssueDto
                {
                    Severity = "warning",
                    Message = $"Placeholder count mismatch: template expects {placeholderCount}, campaign provided {storedParams.Count}.",
                });
            }

            foreach (var bi in dynamicButtonIssues.Distinct())
            {
                issues.Add(new CampaignDryRunIssueDto
                {
                    Severity = "error",
                    Message = bi
                });
            }

            var result = new CampaignDryRunResultDto
            {
                CampaignId = campaignId,
                TemplateName = templateName,
                Language = language,
                PlaceholderCount = placeholderCount,
                CheckedRecipients = recipients.Count,
                Issues = issues,
                ErrorCount = issues.Count(i => string.Equals(i.Severity, "error", StringComparison.OrdinalIgnoreCase)),
                WarningCount = issues.Count(i => string.Equals(i.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            };

            Log.Information("Dry-run completed for Campaign {CampaignId} (biz {BusinessId}) → {Errors} errors, {Warnings} warnings over {Checked} recipients",
                campaignId, businessId, result.ErrorCount, result.WarningCount, result.CheckedRecipients);

            return result;
        }

        private static CampaignDryRunResultDto BuildResult(
            Guid campaignId,
            string templateName,
            string language,
            int placeholderCount,
            List<CampaignRecipient> recipients,
            string globalError)
        {
            var issues = new List<CampaignDryRunIssueDto>
            {
                new CampaignDryRunIssueDto
                {
                    Severity = "error",
                    Message = globalError
                }
            };

            return new CampaignDryRunResultDto
            {
                CampaignId = campaignId,
                TemplateName = templateName,
                Language = language,
                PlaceholderCount = placeholderCount,
                CheckedRecipients = recipients.Count,
                Issues = issues,
                ErrorCount = issues.Count,
                WarningCount = 0
            };
        }

        private static bool IsLikelyPhone(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var digits = s.Count(char.IsDigit);
            return digits >= 10 && digits <= 15;
        }
    }
}
