using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ShopFlow.Modules.Inventory.Contracts;
using Proto = ShopFlow.Modules.Inventory.Protos;

namespace ShopFlow.Inventory.GrpcClient;

/// <summary>
/// gRPC client adapter that implements IInventoryApi by calling Inventory gRPC service.
/// T30: Dual-mode inventory — InProcess or Grpc.
/// </summary>
public sealed class InventoryGrpcClient : IInventoryApi, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly Proto.InventoryService.InventoryServiceClient _client;
    private readonly ILogger<InventoryGrpcClient> _logger;

    public InventoryGrpcClient(string grpcAddress, ILogger<InventoryGrpcClient> logger)
    {
        _logger = logger;
        _channel = GrpcChannel.ForAddress(grpcAddress);
        _client = new Proto.InventoryService.InventoryServiceClient(_channel);
    }

    /// <inheritdoc/>
    public async Task<bool> ReserveAsync(string referenceId, IEnumerable<ReserveItemDto> items,
        CancellationToken cancellationToken = default)
    {
        var request = new Proto.ReserveRequest { ReferenceId = referenceId };
        foreach (var item in items)
        {
            request.Items.Add(new Proto.ReserveItemDto { SkuId = item.SkuId, Quantity = item.Quantity });
        }

        var deadline = DateTime.UtcNow.AddSeconds(3);
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _client.ReserveAsync(request,
                    deadline: deadline,
                    cancellationToken: cancellationToken);
                return response.Success;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded && attempt < 3)
            {
                _logger.LogWarning("Reserve attempt {Attempt} failed: {Status}. Retrying...", attempt, ex.StatusCode);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                deadline = DateTime.UtcNow.AddSeconds(3);
            }
        }

        throw new RpcException(new Status(StatusCode.Unavailable, "Reserve failed after 3 attempts"));
    }

    /// <inheritdoc/>
    public async Task<bool> CommitAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        var request = new Proto.CommitRequest { ReferenceId = referenceId };
        var deadline = DateTime.UtcNow.AddSeconds(3);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _client.CommitAsync(request,
                    deadline: deadline,
                    cancellationToken: cancellationToken);
                return response.Success;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded && attempt < 3)
            {
                _logger.LogWarning("Commit attempt {Attempt} failed: {Status}. Retrying...", attempt, ex.StatusCode);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                deadline = DateTime.UtcNow.AddSeconds(3);
            }
        }

        throw new RpcException(new Status(StatusCode.Unavailable, "Commit failed after 3 attempts"));
    }

    /// <inheritdoc/>
    public async Task<bool> ReleaseAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        var request = new Proto.ReleaseRequest { ReferenceId = referenceId };
        var deadline = DateTime.UtcNow.AddSeconds(3);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _client.ReleaseAsync(request,
                    deadline: deadline,
                    cancellationToken: cancellationToken);
                return response.Success;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded && attempt < 3)
            {
                _logger.LogWarning("Release attempt {Attempt} failed: {Status}. Retrying...", attempt, ex.StatusCode);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                deadline = DateTime.UtcNow.AddSeconds(3);
            }
        }

        throw new RpcException(new Status(StatusCode.Unavailable, "Release failed after 3 attempts"));
    }

    public void Dispose() => _channel.Dispose();
}
