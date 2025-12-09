using System.ComponentModel.DataAnnotations;
using xbytechat.api.Features.CRM.Models;

public class ContactTag
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ContactId { get; set; }

    public Contact Contact { get; set; }

    [Required]
    public Guid TagId { get; set; }

    public Tag Tag { get; set; }

    [Required]
    public Guid BusinessId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public string? AssignedBy { get; set; }
}

