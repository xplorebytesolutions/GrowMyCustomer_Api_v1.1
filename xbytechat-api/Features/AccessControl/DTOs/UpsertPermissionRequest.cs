// 📁 Features/AccessControl/DTOs/UpsertPermissionRequest.cs
namespace xbytechat.api.Features.AccessControl.DTOs
{
    public class UpsertPermissionRequest
    {
        // Code is required only on create. On update, we ignore it.
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Logical group/module/workspace, e.g. "Messaging", "Campaigns", "CRM".
        /// This maps to Permission.Group in the model.
        /// </summary>
        public string? Group { get; set; }

        public string? Description { get; set; }
    }
}
