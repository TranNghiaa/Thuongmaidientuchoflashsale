using MassTransit;
using Microsoft.Extensions.Logging;
using ShopFlow.Modules.Ordering.Contracts.Events;

namespace ShopFlow.Modules.Notification.Consumers;

public class OrderPaidFaultConsumer : IConsumer<Fault<OrderPaid>>
{
    private readonly ILogger<OrderPaidFaultConsumer> _logger;

    public OrderPaidFaultConsumer(ILogger<OrderPaidFaultConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<Fault<OrderPaid>> context)
    {
        var ev = context.Message.Message;
        _logger.LogError($"[NOTIFICATION] Failed to send email for Paid Order {ev.OrderId} to User {ev.UserId}. Error: {context.Message.Exceptions.FirstOrDefault()?.Message}");
        return Task.CompletedTask;
    }
}
