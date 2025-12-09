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
