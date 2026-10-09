namespace ShopFlow.BuildingBlocks.Correlation;

public sealed class CorrelationContext
{
    private readonly AsyncLocal<string?> _correlationId = new();

    public string? CorrelationId
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }
}
