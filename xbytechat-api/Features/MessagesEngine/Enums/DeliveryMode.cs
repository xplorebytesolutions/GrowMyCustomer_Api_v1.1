using System.Collections;

namespace xbytechat.api.Features.MessagesEngine.Enums
{
    public enum DeliveryMode
    {
        /// <summary>
        /// Default behaviour: enqueue into Outbox/worker.
        /// Use this for campaigns, bulk sends, and scheduled jobs.
        /// </summary>
        Queue = 0,

        /// <summary>
        /// High-priority conversational sends:
        /// call the WhatsApp provider directly inside the request.
        /// </summary>
        Immediate = 1
    }
}
