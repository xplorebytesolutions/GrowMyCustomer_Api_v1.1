using System;
using System.Collections.Generic;

namespace xbytechat.api.Features.Audiences.DTOs
{
    public class AudienceCreateDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    public class AudienceSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AudienceMemberDto
    {
        public Guid Id { get; set; }
        public Guid? ContactId { get; set; }   // optional link to CRM contact
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? VariablesJson { get; set; } // if your model stores row-level vars
        public DateTime CreatedAt { get; set; }
    }

    public class AudienceAssignDto
    {
        public List<Guid> ContactIds { get; set; } = new(); // optional: assign CRM contacts
        public Guid? CsvBatchId { get; set; }               // optional: attach CSV batch to audience
    }
}
