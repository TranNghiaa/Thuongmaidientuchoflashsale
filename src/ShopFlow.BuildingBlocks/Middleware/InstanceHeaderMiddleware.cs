using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ShopFlow.BuildingBlocks.Middleware;

internal sealed class InstanceHeaderMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public Task InvokeAsync(HttpContext context)
    {
        var instanceId = configuration["INSTANCE_ID"];
        if (!string.IsNullOrEmpty(instanceId))
        {
            context.Response.Headers.Append("X-Instance", instanceId);
        }
        return next(context);
    }
}
