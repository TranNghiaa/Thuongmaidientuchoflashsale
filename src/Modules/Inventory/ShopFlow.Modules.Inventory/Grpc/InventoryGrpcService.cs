using Grpc.Core;
using Proto = ShopFlow.Modules.Inventory.Protos;

namespace ShopFlow.Modules.Inventory.Grpc;

internal class InventoryGrpcService : Proto.InventoryService.InventoryServiceBase
{
    public override Task<Proto.GetSkuInfoResponse> GetSkuInfo(Proto.GetSkuInfoRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Proto.GetSkuInfoResponse
        {
            SkuId = request.SkuId,
            AvailableQuantity = 100
        });
    }

    public override Task<Proto.ReserveResponse> Reserve(Proto.ReserveRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Proto.ReserveResponse { Success = true });
    }

    public override Task<Proto.CommitResponse> Commit(Proto.CommitRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Proto.CommitResponse { Success = true });
    }

    public override Task<Proto.ReleaseResponse> Release(Proto.ReleaseRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Proto.ReleaseResponse { Success = true });
    }
}
