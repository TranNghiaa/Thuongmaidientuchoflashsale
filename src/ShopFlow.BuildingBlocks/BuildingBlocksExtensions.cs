using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.BuildingBlocks.Correlation;
using ShopFlow.BuildingBlocks.Middleware;
using System;

namespace ShopFlow.BuildingBlocks;

public static class BuildingBlocksExtensions
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        services.AddSingleton<CorrelationContext>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    public static IApplicationBuilder UseBuildingBlocks(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<InstanceHeaderMiddleware>();
        return app;
    }
}
