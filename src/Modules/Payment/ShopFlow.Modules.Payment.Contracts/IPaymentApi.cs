namespace ShopFlow.Modules.Payment.Contracts;

public interface IPaymentApi
{
    Task<bool> ProcessPaymentAsync(string referenceId, decimal amount, CancellationToken cancellationToken = default);
}
