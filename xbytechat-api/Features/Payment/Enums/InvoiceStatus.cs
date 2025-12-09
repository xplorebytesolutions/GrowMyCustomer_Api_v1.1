namespace xbytechat.api.Features.Payment.Enums
{
    /// <summary>
    /// State of an invoice in the billing lifecycle.
    /// </summary>
    public enum InvoiceStatus
    {
        Draft = 0,
        Open = 1,
        Paid = 2,
        Void = 3
    }
}
