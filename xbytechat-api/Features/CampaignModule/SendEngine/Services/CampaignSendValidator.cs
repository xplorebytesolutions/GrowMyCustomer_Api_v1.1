using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using xbytechat.api.Features.CampaignModule.SendEngine;
namespace xbytechat.api.Features.CampaignModule.SendEngine.Services
{
    public sealed record ValidationIssue(string Field, string Message, string Severity = "error");

    public sealed class ValidationResult
    {
        public bool IsValid => Issues.Count == 0 || Issues.All(i => i.Severity == "warning");
        public List<ValidationIssue> Issues { get; } = new();
        public void Error(string field, string msg) => Issues.Add(new(field, msg, "error"));
        public void Warn(string field, string msg) => Issues.Add(new(field, msg, "warning"));
    }

    public static class CampaignSendValidator
    {
        public static ValidationResult ValidateBeforePayload(
            SendPlan plan,
            RecipientPlan recipient,
            TemplateEnvelope envelope)
        {
            var vr = new ValidationResult();

            // ---- PLAN ----
            if (plan.BusinessId == Guid.Empty)
                vr.Error("plan.businessId", "BusinessId is required.");

            if (string.IsNullOrWhiteSpace(plan.TemplateName))
                vr.Error("plan.templateName", "Template name is required.");

            if (string.IsNullOrWhiteSpace(plan.LanguageCode))
                vr.Warn("plan.language", "Language not specified; default will be used.");

            if (string.IsNullOrWhiteSpace(plan.PhoneNumberId))
                vr.Error("plan.phoneNumberId", "PhoneNumberId is required to send.");

            if (plan.Buttons is { Count: > 3 })
                vr.Error("plan.buttons", "Max 3 buttons are allowed by WhatsApp.");

            // Header media URL if media header
            if (plan.HeaderKind is HeaderKind.Image or HeaderKind.Video or HeaderKind.Document)
            {
                if (string.IsNullOrWhiteSpace(envelope.HeaderUrl))
                    vr.Error("header.url", $"HeaderKind={plan.HeaderKind} requires a non-empty HeaderUrl.");
                else if (!LooksLikeAbsoluteUrl(envelope.HeaderUrl!))
                    vr.Error("header.url", "HeaderUrl must be absolute http/https URL (or wa/tel where supported).");
            }

            // ---- RECIPIENT ----
            if (recipient.RecipientId == Guid.Empty)
                vr.Error("recipient.id", "RecipientId is required.");

            if (string.IsNullOrWhiteSpace(recipient.ToPhoneE164))
                vr.Error("recipient.phone", "Recipient phone (E164) is required.");
            else if (!IsLikelyPhone(recipient.ToPhoneE164))
                vr.Warn("recipient.phone", "Phone shape looks unusual for E.164.");

            if (string.IsNullOrWhiteSpace(recipient.IdempotencyKey))
                vr.Warn("recipient.idemKey", "IdempotencyKey missing (retries may duplicate).");

            // ---- PARAM COUNTS ----
            // BODY params come from recipient.ParametersJson (array of strings)
            var bodyParams = DeserializeArray(recipient.ParametersJson);
            // HEADER TEXT params come from envelope.HeaderParams (already extracted)
            var headerParams = envelope.HeaderParams?.ToArray() ?? Array.Empty<string>();

            if (plan.HeaderKind == HeaderKind.Text && headerParams.Length == 0)
                vr.Warn("header.text.params", "Template has TEXT header but no header params were provided.");

            // ---- DYNAMIC URL BUTTONS ----
            // Provider mappers look for keys like: button1.url_param / button2.url_param ...
            var perRecipient = DeserializeDict(envelope.PerRecipientButtonParamsJson);
            var dynNeeded = plan.Buttons
                .Select((b, i) => new { b, idx = i })
                .Where(x => string.Equals(x.b.Type, "url", StringComparison.OrdinalIgnoreCase))
                .Select(x => new { Index = x.idx, Key = $"button{x.idx + 1}.url_param" })
                .ToList();

            foreach (var need in dynNeeded)
            {
                if (perRecipient.TryGetValue(need.Key, out var val))
                {
                    if (string.IsNullOrWhiteSpace(val))
                        vr.Error($"buttons[{need.Index}]", $"Dynamic URL button {need.Index + 1} has empty param value.");
                }
                // Note: Not all URL buttons must be dynamic. If your template *requires* dynamic,
                // you can enforce using live metadata at materialization time.
            }

            return vr;
        }

        private static string[] DeserializeArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try { return JsonSerializer.Deserialize<string[]>(json!) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }

        private static Dictionary<string, string> DeserializeDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json!) ?? new(); }
            catch { return new(); }
        }

        private static bool LooksLikeAbsoluteUrl(string s)
        {
            if (s.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("wa:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("https://wa.me/", StringComparison.OrdinalIgnoreCase)) return true;
            return Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
                   (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLikelyPhone(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var digits = s.Count(char.IsDigit);
            return digits >= 10 && digits <= 15 && s.StartsWith("+");
        }
    }
}