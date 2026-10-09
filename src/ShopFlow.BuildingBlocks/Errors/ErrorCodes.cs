namespace ShopFlow.BuildingBlocks.Errors;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string PaymentDeclined = "PAYMENT_DECLINED";
    public const string NotFound = "NOT_FOUND";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string OrderInProgress = "ORDER_IN_PROGRESS";
    public const string OrderExpired = "ORDER_EXPIRED";
    public const string EmailExists = "EMAIL_EXISTS";
    public const string FlashSaleOverlap = "FLASH_SALE_OVERLAP";
    public const string ProductUnavailable = "PRODUCT_UNAVAILABLE";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string ReservationLost = "RESERVATION_LOST";
    public const string InternalError = "INTERNAL_ERROR";
    public const string PaymentUnavailable = "PAYMENT_UNAVAILABLE";
}
