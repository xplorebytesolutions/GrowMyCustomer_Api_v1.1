// 📄 File: Features/CTAFlowBuilder/Models/CTAFlowStep.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xbytechat.api.Features.CTAFlowBuilder.Models
{
    /// <summary>
    /// Represents a single step in a CTA flow, triggered by a button.
    /// </summary>
    public class CTAFlowStep
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CTAFlowConfigId { get; set; }

        [ForeignKey(nameof(CTAFlowConfigId))]
        public CTAFlowConfig Flow { get; set; } = null!;

        public string TriggerButtonText { get; set; } = string.Empty;

        public string TriggerButtonType { get; set; } = "cta"; // e.g., "quick_reply"

        public string TemplateToSend { get; set; } = string.Empty;

        /// <summary>
        /// Optional media URL used when the selected WhatsApp template has a media header (image/video/document).
        /// Stored per-step so click-triggered CTA sends can resolve media without relying on campaign context.
        /// </summary>
        public string? HeaderMediaUrl { get; set; }

        /// <summary>
        /// JSON array of body parameter values (1-based placeholders) for WhatsApp templates, e.g. ["Alice","123"] for {{1}},{{2}}.
        /// Stored per-step because click-triggered CTA sends don't have campaign-time personalization context.
        /// </summary>
        public string? BodyParamsJson { get; set; }

        /// <summary>
        /// JSON array of URL-button parameter values (max 3). Index 0 => button index "0" (position 1), etc.
        /// Used only for templates with dynamic URL buttons (ParameterValue contains "{{n}}").
        /// Stored per-step so CTA runtime can satisfy Meta's required button parameters on click-triggered sends.
        /// </summary>
        public string? UrlButtonParamsJson { get; set; }

        public int StepOrder { get; set; }

        public string? RequiredTag { get; set; }        // e.g., "interested"
        public string? RequiredSource { get; set; }     // e.g., "ads", "qr", "manual"

        // 🔀 Multiple buttons linking to different steps
        public List<FlowButtonLink> ButtonLinks { get; set; } = new();

        public float? PositionX { get; set; }
        public float? PositionY { get; set; }
        public string? TemplateType { get; set; }

        // ✅ Use WhatsApp Profile Name in this step's template?
        public bool UseProfileName { get; set; } = false;

        // ✅ 1-based placeholder index in the template body (e.g., {{1}})
        public int? ProfileNameSlot { get; set; } = 1;

    }
}
