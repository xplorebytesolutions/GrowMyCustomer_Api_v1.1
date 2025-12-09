namespace xbytechat.api.Features.CRM.Dtos
{
    public class TagDto
    {
        public Guid? Id { get; set; }

        public string Name { get; set; } = default!;

        public string? ColorHex { get; set; }

        public string? Category { get; set; }

        public string? Notes { get; set; }

        public bool IsSystemTag { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }
}
