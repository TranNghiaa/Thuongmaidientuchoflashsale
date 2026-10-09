using Microsoft.AspNetCore.Http;
using Serilog.Context;
using Microsoft.Extensions.Primitives;

namespace ShopFlow.BuildingBlocks.Logging;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CorrelationIdAccessor correlationIdAccessor)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() 
                            ?? Guid.NewGuid().ToString();

        correlationIdAccessor.CorrelationId = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
                {
                    context.Response.Headers.Append("X-Correlation-Id", new StringValues(correlationId));
                }
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
