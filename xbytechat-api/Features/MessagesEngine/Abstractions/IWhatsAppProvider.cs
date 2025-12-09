// 📄 File: Features/MessagesEngine/Abstractions/IWhatsAppProvider.cs
using System.Threading.Tasks;
using System.Collections.Generic;

namespace xbytechat.api.Features.MessagesEngine.Abstractions
{
   
    public interface IWhatsAppProvider
    {
        Task<WaSendResult> SendTextAsync(string to, string body);
        Task<WaSendResult> SendTemplateAsync(string to, string templateName, string languageCode, IEnumerable<object> components);
        Task<WaSendResult> SendInteractiveAsync(object fullPayload); // prebuilt object (e.g., image + CTA)
    }
}



//namespace xbytechat.api.Features.MessagesEngine.Abstractions
//{
//    public interface IWhatsAppProvider
//    {
//        string Provider { get; }

//        Task<WaSendResult> SendTextAsync(string to, string body, CancellationToken ct = default);

//        Task<WaSendResult> SendTemplateAsync(string to, string templateName, string language, object? components, CancellationToken ct = default);

//        // Optional: interactive/image+CTA
//        Task<WaSendResult> SendInteractiveAsync(object payload, CancellationToken ct = default);
//    }
//}
