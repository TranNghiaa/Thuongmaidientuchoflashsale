using ShopFlow.Modules.Payment.Contracts;
using ShopFlow.Modules.Payment.Domain;
using ShopFlow.Modules.Payment.Infrastructure;

namespace ShopFlow.Modules.Payment.Application;

internal class PaymentApi : IPaymentApi
{
    private readonly PaymentDbContext _db;
    private readonly TimeProvider _timeProvider;

    public PaymentApi(PaymentDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<bool> ProcessPaymentAsync(string referenceId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceId) || amount <= 0)
            return false;

        var isSuccess = !referenceId.EndsWith("-FAIL", StringComparison.OrdinalIgnoreCase);

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = referenceId,
            Amount = amount,
            Status = isSuccess ? "Success" : "Failed",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        return isSuccess;
    }
}
