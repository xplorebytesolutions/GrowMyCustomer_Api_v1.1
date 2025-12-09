namespace xbytechat.api.Features.AccessControl.DTOs;

// DTO: Role details used across layers
public class RoleDto
{
    /// <summary>Unique identifier of the role.</summary>
    public Guid Id { get; set; }

   
    public string Role { get; set; } = default!;

   
    public string Code { get; set; } = default!;

   
    public string? Description { get; set; }

    
    public bool IsActive { get; set; }

       public RoleDto() { }

    // Convenience constructor
    
}
