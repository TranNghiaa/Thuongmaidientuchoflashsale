using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShopFlow.BuildingBlocks.Messaging;

public class OutboxPublisher<TContext> : BackgroundService where TContext : DbContext, IHasOutbox
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisher<TContext>> _logger;

    public OutboxPublisher(IServiceProvider serviceProvider, ILogger<OutboxPublisher<TContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"OutboxPublisher for {typeof(TContext).Name} started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing outbox for {typeof(TContext).Name}.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Polling every 5 seconds
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();
        
        var messages = await db.OutboxMessages
            .Where(x => x.ProcessedOn == null)
            .OrderBy(x => x.OccurredOn)
            .Take(50)
            .ToListAsync(stoppingToken);

        if (!messages.Any()) return;

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type);
                if (type != null)
                {
                    var payload = JsonSerializer.Deserialize(message.Content, type);
                    if (payload != null)
                    {
                        await bus.Publish(payload, type, stoppingToken);
                    }
                }
                
                message.ProcessedOn = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Error = ex.ToString();
                _logger.LogError(ex, $"Failed to publish outbox message {message.Id}");
            }
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}
