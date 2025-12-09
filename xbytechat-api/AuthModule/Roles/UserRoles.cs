namespace xbytechat.api.AuthModule.Roles
{
    public static class UserRoles
    {
        public const string Admin = "admin";         // xByte Admin
        public const string Business = "business";   // Tenant Admin
        public const string Staff = "staff";         // CRM Staff (future)
        public const string Agent = "agent";         // WhatsApp/chat agent
        public const string CRM = "crm";             // CRM-only user (future)
        public const string Partner = "partner";
    }
}
