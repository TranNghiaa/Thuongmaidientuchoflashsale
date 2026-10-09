using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShopFlow.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessageBus(this IServiceCollection services, IConfiguration configuration, Action<IBusRegistrationConfigurator>? configure = null)
    {
        var useRabbitMq = configuration.GetValue<bool>("RabbitMQ:Enabled");
        
        services.AddMassTransit(x =>
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith("ShopFlow") == true)
                .ToArray();

            x.AddConsumers(assemblies);

            configure?.Invoke(x);

            if (useRabbitMq)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var host = configuration["RabbitMQ:Host"] ?? "localhost";
                    var user = configuration["RabbitMQ:Username"] ?? "guest";
                    var pass = configuration["RabbitMQ:Password"] ?? "guest";

                    cfg.Host(host, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });

                    cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    cfg.UseSendFilter(typeof(ShopFlow.BuildingBlocks.Correlation.CorrelationIdSendFilter<>), context);
                    cfg.UsePublishFilter(typeof(ShopFlow.BuildingBlocks.Correlation.CorrelationIdPublishFilter<>), context);
                    cfg.UseConsumeFilter(typeof(ShopFlow.BuildingBlocks.Correlation.CorrelationIdConsumeFilter<>), context);

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    cfg.UseSendFilter(typeof(ShopFlow.BuildingBlocks.Correlation.CorrelationIdSendFilter<>), context);
                    cfg.UsePublishFilter(typeof(ShopFlow.BuildingBlocks.Correlation.CorrelationIdPublishFilter<>), context);
                    cfg.UseConsumeFilter(typeof(ShopFlow.BuildingBlocks.Correlation.CorrelationIdConsumeFilter<>), context);
                    
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }

    public static IServiceCollection AddOutboxPublisher<TContext>(this IServiceCollection services) 
        where TContext : Microsoft.EntityFrameworkCore.DbContext, IHasOutbox
    {
        services.AddHostedService<OutboxPublisher<TContext>>();
        return services;
    }
}
