namespace xbytechat.api.Features.Payment.Enums
{
    /// <summary>
    /// Status of a single payment transaction with a gateway.
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>
        /// Initiated but not confirmed yet (checkout created, awaiting gateway result).
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Successfully captured/confirmed by the gateway.
        /// </summary>
        Success = 1,

        /// <summary>
        /// Failed/declined; no funds captured.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// Refunded (partial or full).
        /// </summary>
        Refunded = 3,

        /// <summary>
        /// Chargeback/dispute or manual reversal.
        /// </summary>
        Reversed = 4
    }
}
