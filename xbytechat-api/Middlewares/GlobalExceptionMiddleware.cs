using System.Net;
using Serilog;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public string? StackTrace { get; set; }
        public string? Path { get; set; }
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ An unhandled exception occurred");

            // 🚩 Prevent double-write/headers-already-sent error!
            if (context.Response.HasStarted)
            {
                _logger.LogError("Response has already started, unable to write error response for path: {Path}", context.Request.Path);
                return;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new ErrorResponse
            {
                StatusCode = context.Response.StatusCode,
                Message = ex.Message,
                StackTrace = _env.IsDevelopment() ? ex.StackTrace : null,
                Path = context.Request.Path
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}


//using System.Net;
//using Serilog;
//using System.Net;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Logging;
//using Microsoft.AspNetCore.Hosting;

//public class GlobalExceptionMiddleware
//{
//    private readonly RequestDelegate _next;
//    private readonly ILogger<GlobalExceptionMiddleware> _logger;
//    private readonly IWebHostEnvironment _env;

//    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
//    {
//        _next = next;
//        _logger = logger;
//        _env = env;
//    }
//    public class ErrorResponse
//    {
//        public int StatusCode { get; set; }
//        public string Message { get; set; }
//        public string? StackTrace { get; set; }
//        public string Path { get; set; }
//    }

//    public async Task Invoke(HttpContext context)
//    {
//        try
//        {
//            await _next(context);
//        }
//        catch (Exception ex)
//        {
//            Log.Error(ex, "❌ An unhandled exception occurred");

//            context.Response.ContentType = "application/json";
//            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

//            var response = new ErrorResponse
//            {
//                StatusCode = context.Response.StatusCode,
//                Message = ex.Message,
//                StackTrace = _env.IsDevelopment() ? ex.StackTrace : null,
//                Path = context.Request.Path
//            };
//            await context.Response.WriteAsJsonAsync(response);
//        }
//    }

//}
