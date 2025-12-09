using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace xbytechat.api.Features.CustomeApi.DTOs
{
    public sealed class DirectTemplateSendRequest
    {
        [Required] public string PhoneNumberId { get; set; } = string.Empty;
        [Required] public string To { get; set; } = string.Empty;
        [Required] public string TemplateId { get; set; } = string.Empty;

        /// <summary>Body variable map for {{1}}, {{2}}, ...</summary>
        public Dictionary<string, string>? Variables { get; set; }

        /// <summary>Optional: provide a https .mp4 to attach a VIDEO header.</summary>
        public string? VideoUrl { get; set; }

        /// <summary>Optional CTA flow to link with this send (for click→next-step mapping and analytics).</summary>
        public Guid? FlowConfigId { get; set; }
    }
}


//using System; // <-- needed for Guid
//using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;

//namespace xbytechat.api.Features.CustomeApi.DTOs
//{
//    public sealed class DirectTemplateSendRequest
//    {
//        [Required] public string PhoneNumberId { get; set; } = string.Empty;
//        [Required] public string To { get; set; } = string.Empty;
//        [Required] public string TemplateId { get; set; } = string.Empty;

//        // Optional: start (link) a CTA flow on this send (we'll stamp CTAFlowConfigId/StepId on MessageLog)
//        public Guid? FlowConfigId { get; set; }   // <---- add this

//        // Body variables as WhatsApp {{1}}, {{2}}, ...
//        public Dictionary<string, string>? Variables { get; set; }

//        // Optional header media, validated based on template header type:
//        public string? ImageUrl { get; set; } // IMAGE header
//        public string? VideoUrl { get; set; } // VIDEO header
//        public string? DocumentUrl { get; set; } // DOCUMENT/PDF header
//        public string? DocumentFilename { get; set; } // optional nice filename
//    }
//}
