namespace xbytechat.api.AuthModule.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        // ✅ Extra fields
        public Guid BusinessId { get; set; }
        public string CompanyName { get; set; }
        public string Plan { get; set; }
        public string AccessToken { get; set; }

        public Guid? PlanId { get; set; }
    }
}
