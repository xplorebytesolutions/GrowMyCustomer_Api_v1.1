namespace xbytechat.api.Validators
{
    /// <summary>
    /// Centralized validator for supported message types (text, image, template, etc.)
    /// </summary>
    public static class MessageTypeValidator
    {
        private static readonly HashSet<string> SupportedTypes = new()
        {
            "text", "image", "template"
        };

        /// <summary>
        /// Checks whether a messageType is supported.
        /// </summary>
        public static bool IsValid(string? messageType)
        {
            return !string.IsNullOrWhiteSpace(messageType) &&
                   SupportedTypes.Contains(messageType.ToLower());
        }

        /// <summary>
        /// Returns all supported message types.
        /// </summary>
        public static IEnumerable<string> GetSupportedTypes()
        {
            return SupportedTypes;
        }
    }
}
