using System;
using Newtonsoft.Json.Linq;
using xbytechat.api.AuthModule.Models;
using xbytechat.api.Features.CampaignModule.Models;

namespace xbytechat.api.Features.CampaignModule.Services.SendPipeline
{
    public enum TemplateHeaderKind
    {
        Text,
        Image,
        Video,
        Document
    }

    public static class TemplateHeaderInspector
    {
        /// <summary>
        /// Reads a WhatsApp template row (from DB) and infers the HEADER kind quickly,
        /// using RawJson when present, otherwise falling back to flags like HasImageHeader.
        /// No network calls. Pure and deterministic.
        /// </summary>
        public static TemplateHeaderKind Infer(WhatsAppTemplate templateRow)
        {
            if (templateRow == null) return TemplateHeaderKind.Text;

            // 1) Prefer RawJson (when available) — read only the minimal bits we need
            var raw = templateRow.RawJson;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    var root = JObject.Parse(raw);
                    var comps = root["components"] as JArray;
                    if (comps != null)
                    {
                        foreach (var comp in comps)
                        {
                            var type = comp?["type"]?.ToString()?.Trim().ToUpperInvariant();
                            if (type == "HEADER")
                            {
                                var format = comp?["format"]?.ToString()?.Trim().ToUpperInvariant();
                                if (!string.IsNullOrEmpty(format))
                                {
                                    if (format == "IMAGE") return TemplateHeaderKind.Image;
                                    if (format == "VIDEO") return TemplateHeaderKind.Video;
                                    if (format == "DOCUMENT" || format == "PDF") return TemplateHeaderKind.Document;
                                    return TemplateHeaderKind.Text;
                                }
                                // If TYPE=HEADER but no format => treat as Text
                                return TemplateHeaderKind.Text;
                            }
                        }
                    }
                }
                catch
                {
                    // swallow; fall through to flags
                }
            }

            // 2) Fallback: existing DB flags
           // if (templateRow.HasImageHeader) return TemplateHeaderKind.Image;

            return TemplateHeaderKind.Text;
        }

        /// <summary>
        /// Maps a header kind to our “mediaType” shorthand used by send routing.
        /// </summary>
        public static string ToMediaType(TemplateHeaderKind kind)
        {
            return kind switch
            {
                TemplateHeaderKind.Image => "image",
                TemplateHeaderKind.Video => "video",
                TemplateHeaderKind.Document => "document",
                _ => "text"
            };
        }
    }
}
