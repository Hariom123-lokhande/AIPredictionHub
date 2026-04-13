using Serilog.Context;
using System.Security.Claims;

namespace AIPredictionHub.Middleware
{
    public class LoggingEnrichmentMiddleware
    {
        private readonly RequestDelegate _next;

        public LoggingEnrichmentMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                using (LogContext.PushProperty("UserId", userId))
                {
                    await _next(context);
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
