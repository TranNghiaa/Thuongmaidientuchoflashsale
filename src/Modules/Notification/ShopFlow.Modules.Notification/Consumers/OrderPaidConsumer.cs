using MassTransit;
using Microsoft.Extensions.Logging;
using ShopFlow.Modules.Ordering.Contracts.Events;

namespace ShopFlow.Modules.Notification.Consumers;

public class OrderPaidConsumer : IConsumer<OrderPaid>
{
    private readonly ILogger<OrderPaidConsumer> _logger;

    public OrderPaidConsumer(ILogger<OrderPaidConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderPaid> context)
    {
        var ev = context.Message;
        _logger.LogInformation($"[NOTIFICATION] Sending email for Paid Order {ev.OrderId} to User {ev.UserId}. Total Amount: {ev.TotalAmount}.");
        
        // Simulating email sending
        return Task.CompletedTask;
    }
}
