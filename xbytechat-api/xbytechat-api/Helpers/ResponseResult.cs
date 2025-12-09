//namespace xbytechat.api.Helpers
//{
//    /// <summary>
//    /// Represents a standardized response structure for service layer results.
//    /// </summary>
//    public class ResponseResult
//    {
//        public bool Success { get; set; }                  // ✅ Whether operation succeeded
//        public string Message { get; set; }                // ✅ User-friendly message
//        public object? Data { get; set; }                  // Optional payload (if needed)

//        // ✅ WhatsApp-specific diagnostics
//        public string? ErrorMessage { get; set; }          // Error from API or exception
//        public string? RawResponse { get; set; }           // Full API raw response

//        public string? MessageId { get; set; } // 🌐 WhatsApp WAMID (Message ID)

//        public Guid? LogId { get; set; } // ✅ Unique ID of MessageLog for tracking
//                                         // ✅ Factory method for successful result

//        public string? Token { get; set; }

//        public string? RefreshToken { get; set; }
//        public static ResponseResult SuccessInfo(string message, object? data = null, string? raw = null)
//        {
//            return new ResponseResult
//            {
//                Success = true,
//                Message = message,
//                Data = data,
//                RawResponse = raw
//            };
//        }

//        // ❌ Factory method for error result
//        public static ResponseResult ErrorInfo(string message, string? error = null, string? raw = null)
//        {
//            return new ResponseResult
//            {
//                Success = false,
//                Message = message,
//                ErrorMessage = error,
//                RawResponse = raw
//            };
//        }
//    }
//}
using System;
using System.Net;

namespace xbytechat.api.Helpers
{
    /// <summary>
    /// Standard service-layer result with optional HTTP-like status code and payload.
    /// Backward-compatible with existing callers that use SuccessInfo/ErrorInfo/Data.
    /// </summary>
    public class ResponseResult
    {
        // Primary flags
        public bool Success { get; set; }                 // Whether operation succeeded
        public string Message { get; set; } = string.Empty;

        // Optional status code (HTTP-like, but not tied to ASP.NET)
        public int Code { get; set; } = (int)HttpStatusCode.OK;

        // Primary payload (prefer this going forward)
        public object? Payload { get; set; }

        // Back-compat data field (kept to avoid breaking callers)
        public object? Data { get; set; }

        // WhatsApp / diagnostics (retained)
        public string? ErrorMessage { get; set; }
        public string? RawResponse { get; set; }
        public string? MessageId { get; set; }
        public Guid? LogId { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }

        // ---- Factory helpers (preferred) ----
        public static ResponseResult Ok(string message = "OK", object? payload = null)
            => new()
            {
                Success = true,
                Code = (int)HttpStatusCode.OK,
                Message = message,
                Payload = payload,
                Data = payload // keep Data in sync for legacy consumers
            };

        public static ResponseResult Created(string message = "Created", object? payload = null)
            => new()
            {
                Success = true,
                Code = (int)HttpStatusCode.Created,
                Message = message,
                Payload = payload,
                Data = payload
            };

        public static ResponseResult NotFound(string message = "Not found", object? payload = null)
            => new()
            {
                Success = false,
                Code = (int)HttpStatusCode.NotFound,
                Message = message,
                Payload = payload
            };

        public static ResponseResult Conflict(string message = "Conflict", object? payload = null)
            => new()
            {
                Success = false,
                Code = (int)HttpStatusCode.Conflict,
                Message = message,
                Payload = payload
            };

        public static ResponseResult BadRequest(string message = "Bad request", object? payload = null, string? error = null)
            => new()
            {
                Success = false,
                Code = (int)HttpStatusCode.BadRequest,
                Message = message,
                Payload = payload,
                ErrorMessage = error
            };

        public static ResponseResult Forbidden(string message = "Forbidden")
            => new()
            {
                Success = false,
                Code = (int)HttpStatusCode.Forbidden,
                Message = message
            };

        public static ResponseResult FromException(Exception ex, string message = "Unexpected error")
            => new()
            {
                Success = false,
                Code = (int)HttpStatusCode.InternalServerError,
                Message = message,
                ErrorMessage = ex.Message,
                RawResponse = ex.ToString()
            };

        // ---- Backward-compatible helpers (kept; route to new ones) ----

        /// <summary>
        /// Legacy success. Prefer Ok(...) going forward.
        /// </summary>
        public static ResponseResult SuccessInfo(string message, object? data = null, string? raw = null)
        {
            return new ResponseResult
            {
                Success = true,
                Code = (int)HttpStatusCode.OK,
                Message = message,
                Payload = data,
                Data = data,
                RawResponse = raw
            };
        }

        /// <summary>
        /// Legacy error. Prefer BadRequest/Conflict/NotFound/... going forward.
        /// </summary>
        public static ResponseResult ErrorInfo(string message, string? error = null, string? raw = null)
        {
            return new ResponseResult
            {
                Success = false,
                Code = (int)HttpStatusCode.BadRequest,
                Message = message,
                ErrorMessage = error,
                RawResponse = raw
            };
        }
    }
}
