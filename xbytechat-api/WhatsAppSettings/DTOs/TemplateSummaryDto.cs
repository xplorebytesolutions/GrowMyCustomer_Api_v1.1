using System;
using System.Collections.Generic;

namespace xbytechat.api.WhatsAppSettings.DTOs
{
    /// <summary>
    /// Section-aware summary of a provider template.
    /// Distinct slots are tracked per section to avoid collisions (e.g., header {{1}} vs body {{1}}).
    /// </summary>
    public sealed class TemplateSummaryDto
    {
        // --- What users see in CSV/FE ---
        public string HeaderKind { get; init; } = "none";           // none | text | image | video | document
        public string? HeaderText { get; init; }                    // original header text if TEXT
        public string? BodyText { get; init; }                      // original body text
        public string CombinedPreviewBody { get; init; } = "";      // HeaderText + "\n\n" + BodyText (for FE/CSV)

        // --- Distinct slot lists per section (order of appearance) ---
        public List<int> HeaderParamIndices { get; init; } = new(); // e.g., [1,2]
        public List<int> BodyParamIndices { get; init; } = new();   // e.g., [1,2,3]

        // Per-button parameter templates (URL/tel/etc), each with their own local indices
        public List<ButtonParamTemplate> Buttons { get; init; } = new(); // up to 3, in template order (1..3)

        // Convenience total for CSV/validation (sum across sections, not union)
        public int PlaceholderCount { get; init; }                  // HeaderParamIndices.Count + BodyParamIndices.Count + Σ Buttons[i].ParamIndices.Count
    }

    public sealed class ButtonParamTemplate
    {
        public int Order { get; init; }                 // 1..3 (template order)
        public string Type { get; init; } = "";         // URL | PHONE_NUMBER | QUICK_REPLY | FLOW | ...
        public string Text { get; init; } = "";         // Button label
        public string? ParamTemplate { get; init; }     // e.g., "https://x.example/{{1}}", "tel:+911234567890"
        public List<int> ParamIndices { get; init; } = new(); // e.g., [1]
    }

    /// <summary>
    /// Result of filling values across all sections in the agreed order:
    /// [ header slots... , body slots... , (btn1 slots...), (btn2 slots...), ... ]
    /// </summary>
    public sealed class TemplateFillResult
    {
        public string ResolvedHeaderText { get; init; } = "";
        public string ResolvedBodyText { get; init; } = "";
        public string ResolvedCombinedPreviewBody { get; init; } = "";
        public List<ResolvedButtonParam> ResolvedButtons { get; init; } = new(); // aligned with TemplateSummaryDto.Buttons
        public List<string> Warnings { get; init; } = new();
        public List<string> Errors { get; init; } = new();
    }

    public sealed class ResolvedButtonParam
    {
        public int Order { get; init; }            // 1..3
        public string Type { get; init; } = "";
        public string Text { get; init; } = "";
        public string? ResolvedValue { get; init; } // filled URL/tel/... or original if no placeholders
    }
}
