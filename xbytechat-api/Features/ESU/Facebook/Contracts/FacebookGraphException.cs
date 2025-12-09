#nullable enable
using System;

namespace xbytechat.api.Features.ESU.Facebook.Contracts
{
    public sealed class FacebookGraphException : Exception
    {
        public string? Type { get; }
        public int? Code { get; }
        public int? SubCode { get; }
        public string? TraceId { get; }

        public FacebookGraphException(string message, string? type, int? code, int? subCode, string? traceId)
            : base(message)
        {
            Type = type;
            Code = code;
            SubCode = subCode;
            TraceId = traceId;
        }
    }
}
