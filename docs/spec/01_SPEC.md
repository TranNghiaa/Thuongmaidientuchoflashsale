# 01 — Đặc tả ShopFlow (nguồn sự thật duy nhất)

> Phiên bản đã sửa các lỗi thiết kế của Guide cũ (xem §13 Errata). Các con số ở §2 là **mục tiêu giả định**, phải đo lại và điều chỉnh sau khi chạy thật.

---

## 1. Mục tiêu và phạm vi

**ShopFlow**: backend thương mại điện tử cho flash sale, Modular Monolith (6 module, mỗi module một schema), đảm bảo **không bán vượt tồn kho**, việc phụ chạy bất đồng bộ qua RabbitMQ + Outbox, chạy bằng Docker Compose sau NGINX, có đường tiến hóa tách `Inventory` thành gRPC service.

**Trong phạm vi:** đăng ký/đăng nhập (JWT + refresh), quản lý sản phẩm + ảnh (MinIO), flash sale, tồn kho + giữ hàng có TTL, đặt hàng idempotent, thanh toán giả lập, thông báo giả lập, CI/CD, load test, tách gRPC.

**Ngoài phạm vi (không làm):** giỏ hàng, vận chuyển, huỷ đơn thủ công, hoàn tiền (refund), nhiều SKU cho một sản phẩm, giới hạn mua theo từng người cho cả đợt flash sale, chống bot/captcha, đa tiền tệ, đa ngôn ngữ, giao diện web.

## 2. Mục tiêu phi chức năng (giả định, cần đo)

| Mã | Mục tiêu | Cách kiểm |
|---|---|---|
| NFR-1 | Tính đúng: 500 request đồng thời vào SKU tồn 50 → đúng 50 đơn `Paid`, `available = 0`, không âm | Integration test T13/T18, k6 T26 |
| NFR-2 | p95 `POST /orders` < 800 ms ở 200 VU, 2 instance, máy dev (ngưỡng k6) | k6 T26 |
| NFR-3 | Không mất sự kiện `OrderCreated` khi RabbitMQ hoặc consumer tạm dừng | T21–T23 |
| NFR-4 | Trùng `Idempotency-Key` không gây trừ kho/thanh toán lần hai, kể cả khi song song | T18 |
| NFR-5 | Rollback về image cũ < 5 phút | T28 |

## 3. Công nghệ và phiên bản

- **.NET 10 (LTS)**, `net10.0`. Lý do: .NET 8 hết hỗ trợ ngày 10/11/2026. Mọi chỗ trong Guide cũ ghi "8" đọc thành "10": image `mcr.microsoft.com/dotnet/sdk:10.0` và `aspnet:10.0`, EF Core 10, Npgsql EF provider 10.
- ASP.NET Core Minimal API, EF Core + Npgsql, PostgreSQL 16, RabbitMQ 3.13 (`RabbitMQ.Client` 7.x), MinIO (SDK `Minio`), Serilog, xUnit, Testcontainers, NetArchTest, NSubstitute (hoặc Moq), `Microsoft.Extensions.TimeProvider.Testing`, k6, Docker Compose, NGINX, Jenkins (GitHub Actions là phương án thay thế).
- Không hardcode số phiên bản; ghi phiên bản thực dùng vào `docs/PACKAGES.md`. Tag image Docker (đặc biệt MinIO) phải kiểm tra còn pull được khi dựng.

## 4. Kiến trúc và quy tắc phụ thuộc

Cây thư mục solution giữ nguyên Guide cũ §1, thêm: `docs/spec/`, `docs/PACKAGES.md`, `docs/OPEN_QUESTIONS.md`, `docs/evidence/`, `tools/`.

**Ma trận tham chiếu (project triển khai → Contracts được phép tham chiếu):**

| Module | Được tham chiếu |
|---|---|
| Identity | (không) |
| Catalog | (không) |
| Inventory | (không) |
| Payment | (không) |
| Ordering | Catalog.Contracts, Inventory.Contracts, Payment.Contracts (và Notification.Contracts chỉ ở T27) |
| Notification | Ordering.Contracts (chỉ để dùng record `OrderCreated`) |

Mọi module tham chiếu `ShopFlow.BuildingBlocks`. `*.Contracts` không tham chiếu project nào khác. Host tham chiếu mọi module.

Quy tắc bắt buộc: kiểu triển khai `internal`; mỗi `DbContext` gọi `HasDefaultSchema("<module>")`; không truy cập schema module khác; trong module `Domain` ← `Application` ← `Infrastructure`; test cần thấy internal dùng `InternalsVisibleTo`.

## 5. Quy ước chung

- **ID:** `Guid` (uuid). **Thời gian:** UTC, cột `timestamptz`, kiểu .NET `DateTime` với `Kind=Utc`, lấy qua `TimeProvider`.
- **Tiền:** đơn vị VND, `decimal`, cột `numeric(18,0)`. Đơn giá sau mọi quy tắc giảm giá được làm tròn `Math.Round(x, 0, MidpointRounding.AwayFromZero)`. Tổng đơn = tổng `unit_price * quantity`.
- **Tên DB:** `snake_case`. Dùng `EFCore.NamingConventions` nếu tương thích EF Core 10, nếu không thì ánh xạ tên tường minh.
- **Migration:** mỗi module có migration riêng, bảng lịch sử `__EFMigrationsHistory` nằm trong schema của module. `IModuleMigrator` chạy `CREATE SCHEMA IF NOT EXISTS <schema>` trước khi `MigrateAsync`. Chỉ container `migrator` chạy migration (cờ `--migrate`).
- **Không bật `EnableRetryOnFailure`** cho DbContext có dùng transaction thủ công (xung đột với `BeginTransaction`). Nếu cần retry, bọc bằng `CreateExecutionStrategy()`.
- **Định dạng lỗi:** RFC 7807 `ProblemDetails`, thêm các trường mở rộng `code` (chuỗi trong §9.2), `correlationId`. `type = "https://shopflow/errors/<code-kebab>"`.
- **Correlation:** header `X-Correlation-Id` (nhận hoặc tự sinh), đưa vào log Serilog và vào header message RabbitMQ `x-correlation-id`; consumer phục hồi giá trị này vào `CorrelationContext` khi xử lý.
- **Header phản hồi:** `X-Instance` = `INSTANCE_ID`.
- **Đăng ký DI/Endpoint:** mỗi module expose `Add<Module>Module(IServiceCollection, IConfiguration)` và `Map<Module>Endpoints(IEndpointRouteBuilder)`.

## 6. Dữ liệu (một database, nhiều schema)

Mọi bảng dưới đây là bảng **cuối cùng** (đã gộp thay đổi so với Guide cũ).

**identity**
- `users`: `id` PK, `email` text unique (lưu chữ thường), `password_hash`, `role` CHECK IN (`User`,`Admin`), `created_at`.
- `refresh_tokens`: `id` PK, `user_id`, `token_hash` unique (SHA-256), `expires_at`, `revoked_at` null, `created_at`; index `user_id`.

**catalog** (một sản phẩm = một SKU)
- `products`: `id` PK, `sku_id` unique (do server sinh), `name`, `description`, `base_price` numeric(18,0) CHECK > 0, `image_key` null, `is_active`, `created_at`, `updated_at`.
- `flash_sales`: `id` PK, `sku_id`, `sale_price` numeric(18,0) CHECK > 0, `starts_at`, `ends_at`, CHECK `ends_at > starts_at`. **Khuyến nghị:** chặn chồng khung giờ cùng SKU ở mức DB bằng exclusion constraint `EXCLUDE USING gist (sku_id WITH =, tstzrange(starts_at, ends_at) WITH &&)` (cần `CREATE EXTENSION IF NOT EXISTS btree_gist`, viết bằng SQL thô trong migration). Ngoài ra vẫn validate ở tầng ứng dụng.

**inventory**
- `stock_items`: `sku_id` PK, `available` int **CHECK ≥ 0**, `updated_at`.
- `reservations`: `reservation_id`, `sku_id`, `order_id`, `quantity` CHECK > 0, `status` CHECK IN (`Held`,`Committed`,`Released`), `expires_at`, `created_at`; **PK (reservation_id, sku_id)**; index một phần `(status, expires_at) WHERE status = 'Held'`; index `order_id`.
- *Không còn* `processed_messages` ở Inventory (Inventory không consume sự kiện nữa, xem §8.3).

**ordering**
- `orders`: `id` PK, `user_id`, `status` CHECK IN (`Pending`,`Paid`,`Failed`), `failure_reason` null (`OUT_OF_STOCK`,`PAYMENT_DECLINED`,`EXPIRED`,`RESERVATION_LOST`), `total` numeric(18,0), `idempotency_key` varchar(64), `request_hash` char(64), `reservation_id` uuid, `created_at`, `updated_at`, `paid_at` null; **unique (user_id, idempotency_key)**; index `(user_id, created_at DESC)`; index một phần `(created_at) WHERE status = 'Pending'`.
- `order_lines`: `id` PK, `order_id` FK, `sku_id`, `quantity` CHECK > 0, `unit_price`.
- `outbox_messages`: `id` PK (cũng là MessageId), `type`, `payload` jsonb, `correlation_id` null, `occurred_at`, `processed_at` null; index một phần `(occurred_at) WHERE processed_at IS NULL`.

**payment**
- `payments`: `id` PK, `order_id` **unique**, `amount`, `status` CHECK IN (`Succeeded`,`Failed`), `provider_ref` null, `error` null, `created_at`.

**notification**
- `notifications`: `id` PK, `user_id`, `order_id`, `channel`, `content`, `created_at`; unique `(order_id, channel)`.
- `processed_messages`: `message_id` PK, `processed_at`.

## 7. Hợp đồng giữa các module (Contracts, bản cuối)

```csharp
// ShopFlow.Modules.Inventory.Contracts
public sealed record ReserveLine(Guid SkuId, int Quantity);
public sealed record ReserveRequest(Guid ReservationId, Guid OrderId,
                                    IReadOnlyList<ReserveLine> Lines, TimeSpan Ttl);
public sealed record ReserveResult(bool Success, string? Reason, Guid? FailedSkuId)
{
    public static ReserveResult Ok() => new(true, null, null);
    public static ReserveResult OutOfStock(Guid sku) => new(false, "OUT_OF_STOCK", sku);
}
public interface IInventoryService
{
    // Idempotent theo ReservationId: gọi lại cùng id trả Ok, không trừ kho lần hai.
    Task<ReserveResult> ReserveAsync(ReserveRequest request, CancellationToken ct);
    // true nếu reservation đang/đã Committed. false nếu đã Released hoặc không tồn tại.
    Task<bool> CommitAsync(Guid reservationId, CancellationToken ct);
    // Idempotent: chỉ hoàn kho nếu reservation đang Held; không tồn tại thì không làm gì.
    Task ReleaseAsync(Guid reservationId, CancellationToken ct);
}

// ShopFlow.Modules.Catalog.Contracts
public sealed record SkuPrice(Guid SkuId, decimal BasePrice, decimal EffectivePrice, bool IsFlashSale);
public interface ICatalogService
{
    // Chỉ trả các SKU của sản phẩm đang active. SKU thiếu trong kết quả = không khả dụng.
    Task<IReadOnlyList<SkuPrice>> GetPricesAsync(IEnumerable<Guid> skuIds, DateTime atUtc, CancellationToken ct);
}

// ShopFlow.Modules.Payment.Contracts
public sealed record ChargeRequest(Guid OrderId, decimal Amount, string IdempotencyKey);
public sealed record ChargeResult(bool Success, string? ProviderRef, string? Error);
public sealed record PaymentInfo(bool Success, string? ProviderRef);
public interface IPaymentService
{
    // Idempotent theo OrderId: đã có bản ghi thì trả lại kết quả cũ, không gọi cổng lần hai.
    Task<ChargeResult> ChargeAsync(ChargeRequest r, CancellationToken ct);
    Task<PaymentInfo?> GetByOrderAsync(Guid orderId, CancellationToken ct);
}

// ShopFlow.Modules.Ordering.Contracts
// Ngữ nghĩa: sự kiện CHỈ được phát khi đơn chuyển sang Paid.
public sealed record OrderCreated(Guid OrderId, Guid UserId, Guid ReservationId, decimal Total,
    IReadOnlyList<OrderCreatedLine> Lines, DateTime OccurredAtUtc);
public sealed record OrderCreatedLine(Guid SkuId, int Quantity, decimal UnitPrice);

// ShopFlow.Modules.Notification.Contracts (chỉ dùng ở T27)
public interface INotificationService
{
    Task NotifyOrderPaidAsync(OrderCreated evt, CancellationToken ct);
}
```

## 8. Quy tắc nghiệp vụ

### 8.1 Identity
- `POST /auth/register {email, password}`: email chuẩn hóa chữ thường, hợp lệ; mật khẩu ≥ 8 ký tự. Luôn tạo role `User` (client **không** được chọn role). Trùng email → `409 EMAIL_EXISTS`.
- `POST /auth/login`: sai email hoặc mật khẩu → `401 INVALID_CREDENTIALS` (cùng một thông báo cho cả hai). Thành công trả `{accessToken, refreshToken, expiresIn}`.
- Mật khẩu băm bằng BCrypt (`BCrypt.Net-Next`). Access token 15 phút, claim: `sub`, `email`, `role`. Refresh token: 32 byte ngẫu nhiên base64url, **chỉ lưu SHA-256**, hạn 7 ngày.
- `POST /auth/refresh {refreshToken}`: **xoay vòng**: token cũ bị thu hồi, cấp cặp mới. Nếu dùng lại token đã thu hồi → thu hồi **toàn bộ** refresh token của user đó và trả `401 INVALID_REFRESH_TOKEN`.
- `POST /auth/logout {refreshToken}`: thu hồi token đó (idempotent, luôn `204`).
- Admin đầu tiên được tạo bởi `--seed` từ cấu hình `Admin:Email`/`Admin:Password` (idempotent: đã có thì bỏ qua).
- Policy `AdminOnly` = role `Admin`. Cấu hình xác thực JWT đặt trong BuildingBlocks (`AddShopFlowJwtAuth`) để Inventory Service ở T29 dùng lại.

### 8.2 Catalog và flash sale
- Tạo sản phẩm (`POST /products`, Admin): server sinh `id` và `sku_id`. `base_price > 0`. `PUT /products/{id}` cập nhật tên/mô tả/giá/`is_active`.
- `GET /products`: chỉ trả `is_active = true` cho người không phải Admin. Phân trang `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100), lọc `q` (tên chứa, không phân biệt hoa thường). Trả `{items, page, pageSize, total}`.
- `POST /admin/flash-sales {skuId, salePrice, startsAt, endsAt}`: SKU phải tồn tại; `0 < salePrice < base_price`; `startsAt < endsAt`; `endsAt > now`; không chồng khung giờ với flash sale khác cùng SKU → `409 FLASH_SALE_OVERLAP`.
- **Giá hiệu lực** tại thời điểm `t`: nếu có flash sale với `starts_at <= t < ends_at` thì `EffectivePrice = sale_price`, `IsFlashSale = true`; ngược lại `EffectivePrice = BasePrice`.
- Ảnh: key dạng `products/{productId}/{guid}{ext}`; chỉ nhận `image/jpeg`, `image/png`, `image/webp`; presigned PUT và GET hết hạn sau 10 phút. `PUT /products/{id}/image {key}` yêu cầu key bắt đầu bằng `products/{id}/`. Bucket `product-images` được tạo bởi bước seed (không tạo lúc request).

### 8.3 Inventory
- `PUT /admin/inventory/{skuId} {available}` (Admin): upsert tồn kho, `available ≥ 0`. `GET /admin/inventory/{skuId}` trả `{skuId, available, held, committed}` (tính từ `reservations`).
- **Reserve** (nguyên tử, không khóa dài), một transaction:
  1. Nếu đã có reservation với `ReservationId` đó → trả `Ok` ngay (idempotent).
  2. Sắp xếp dòng theo `SkuId` (mọi giao dịch khóa theo cùng thứ tự để tránh deadlock).
  3. Mỗi dòng: `UPDATE inventory.stock_items SET available = available - @q WHERE sku_id = @s AND available >= @q`. 0 hàng bị ảnh hưởng (hết hàng **hoặc** chưa có dòng tồn kho) → rollback, `ChangeTracker.Clear()`, trả `OutOfStock(sku)`.
  4. Thêm `reservations` (status `Held`, `expires_at = now + Ttl`), commit.
  - Giữ transaction ngắn: toàn bộ request đồng thời vào cùng SKU bị tuần tự hóa trên khóa hàng, nên đây là điểm nghẽn có chủ đích. Phải đo (T26) và ghi vào ADR-002.
- **Commit:** `UPDATE reservations SET status='Committed' WHERE reservation_id=@id AND status='Held'`. Có hàng bị cập nhật → `true`. Không có: nếu đã `Committed` → `true`; còn lại (`Released`/không tồn tại) → `false`. Commit thành công kể cả khi `expires_at` đã qua nhưng chưa bị sweeper dọn.
- **Release:** idempotent như Guide cũ §4.1 (CTE chỉ hoàn kho khi status đang `Held`).
- **ReservationSweeper:** mỗi `Inventory:SweeperIntervalSeconds` giây, lấy tối đa 100 reservation `Held` đã quá hạn và gọi `ReleaseAsync`. An toàn khi chạy nhiều instance nhờ Release idempotent.
- **Quyết định thiết kế quan trọng (sửa lỗi Guide cũ):** `Commit` được gọi **đồng bộ** trong `PlaceOrder` ngay sau khi thanh toán thành công, **không** qua consumer. Lý do: nếu commit phụ thuộc consumer, consumer chậm quá TTL sẽ khiến sweeper hoàn kho cho đơn đã thanh toán và gây bán vượt. Hệ quả: Inventory không consume sự kiện nào.

### 8.4 Ordering

**Trạng thái đơn:** `Pending → Paid` hoặc `Pending → Failed(reason)`. Trạng thái cuối (`Paid`, `Failed`) không đổi nữa. Mọi chuyển trạng thái phải là **cập nhật có điều kiện** `... WHERE id=@id AND status='Pending'` (kiểm tra số hàng bị ảnh hưởng), để handler và reaper không ghi đè nhau.

**Giới hạn đầu vào của `POST /orders`:** header `Idempotency-Key` bắt buộc, 8–64 ký tự `[A-Za-z0-9_-]`; body `{items:[{skuId, quantity}]}` với 1 ≤ số dòng ≤ `Order:MaxLines`, `1 ≤ quantity ≤ Order:MaxQuantityPerLine`, `skuId` không trùng nhau. Vi phạm → `400 VALIDATION_ERROR`.

**Giá:** `FlashSaleRule` (Order 10) thay giá bằng giá flash nếu `IsFlashSale`. `BulkDiscountRule` (Order 20) giảm `Order:BulkDiscountRate` khi `quantity >= Order:BulkThreshold` **và không áp dụng cho SKU đang flash sale**. Làm tròn theo §5.

**Luồng `PlaceOrder` (bản cuối):**

```
P0  Validate đầu vào (bảng trên). requestHash = SHA-256 của JSON chuẩn hóa items (sắp theo skuId).
P1  Tìm đơn theo (userId, idempotencyKey). Có → ReplayOrReject(existing).
P2  prices = Catalog.GetPrices(...). Thiếu SKU nào → 422 PRODUCT_UNAVAILABLE (chưa lưu đơn).
P3  lines = PriceCalculator; order = Pending, id mới, reservationId = Guid mới (lưu sẵn trên đơn).
P4  INSERT order + lines. Vi phạm unique (user_id, idempotency_key) (Postgres 23505)
    → tải đơn đã có → ReplayOrReject. Lỗi DbUpdateException khác → ném lại, KHÔNG coi là trùng.
P5  Inventory.Reserve(reservationId, orderId, lines, TTL).
    Thất bại → chuyển Failed(OUT_OF_STOCK) → 409 OUT_OF_STOCK.
P6  Payment.Charge(orderId, total, idempotencyKey = orderId).
    - Thất bại (declined) → Inventory.Release(reservationId); Failed(PAYMENT_DECLINED) → 402.
    - Ném exception (timeout...) → trạng thái thanh toán KHÔNG rõ: không Release, để đơn Pending
      cho PendingOrderReaper xử lý; trả 503.
P7  Inventory.Commit(reservationId). false → Failed(RESERVATION_LOST), log Critical
    (đã thu tiền nhưng mất hàng, xem §14) → 500 RESERVATION_LOST.
P8  Một transaction: cập nhật có điều kiện Pending→Paid (paid_at) + INSERT outbox OrderCreated.
    Số hàng cập nhật = 0 (reaper đã chuyển trước) → tải lại đơn, trả theo ReplayOrReject.
P9  Trả 201.
```

**ReplayOrReject(existing):** `request_hash` khác → `422 IDEMPOTENCY_KEY_REUSED`. Cùng hash: `Paid` → `200` (đơn cũ); `Failed` → lặp lại đúng mã lỗi gốc (`OUT_OF_STOCK`→409, `PAYMENT_DECLINED`→402, `EXPIRED`→409 `ORDER_EXPIRED`, `RESERVATION_LOST`→500); `Pending` → `409 ORDER_IN_PROGRESS` kèm header `Retry-After: 2`. Mọi phản hồi replay có header `Idempotent-Replayed: true`.

**PendingOrderReaper** (background, chạy mỗi 30 giây): với đơn `Pending` có `created_at < now - Order:PendingTimeoutMinutes`:
1. `payment = Payment.GetByOrder(orderId)`.
2. Có thanh toán thành công → `Commit(reservationId)`; `true` → chuyển Paid + outbox (như P8); `false` → Failed(`RESERVATION_LOST`) + log Critical.
3. Không có thanh toán thành công → `Release(reservationId)` rồi Failed(`EXPIRED`).

**Ràng buộc cấu hình bắt buộc:** `Order:PendingTimeoutMinutes` (mặc định 3) **phải nhỏ hơn** `Inventory:ReservationTtlMinutes` (mặc định 10), kiểm tra lúc khởi động và ném lỗi nếu vi phạm. Nếu không, reaper có thể "cứu" một đơn đã thanh toán mà kho đã bị sweeper hoàn. Ngoài ra đặt timeout toàn request ≤ 30 giây.

**Outbox publisher:** như Guide cũ §5.4 (`FOR UPDATE SKIP LOCKED`, lô 50) nhưng **bật publisher confirms** và chỉ đánh dấu `processed_at` sau khi broker xác nhận. Ngữ nghĩa *at-least-once*: có thể phát lặp, consumer phải idempotent.

### 8.5 Payment
- `ChargeAsync`: tra `payments` theo `order_id`; có rồi → trả kết quả cũ. Chưa có → gọi `IPaymentGateway`, ghi bản ghi; nếu chạm unique `order_id` (23505) → tải bản ghi và trả kết quả đó.
- `FakeGateway`: trễ ngẫu nhiên 50–150 ms (cấu hình được), thất bại theo `Payment:FailRate` (mã `DECLINED`).
- Không có refund trong phạm vi (xem §14).

### 8.6 Notification
- Consumer queue `notification.order-created`. Xử lý trong **một transaction DB**: (1) `INSERT processed_messages ... ON CONFLICT DO NOTHING`, 0 hàng → coi là trùng, ack bỏ qua; (2) `INSERT notifications`; (3) gọi `INotificationChannel.SendAsync` (FakeEmail ghi log, độ trễ giả lập `Notification:SimulatedLatencyMs`); (4) commit; (5) ack.
- Đánh đổi (ghi vào ADR-003): gửi **trước** commit nên có thể gửi trùng hiếm khi commit lỗi, nhưng không bao giờ mất thông báo.
- Exception không thể retry (JSON hỏng) → nack không requeue ngay (vào DLQ), không retry.
- Thêm kênh mới = thêm class implement `INotificationChannel`, không sửa consumer.

## 9. API

### 9.1 Danh sách

| Method | Path | Quyền | Ghi chú |
|---|---|---|---|
| POST | `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout` | Public | §8.1 |
| GET | `/products`, `/products/{id}` | Public | `GET /{id}` kèm presigned GET URL (10 phút) |
| POST/PUT | `/products`, `/products/{id}` | Admin | |
| POST | `/products/{id}/image-upload-url` | Admin | Body `{fileName, contentType}` → `{key, uploadUrl}` |
| PUT | `/products/{id}/image` | Admin | Body `{key}` |
| POST | `/admin/flash-sales` | Admin | |
| PUT/GET | `/admin/inventory/{skuId}` | Admin | |
| POST | `/orders` | User | Header `Idempotency-Key` bắt buộc |
| GET | `/orders`, `/orders/{id}` | User | Chỉ đơn của mình; đơn người khác → 404. `/orders` phân trang như `/products`, mới nhất trước |
| GET | `/health/live`, `/health/ready` | Public | `ready` kiểm tra Postgres và RabbitMQ |

Phản hồi 201/200 của `POST /orders` và `GET /orders/{id}`: `{orderId, status, failureReason, total, createdAt, paidAt, lines:[{skuId, quantity, unitPrice}]}`.

### 9.2 Mã lỗi

| HTTP | `code` | Khi nào |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Thiếu/sai header hoặc body |
| 401 | `INVALID_CREDENTIALS`, `INVALID_REFRESH_TOKEN` | §8.1 |
| 402 | `PAYMENT_DECLINED` | Cổng từ chối |
| 404 | `NOT_FOUND` | Không có, hoặc không thuộc về bạn |
| 409 | `OUT_OF_STOCK` | Hết hàng hoặc không đủ |
| 409 | `ORDER_IN_PROGRESS` | Cùng key đang xử lý (có `Retry-After`) |
| 409 | `ORDER_EXPIRED` | Replay đơn đã bị reaper đánh hỏng; dùng key mới |
| 409 | `EMAIL_EXISTS`, `FLASH_SALE_OVERLAP` | |
| 422 | `PRODUCT_UNAVAILABLE`, `IDEMPOTENCY_KEY_REUSED` | |
| 500 | `RESERVATION_LOST`, `INTERNAL_ERROR` | |
| 503 | `PAYMENT_UNAVAILABLE` | Exception ở bước thanh toán, đơn để Pending |

Mã thành công của `POST /orders`: `201` tạo mới và đã `Paid`; `200` replay đơn `Paid`.

## 10. Messaging

- Exchange `order.events` (fanout, durable). Exchange chết `order.events.dlx` (fanout) → queue `order.events.dead`.
- Message: `MessageId = outbox.id`, `Type = "OrderCreated"`, `DeliveryMode = Persistent`, `ContentType = application/json`, header `x-correlation-id`.
- Queue của consumer: `notification.order-created` (durable, `x-dead-letter-exchange = order.events.dlx`), bind vào `order.events`.
- Consumer: `prefetch = 10`, thủ công ack, retry trong tiến trình tối đa 3 lần (chờ 2 s, 4 s) rồi nack không requeue. Kết nối dùng automatic recovery; lúc khởi động thử lại tối đa 20 lần cách 3 giây.
- Publisher: bật publisher confirms.
- Đổi tham số queue đã tồn tại gây `PRECONDITION_FAILED`: xóa queue cũ hoặc `docker compose down -v` (chỉ môi trường dev).

## 11. Cấu hình

| Khóa | Mặc định | Ý nghĩa |
|---|---|---|
| `ConnectionStrings:Default` | | Postgres chính |
| `ConnectionStrings:Inventory` | | Chỉ ở chế độ gRPC (DB riêng) |
| `RabbitMq:Uri` | | |
| `Messaging:Enabled` | `true` | `false` = không đăng ký publisher/consumer (dùng cho test) |
| `Notification:ConsumerEnabled` | `true` | Tắt consumer để thử chịu lỗi |
| `Notification:SimulatedLatencyMs` | `0` | |
| `Benchmark:InlineNotification` | `false` | Chỉ ở T27 |
| `Minio:Endpoint` / `PublicEndpoint` / `AccessKey` / `SecretKey` / `Bucket` | / `product-images` | Endpoint nội bộ khác endpoint public (presigned URL ký theo host) |
| `Jwt:Secret` (≥ 32 ký tự), `Jwt:Issuer`, `Jwt:Audience` | | Secret khác nhau mỗi môi trường |
| `Admin:Email`, `Admin:Password` | | Dùng cho seed |
| `Inventory:Mode` | `InProcess` | `InProcess` hoặc `Grpc` |
| `Inventory:GrpcUrl` | | |
| `Inventory:ReservationTtlMinutes` | `10` | |
| `Inventory:SweeperIntervalSeconds` | `10` | |
| `Order:PendingTimeoutMinutes` | `3` | Phải < TTL |
| `Order:MaxLines` / `MaxQuantityPerLine` | `10` / `10` | |
| `Order:BulkThreshold` / `BulkDiscountRate` | `5` / `0.05` | |
| `Payment:FailRate` | `0.0` | 0..1 |
| `Payment:SimulatedLatencyMinMs` / `MaxMs` | `50` / `150` | |
| `INSTANCE_ID` | | Header `X-Instance` |

## 12. Nghiệm thu end-to-end (kiểm ở T31)

| ID | Kịch bản | Kỳ vọng | Test ở |
|---|---|---|---|
| E1 | 500 request đồng thời, tồn 50 | Đúng 50 đơn `Paid`, `available = 0` | T13, T18, T26 |
| E2 | Cùng `Idempotency-Key` hai lần (tuần tự) | Một đơn, một lần trừ kho, một lần thanh toán; lần hai `200` + `Idempotent-Replayed` | T18 |
| E3 | Cùng key, 20 request song song | Đúng một `201`; payments = 1; kho giảm 1 | T18 |
| E4 | Cùng key, body khác | `422 IDEMPOTENCY_KEY_REUSED` | T18 |
| E5 | Thanh toán lỗi (`FailRate=1`) | `402`, kho được hoàn, replay vẫn `402` | T18 |
| E6 | Đơn Pending bị bỏ rơi | Reaper dọn đúng theo §8.4 | T19 |
| E7 | Tắt consumer, đặt đơn, bật lại | Thông báo được xử lý sau đó; không trùng | T23 |
| E8 | Tắt RabbitMQ, đặt đơn, bật lại | Đơn vẫn `201`; sự kiện được phát sau | T23 |
| E9 | Message hỏng | Vào `order.events.dead` | T22 |
| E10 | Hai instance qua NGINX | `X-Instance` luân phiên | T24 |
| E11 | Vi phạm ranh giới module | Architecture test đỏ | T05 |
| E12 | Test đỏ | Pipeline dừng, không build image | T28 |
| E13 | Chạy ở cả hai chế độ `InProcess` và `Grpc` | Cùng kết quả E1–E3 | T30 |
| E14 | `.env` thật không có trong Git | `git log -p` chỉ thấy `.env.example` | T31 |

## 13. Errata so với Guide cũ

| Vị trí Guide cũ | Vấn đề | Sửa trong SPEC |
|---|---|---|
| §5.2 PlaceOrder | Mỗi request tự sinh `Order.Id`, nên unique `payments.order_id` không chặn thu tiền trùng khi 2 request cùng key chạy song song; request thua không được hoàn tiền | Tạo đơn `Pending` trước (unique key chọn người thắng), chỉ người thắng mới reserve/charge (§8.4 P3–P6) |
| §5.2, §4.3 | Commit kho phụ thuộc consumer; consumer chậm quá TTL → sweeper hoàn kho cho đơn đã trả tiền → bán vượt | Commit đồng bộ, Inventory không consume (§8.3) |
| §4.3 | `processed_messages` và nghiệp vụ ở hai transaction | Cùng một transaction (§8.6) |
| §5.4, §6.1 | "At-least-once" chưa đúng vì thiếu publisher confirms | Bật confirms (§8.4, §10) |
| §5.2 | `catch (DbUpdateException)` coi mọi lỗi là trùng key | Chỉ xử lý 23505 trên unique `(user_id, idempotency_key)` |
| §6.2 | Consumer không xử lý rớt kết nối, không khôi phục correlation ID, JSON hỏng vẫn retry | §10, §8.6 |
| §4.1 | Sau rollback, entity `Reservation` vẫn nằm trong ChangeTracker | `ChangeTracker.Clear()` (§8.3) |
| §12.2 compose | `environment` ở api1/api2 ghi đè cả khối, mất connection string | Gộp bằng anchor mức khóa (`<<: *api-env`), xem T04 |
| §13.4 k6 | k6 mặc định coi 4xx là lỗi nên ngưỡng `http_req_failed` luôn đỏ vì 409 là kết quả mong đợi; `setup()` đăng ký 200 user có thể vượt 60 s | `http.setResponseCallback(http.expectedStatuses(201, 409))`, `setupTimeout`, đăng ký theo lô (T26) |
| §2, §12.1 | .NET 8 hết hỗ trợ 10/11/2026 | .NET 10 (§3) |
| §5.1, §11 | Trạng thái `Cancelled` không dùng; đơn Pending không có đường thoát | Bỏ `Cancelled`; thêm reaper (§8.4) |

## 14. Giới hạn đã biết (phải ghi vào README/ADR)

- Không có refund: nếu `Commit` trả `false` sau khi đã thu tiền (hiếm, vì TTL ≫ thời gian xử lý), đơn thành `Failed(RESERVATION_LOST)` và chỉ có log Critical. Cần quy trình đối soát thủ công.
- Điểm nghẽn tồn kho: mọi request vào cùng SKU tuần tự hóa trên một khóa hàng. Chấp nhận được cho phạm vi đề tài; phải đo và nêu trong báo cáo.
- Sau khi tách gRPC (T29–T30), giao dịch cục bộ không còn bao trùm Inventory và Ordering; tính đúng dựa vào `ReservationId` idempotent, TTL, `Release` bù trừ và reaper.
- Thông báo có thể gửi trùng hiếm khi (§8.6).
