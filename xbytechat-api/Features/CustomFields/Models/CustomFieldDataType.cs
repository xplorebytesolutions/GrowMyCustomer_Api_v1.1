namespace xbytechat.api.Features.CustomFields.Models
{
    /// <summary>
    /// Supported field data types for Custom Fields.
    /// Stored as string in DB for readability and forward compatibility.
    /// </summary>
    public enum CustomFieldDataType
    {
        Text = 1,
        Number = 2,
        Date = 3,
        Boolean = 4,
        SingleSelect = 5,
        MultiSelect = 6
    }
}
