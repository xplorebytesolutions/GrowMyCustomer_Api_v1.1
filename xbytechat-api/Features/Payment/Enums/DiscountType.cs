namespace xbytechat.api.Features.Payment.Enums
{
    /// <summary>
    /// Defines how a discount value should be applied.
    /// </summary>
    public enum DiscountType
    {
        /// <summary>
        /// Fixed amount off (e.g. ₹500 off).
        /// </summary>
        FixedAmount = 1,

        /// <summary>
        /// Percentage off (e.g. 20% off).
        /// </summary>
        Percentage = 2
    }
}
