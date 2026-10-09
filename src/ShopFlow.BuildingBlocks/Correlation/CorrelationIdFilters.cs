using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Context;

namespace ShopFlow.BuildingBlocks.Correlation;

public class CorrelationIdSendFilter<T> : IFilter<SendContext<T>> where T : class
{
    private readonly IServiceProvider _serviceProvider;

    public CorrelationIdSendFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Probe(ProbeContext context) { }

    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        using var scope = _serviceProvider.CreateScope();
        var correlationContext = scope.ServiceProvider.GetService<CorrelationContext>();
        if (correlationContext != null && !string.IsNullOrEmpty(correlationContext.CorrelationId))
        {
            context.Headers.Set("X-Correlation-Id", correlationContext.CorrelationId);
        }

        await next.Send(context);
    }
}

public class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly IServiceProvider _serviceProvider;

    public CorrelationIdPublishFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Probe(ProbeContext context) { }

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        using var scope = _serviceProvider.CreateScope();
        var correlationContext = scope.ServiceProvider.GetService<CorrelationContext>();
        if (correlationContext != null && !string.IsNullOrEmpty(correlationContext.CorrelationId))
        {
            context.Headers.Set("X-Correlation-Id", correlationContext.CorrelationId);
        }

        await next.Send(context);
    }
}

public class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly IServiceProvider _serviceProvider;

    public CorrelationIdConsumeFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Probe(ProbeContext context) { }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var correlationId = context.Headers.Get<string>("X-Correlation-Id") ?? Guid.NewGuid().ToString("N");

        using var scope = _serviceProvider.CreateScope();
        var correlationContext = scope.ServiceProvider.GetService<CorrelationContext>();
        if (correlationContext != null)
        {
            correlationContext.CorrelationId = correlationId;
        }

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next.Send(context);
        }
    }
}
