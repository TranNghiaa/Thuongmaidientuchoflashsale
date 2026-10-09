using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.BuildingBlocks.Database;
using ShopFlow.Modules.Payment.Domain;
using ShopFlow.Modules.Payment.Infrastructure;

namespace ShopFlow.Modules.Payment;

public static class PaymentModule
{
    public static IServiceCollection AddPaymentModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IModuleMigrator, PaymentMigrator>();
        services.AddScoped<ShopFlow.Modules.Payment.Contracts.IPaymentApi, ShopFlow.Modules.Payment.Application.PaymentApi>();

        return services;
    }

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("").WithTags("Payment");

        group.MapPost("/payment/process", async (ProcessPaymentRequest req, PaymentDbContext db, TimeProvider timeProvider) =>
        {
            if (string.IsNullOrWhiteSpace(req.ReferenceId) || req.Amount <= 0)
                return Results.BadRequest(new { code = "INVALID_REQUEST", message = "ReferenceId and valid Amount are required." });

            // Mock Stripe processing
            // For demo: if amount > 1,000,000 it fails randomly? Actually, let's just make it success if referenceId doesn't end with "FAIL".
            var isSuccess = !req.ReferenceId.EndsWith("-FAIL", StringComparison.OrdinalIgnoreCase);

            var transaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                ReferenceId = req.ReferenceId,
                Amount = req.Amount,
                Status = isSuccess ? "Success" : "Failed",
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };

            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();

            return Results.Ok(new { transaction.Id, transaction.ReferenceId, transaction.Status });
        });

        return endpoints;
    }
}

internal class ProcessPaymentRequest
{
    public string ReferenceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
