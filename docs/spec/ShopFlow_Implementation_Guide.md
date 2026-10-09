# ShopFlow – Hướng dẫn triển khai chi tiết

> **Lưu ý cập nhật công nghệ:** Solution đã được nâng cấp và triển khai thực tế trên **.NET 10 (`net10.0`)**, sử dụng **MassTransit 8.3.4** với RabbitMQ 3.13, **AWS SDK for .NET S3 (`AWSSDK.S3` 3.7.411.3)** cho MinIO, **EF Core 10**, và **gRPC 2.84.0** cho Dual-Mode Inventory. Danh sách phiên bản gói chi tiết được quản lý tập trung tại [`Directory.Packages.props`](file:///d:/Maychu/Directory.Packages.props) và [`docs/PACKAGES.md`](file:///d:/Maychu/docs/PACKAGES.md).

---

## 0. Tóm tắt đề tài

**ShopFlow** là backend thương mại điện tử cho flash sale, xây theo **Modular Monolith** (6 module, mỗi module một schema riêng), đảm bảo **không bán vượt tồn kho**, xử lý việc phụ bằng **RabbitMQ + Outbox (MassTransit)**, đóng gói bằng **Docker Compose**, chia tải bằng **NGINX**, và có đường tiến hóa tách `Inventory` thành **gRPC microservice**.

**Công nghệ sử dụng:**
- **Nền tảng & Framework:** .NET 10 (LTS, `net10.0`), ASP.NET Core Minimal API.
- **Cơ sở dữ liệu & ORM:** PostgreSQL 16, Entity Framework Core 10 (`Microsoft.EntityFrameworkCore` 10.0.12, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3), Schema-per-module isolation.
- **Message Broker & Outbox:** RabbitMQ 3.13, MassTransit 8.3.4 (`MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`).
- **Object Storage:** MinIO S3-compatible, AWS SDK for .NET S3 (`AWSSDK.S3` 3.7.411.3).
- **Giao tiếp liên dịch vụ & Tiến hóa Microservice:** gRPC for .NET 2.84.0 (`Grpc.AspNetCore`, `Grpc.Net.Client`, `Grpc.Tools`, `Google.Protobuf` 3.36.2) hỗ trợ Dual-Mode (InProcess hoặc gRPC).
- **Xác thực & Bảo mật:** JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12), Password Hashing (`BCrypt.Net-Next` 4.2.0).
- **Ghi log & Giám sát:** Serilog (`Serilog.AspNetCore` 10.0.0), Health Checks (`AspNetCore.HealthChecks.NpgSql`, `AspNetCore.HealthChecks.Rabbitmq`, `AspNetCore.HealthChecks.Aws.S3` v9.0.0).
- **Tài liệu API:** OpenAPI / Swagger UI (`Swashbuckle.AspNetCore` 10.3.0, `Microsoft.AspNetCore.OpenApi` 10.0.12).
- **Kiểm thử & Chất lượng mã nguồn:** xUnit 2.9.3, NSubstitute 6.2.0, Testcontainers.PostgreSql 4.16.0, NetArchTest.Rules 1.3.2, coverlet.collector 6.0.4.
- **Điều phối & Tải:** Docker Compose, NGINX Alpine (Reverse Proxy & Load Balancer cho 2 instances API), k6 (load test).

---

## 1. Cấu trúc solution

```
shopflow/
├─ src/
│  ├─ ShopFlow.Host/                      # Composition root, Program.cs, middleware
│  ├─ ShopFlow.BuildingBlocks/            # Messaging, Outbox helper, Correlation, Result
│  └─ Modules/
│     ├─ Identity/     ShopFlow.Modules.Identity.Contracts   +  ShopFlow.Modules.Identity
│     ├─ Catalog/      ShopFlow.Modules.Catalog.Contracts    +  ShopFlow.Modules.Catalog
│     ├─ Inventory/    ShopFlow.Modules.Inventory.Contracts  +  ShopFlow.Modules.Inventory
│     ├─ Ordering/     ShopFlow.Modules.Ordering.Contracts   +  ShopFlow.Modules.Ordering
│     ├─ Payment/      ShopFlow.Modules.Payment.Contracts    +  ShopFlow.Modules.Payment
│     └─ Notification/ ShopFlow.Modules.Notification.Contracts + ShopFlow.Modules.Notification
├─ tests/
│  ├─ ShopFlow.ArchitectureTests/
│  ├─ ShopFlow.Ordering.UnitTests/
│  └─ ShopFlow.Inventory.IntegrationTests/   # Testcontainers (Postgres)
├─ deploy/
│  ├─ docker-compose.yml
│  ├─ nginx/nginx.conf
│  └─ .env.example
├─ loadtest/oversell.js
├─ docs/adr/                              # Architecture Decision Records
├─ Dockerfile
├─ .dockerignore
├─ Jenkinsfile
└─ ShopFlow.sln
```

### Quy ước thư mục bên trong mỗi module (project không-Contracts)

```
ShopFlow.Modules.Ordering/
├─ Domain/           # Entity, value object, domain event (không phụ thuộc EF/ASP.NET)
├─ Application/      # Use case/service, interface cổng (IPriceCalculator...)
├─ Infrastructure/   # DbContext, EF config, repository, outbox publisher, consumer
├─ Endpoints/        # Minimal API map
└─ OrderingModule.cs # AddOrderingModule(), MapOrderingEndpoints()
```

### Quy tắc phụ thuộc (bắt buộc)

1. Project module **chỉ được tham chiếu `*.Contracts` của module khác**, không bao giờ tham chiếu project triển khai của module khác. Đây là rào chắn ở thời điểm biên dịch.
2. Mọi kiểu triển khai (`InventoryService`, `OrderingDbContext`...) là `internal`. Chỉ `Contracts` và lớp `XModule` là `public`.
3. Module không đọc/ghi bảng của module khác. Mỗi `DbContext` gọi `HasDefaultSchema("<tên module>")`.
4. Trong một module: `Domain` ← `Application` ← `Infrastructure` (mũi tên chỉ chiều phụ thuộc, Domain không biết ai).

---

## 2. Thiết kế dữ liệu (một database, nhiều schema)

| Schema | Bảng | Cột chính |
|---|---|---|
| `identity` | `users` | id, email (unique), password_hash, role, created_at |
| | `refresh_tokens` | id, user_id, token_hash, expires_at, revoked_at |
| `catalog` | `products` | id, sku_id (unique), name, description, base_price, image_key, is_active |
| | `flash_sales` | id, sku_id, sale_price, starts_at, ends_at |
| `inventory` | `stock_items` | sku_id (PK), available int (CHECK ≥ 0), updated_at |
| | `reservations` | reservation_id, sku_id, order_id, quantity, status (Held/Committed/Released), expires_at — PK (reservation_id, sku_id) |
| | `processed_messages` | message_id (PK), processed_at |
| `ordering` | `orders` | id, user_id, status, total, idempotency_key, created_at — unique (user_id, idempotency_key) |
| | `order_lines` | id, order_id, sku_id, quantity, unit_price |
| | `outbox_messages` | id, type, payload jsonb, occurred_at, processed_at |
| `payment` | `payments` | id, order_id (unique), amount, status, provider_ref, created_at |
| `notification` | `notifications` | id, user_id, order_id, channel, content, created_at |
| | `processed_messages` | message_id (PK), processed_at |

Ràng buộc cứng ở DB (`CHECK (available >= 0)`) là lớp phòng thủ cuối cùng chống bán vượt, bên cạnh logic ứng dụng.

---

## 3. Hợp đồng giữa các module (Contracts)

```csharp
// ShopFlow.Modules.Inventory.Contracts
namespace ShopFlow.Modules.Inventory.Contracts;

public sealed record ReserveLine(Guid SkuId, int Quantity);
public sealed record ReserveRequest(Guid OrderId, IReadOnlyList<ReserveLine> Lines, TimeSpan Ttl);
public sealed record ReserveResult(bool Success, Guid? ReservationId, string? Reason)
{
    public static ReserveResult Ok(Guid id) => new(true, id, null);
    public static ReserveResult Fail(string reason) => new(false, null, reason);
}

public interface IInventoryService
{
    Task<ReserveResult> ReserveAsync(ReserveRequest request, CancellationToken ct);
    Task CommitAsync(Guid reservationId, CancellationToken ct);
    Task ReleaseAsync(Guid reservationId, CancellationToken ct);
}
```

```csharp
// ShopFlow.Modules.Catalog.Contracts
public sealed record SkuPrice(Guid SkuId, decimal BasePrice, decimal EffectivePrice, bool IsFlashSale);
public interface ICatalogService
{
    Task<IReadOnlyList<SkuPrice>> GetPricesAsync(IEnumerable<Guid> skuIds, DateTime atUtc, CancellationToken ct);
}
```

```csharp
// ShopFlow.Modules.Payment.Contracts
public sealed record ChargeRequest(Guid OrderId, decimal Amount, string IdempotencyKey);
public sealed record ChargeResult(bool Success, string? ProviderRef, string? Error);
public interface IPaymentService { Task<ChargeResult> ChargeAsync(ChargeRequest r, CancellationToken ct); }
```

```csharp
// ShopFlow.Modules.Ordering.Contracts  (event phát ra ngoài)
public sealed record OrderCreated(
    Guid OrderId, Guid UserId, Guid ReservationId, decimal Total,
    IReadOnlyList<OrderCreatedLine> Lines, DateTime OccurredAtUtc);
public sealed record OrderCreatedLine(Guid SkuId, int Quantity, decimal UnitPrice);
```

Hợp đồng sự kiện là một phần của `Contracts`: consumer ở module khác chỉ phụ thuộc vào record này.

---

## 4. Module Inventory – trái tim chống bán vượt

### 4.1 Reserve (nguyên tử, không khóa dài)

Ý tưởng: trừ `available` bằng một câu `UPDATE` có điều kiện. PostgreSQL khóa hàng trong lúc update, nên hai request đồng thời không thể cùng thấy "còn hàng" rồi cùng trừ.

```csharp
// ShopFlow.Modules.Inventory/Infrastructure/InventoryService.cs
internal sealed class InventoryService(InventoryDbContext db) : IInventoryService
{
    public async Task<ReserveResult> ReserveAsync(ReserveRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservationId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow + req.Ttl;

        // Sắp xếp theo SkuId để mọi giao dịch khóa hàng theo cùng thứ tự => tránh deadlock
        foreach (var line in req.Lines.OrderBy(l => l.SkuId))
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE inventory.stock_items
                   SET available = available - {line.Quantity}, updated_at = now()
                 WHERE sku_id = {line.SkuId} AND available >= {line.Quantity}", ct);

            if (affected == 0)
            {
                await tx.RollbackAsync(ct);
                return ReserveResult.Fail($"Hết hàng hoặc không đủ số lượng: {line.SkuId}");
            }

            db.Reservations.Add(new Reservation(reservationId, line.SkuId, req.OrderId,
                                                line.Quantity, expiresAt));
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ReserveResult.Ok(reservationId);
    }

    public Task CommitAsync(Guid reservationId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE inventory.reservations SET status = 'Committed'
             WHERE reservation_id = {reservationId} AND status = 'Held'", ct);

    // Release idempotent: chỉ hoàn kho nếu reservation đang ở trạng thái Held
    public Task ReleaseAsync(Guid reservationId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($@"
            WITH r AS (
                UPDATE inventory.reservations SET status = 'Released'
                 WHERE reservation_id = {reservationId} AND status = 'Held'
             RETURNING sku_id, quantity)
            UPDATE inventory.stock_items s
               SET available = s.available + r.quantity, updated_at = now()
              FROM r WHERE s.sku_id = r.sku_id", ct);
}
```

### 4.2 Dọn reservation hết hạn

```csharp
internal sealed class ReservationSweeper(IServiceScopeFactory scopes, ILogger<ReservationSweeper> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopes.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<IInventoryService>();

            var expired = await db.Reservations
                .Where(r => r.Status == ReservationStatus.Held && r.ExpiresAt < DateTime.UtcNow)
                .Select(r => r.ReservationId).Distinct().Take(100).ToListAsync(ct);

            foreach (var id in expired) await svc.ReleaseAsync(id, ct);
            if (expired.Count > 0) log.LogInformation("Released {Count} expired reservations", expired.Count);

            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}
```

Chạy trên nhiều instance vẫn an toàn vì `ReleaseAsync` idempotent (`WHERE status = 'Held'`).

### 4.3 Consumer: xác nhận trừ kho khi có `OrderCreated`

```csharp
internal sealed class OrderCreatedConsumer(IServiceScopeFactory scopes, IConfiguration cfg,
    ILogger<OrderCreatedConsumer> log) : RabbitConsumer(cfg, log)
{
    protected override string QueueName => "inventory.order-created";

    protected override async Task HandleAsync(string type, string messageId,
        ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (type != nameof(OrderCreated)) return;
        var evt = JsonSerializer.Deserialize<OrderCreated>(body.Span)!;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        // Idempotent: chèn message_id, đã tồn tại thì bỏ qua
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO inventory.processed_messages(message_id) VALUES ({Guid.Parse(messageId)})
            ON CONFLICT DO NOTHING", ct);
        if (inserted == 0) return;

        await scope.ServiceProvider.GetRequiredService<IInventoryService>()
                   .CommitAsync(evt.ReservationId, ct);
    }
}
```

> Để chặt chẽ hơn, đặt câu `INSERT processed_messages` và nghiệp vụ trong cùng một transaction.

---

## 5. Module Ordering – luồng đặt hàng

### 5.1 Entity gọn

```csharp
internal enum OrderStatus { Pending, Paid, Failed, Cancelled }

internal sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public decimal Total { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public List<OrderLine> Lines { get; private set; } = new();

    public static Order Create(Guid userId, string key, IEnumerable<OrderLine> lines)
    {
        var o = new Order { UserId = userId, IdempotencyKey = key, Lines = lines.ToList() };
        o.Total = o.Lines.Sum(l => l.UnitPrice * l.Quantity);
        return o;
    }
    public void MarkPaid() => Status = OrderStatus.Paid;
    public void MarkFailed() => Status = OrderStatus.Failed;
}
```

### 5.2 Use case `PlaceOrder`

```csharp
internal sealed class PlaceOrderHandler(
    OrderingDbContext db, ICatalogService catalog, IPriceCalculator pricing,
    IInventoryService inventory, IPaymentService payment)
{
    public async Task<PlaceOrderResult> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        // 1. Idempotency: cùng (user, key) thì trả lại đơn cũ
        var existing = await db.Orders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.UserId == cmd.UserId && o.IdempotencyKey == cmd.IdempotencyKey, ct);
        if (existing is not null) return PlaceOrderResult.From(existing, replayed: true);

        // 2. Tính giá (Catalog) rồi áp quy tắc giảm giá (OCP)
        var prices = await catalog.GetPricesAsync(cmd.Items.Select(i => i.SkuId), DateTime.UtcNow, ct);
        var lines  = pricing.Price(cmd.Items, prices);
        var order  = Order.Create(cmd.UserId, cmd.IdempotencyKey, lines);

        // 3. ĐỒNG BỘ: giữ hàng. Hết hàng thì từ chối ngay
        var reserve = await inventory.ReserveAsync(new ReserveRequest(order.Id,
            cmd.Items.Select(i => new ReserveLine(i.SkuId, i.Quantity)).ToList(),
            TimeSpan.FromMinutes(10)), ct);
        if (!reserve.Success) return PlaceOrderResult.OutOfStock(reserve.Reason!);

        // 4. ĐỒNG BỘ: thanh toán. Lỗi thì bù trừ bằng cách hoàn kho
        var pay = await payment.ChargeAsync(new ChargeRequest(order.Id, order.Total, cmd.IdempotencyKey), ct);
        if (!pay.Success)
        {
            await inventory.ReleaseAsync(reserve.ReservationId!.Value, ct);
            return PlaceOrderResult.PaymentFailed(pay.Error);
        }
        order.MarkPaid();

        // 5. Một transaction duy nhất: ghi đơn + ghi outbox
        db.Orders.Add(order);
        db.OutboxMessages.Add(OutboxMessage.Create(new OrderCreated(order.Id, order.UserId,
            reserve.ReservationId!.Value, order.Total,
            order.Lines.Select(l => new OrderCreatedLine(l.SkuId, l.Quantity, l.UnitPrice)).ToList(),
            DateTime.UtcNow)));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) // trùng (user,key) do 2 request song song
        {
            await inventory.ReleaseAsync(reserve.ReservationId.Value, ct);
            var dup = await db.Orders.Include(o => o.Lines).AsNoTracking()
                .FirstAsync(o => o.UserId == cmd.UserId && o.IdempotencyKey == cmd.IdempotencyKey, ct);
            return PlaceOrderResult.From(dup, replayed: true);
        }
        return PlaceOrderResult.From(order, replayed: false);
    }
}
```

**Cửa sổ lỗi cần ghi vào báo cáo:** nếu tiến trình chết sau bước 3 nhưng trước bước 5, kho đang bị giữ nhưng không có đơn. `ReservationSweeper` sẽ hoàn kho khi TTL hết, đây là cơ chế tự chữa (self-healing). Thanh toán trùng được chặn nhờ `payments.order_id` unique và `IdempotencyKey`.

### 5.3 OCP: quy tắc giá/giảm giá có thể thêm mà không sửa lõi

```csharp
internal interface IDiscountRule
{
    int Order { get; }
    decimal Apply(OrderItem item, SkuPrice price, decimal currentUnitPrice);
}
internal sealed class FlashSaleRule : IDiscountRule
{
    public int Order => 10;
    public decimal Apply(OrderItem i, SkuPrice p, decimal current) => p.IsFlashSale ? p.EffectivePrice : current;
}
internal sealed class BulkDiscountRule : IDiscountRule   // thêm quy tắc mới = thêm class + đăng ký DI
{
    public int Order => 20;
    public decimal Apply(OrderItem i, SkuPrice p, decimal current) => i.Quantity >= 5 ? current * 0.95m : current;
}
internal sealed class PriceCalculator(IEnumerable<IDiscountRule> rules) : IPriceCalculator
{
    public IReadOnlyList<OrderLine> Price(IEnumerable<OrderItem> items, IReadOnlyList<SkuPrice> prices) =>
        items.Select(i =>
        {
            var p = prices.First(x => x.SkuId == i.SkuId);
            var unit = rules.OrderBy(r => r.Order).Aggregate(p.BasePrice, (cur, r) => r.Apply(i, p, cur));
            return new OrderLine(i.SkuId, i.Quantity, unit);
        }).ToList();
}
```

### 5.4 Outbox publisher (an toàn khi chạy 2 instance)

```csharp
internal sealed class OutboxPublisher(IServiceScopeFactory scopes, IMessageBus bus,
    ILogger<OutboxPublisher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            int count;
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                // SKIP LOCKED: 2 instance không lấy trùng cùng một lô
                var batch = await db.OutboxMessages.FromSqlRaw(@"
                    SELECT * FROM ordering.outbox_messages
                     WHERE processed_at IS NULL ORDER BY occurred_at
                     LIMIT 50 FOR UPDATE SKIP LOCKED").ToListAsync(ct);

                foreach (var m in batch)
                {
                    await bus.PublishAsync("order.events", m.Type, m.Payload, m.Id, ct);
                    m.ProcessedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                count = batch.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Outbox publish failed, will retry");
                count = 0;
            }
            if (count == 0) await Task.Delay(500, ct);
        }
    }
}
```

Đây là *at-least-once*: nếu publish xong mà commit lỗi, message có thể phát lại. Đó là lý do mọi consumer phải idempotent (mục 4.3).

---

## 6. Messaging với RabbitMQ (BuildingBlocks)

### 6.1 Publisher

```csharp
public interface IMessageBus
{
    Task PublishAsync(string exchange, string type, string payloadJson, Guid messageId, CancellationToken ct);
}

internal sealed class RabbitMessageBus(IConfiguration cfg) : IMessageBus, IAsyncDisposable
{
    private IConnection? _conn; private IChannel? _ch;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task PublishAsync(string exchange, string type, string payload, Guid id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_conn is null || !_conn.IsOpen)
            {
                var f = new ConnectionFactory { Uri = new Uri(cfg["RabbitMq:Uri"]!) };
                _conn = await f.CreateConnectionAsync(ct);
                _ch   = await _conn.CreateChannelAsync(cancellationToken: ct);
                await _ch.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);
            }
            var props = new BasicProperties
            {
                MessageId = id.ToString(), Type = type, DeliveryMode = DeliveryModes.Persistent,
                ContentType = "application/json",
                Headers = new Dictionary<string, object?> { ["x-correlation-id"] = CorrelationContext.Current }
            };
            await _ch!.BasicPublishAsync(exchange, routingKey: "", mandatory: false, props,
                                         Encoding.UTF8.GetBytes(payload), ct);
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync() { if (_ch is not null) await _ch.DisposeAsync(); if (_conn is not null) await _conn.DisposeAsync(); }
}
```

### 6.2 Consumer nền (retry + dead-letter)

```csharp
public abstract class RabbitConsumer(IConfiguration cfg, ILogger log) : BackgroundService
{
    protected abstract string QueueName { get; }
    protected abstract Task HandleAsync(string type, string messageId, ReadOnlyMemory<byte> body, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(cfg["RabbitMq:Uri"]!) };
        // Chờ RabbitMQ sẵn sàng khi khởi động cùng compose
        IConnection conn = default!;
        for (var i = 0; i < 20; i++)
        {
            try { conn = await factory.CreateConnectionAsync(ct); break; }
            catch { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
        }
        var ch = await conn.CreateChannelAsync(cancellationToken: ct);

        await ch.ExchangeDeclareAsync("order.events", ExchangeType.Fanout, true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync("order.events.dlx", ExchangeType.Fanout, true, cancellationToken: ct);
        await ch.QueueDeclareAsync("order.events.dead", true, false, false, cancellationToken: ct);
        await ch.QueueBindAsync("order.events.dead", "order.events.dlx", "", cancellationToken: ct);

        await ch.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "order.events.dlx" },
            cancellationToken: ct);
        await ch.QueueBindAsync(QueueName, "order.events", "", cancellationToken: ct);
        await ch.BasicQosAsync(0, 10, false, ct);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var type = ea.BasicProperties.Type ?? "";
            var id   = ea.BasicProperties.MessageId ?? "";
            try
            {
                await WithRetry(() => HandleAsync(type, id, ea.Body, ct), ct);
                await ch.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Message {Id} failed after retries -> DLQ", id);
                await ch.BasicNackAsync(ea.DeliveryTag, false, requeue: false, ct); // sang DLQ
            }
        };
        await ch.BasicConsumeAsync(QueueName, autoAck: false, consumer, ct);
        await Task.Delay(Timeout.Infinite, ct);
    }

    private static async Task WithRetry(Func<Task> action, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { await action(); return; }
            catch when (attempt < 3) { await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct); }
        }
    }
}
```

Mỗi module quan tâm tới event có **một queue riêng** gắn vào fanout exchange `order.events` (`inventory.order-created`, `notification.order-created`). Bên gửi không biết ai nhận.

> Nếu thay đổi tham số của một queue đã tồn tại (ví dụ thêm `x-dead-letter-exchange`), RabbitMQ báo lỗi `PRECONDITION_FAILED`. Khi đó xóa queue cũ trong management UI (`:15672`) hoặc `docker compose down -v`.

### 6.3 Notification consumer (kênh thông báo theo ISP/DIP)

```csharp
public interface INotificationChannel { string Name { get; } Task SendAsync(Guid userId, string content, CancellationToken ct); }
internal sealed class LogEmailChannel(ILogger<LogEmailChannel> log) : INotificationChannel
{
    public string Name => "email";
    public Task SendAsync(Guid userId, string content, CancellationToken ct)
    { log.LogInformation("[FAKE-EMAIL] to={User} body={Body}", userId, content); return Task.CompletedTask; }
}
```

Thêm kênh SMS = thêm một class mới implement `INotificationChannel`, không sửa consumer.

---

## 7. Module Catalog + MinIO

```csharp
public interface IImageStorage
{
    Task<(string Key, string UploadUrl)> CreateUploadUrlAsync(string fileName, string contentType, CancellationToken ct);
    string GetDownloadUrl(string key, TimeSpan expiry);
}
```

Endpoint:

| Method | Path | Mô tả |
|---|---|---|
| POST | `/products/{id}/image-upload-url` (admin) | Trả `{ key, uploadUrl }`, client `PUT` ảnh trực tiếp lên MinIO |
| PUT | `/products/{id}/image` (admin) | Lưu `image_key` sau khi upload xong |
| GET | `/products/{id}` | Trả sản phẩm, kèm presigned GET URL hết hạn sau 10 phút |

Lưu ý khi dùng presigned URL trong Docker: URL được ký cho một host cụ thể. Cấu hình `Minio:PublicEndpoint` (ví dụ `localhost:9000`) cho client trình duyệt, khác với `Minio:Endpoint` (`minio:9000`) dùng nội bộ giữa container. Dùng hai client SDK khác endpoint nếu cần.

---

## 8. Module Identity (JWT)

- `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`.
- Băm mật khẩu bằng `BCrypt.Net-Next`. Access token 15 phút, refresh token lưu **băm** trong DB.
- Secret đọc từ biến môi trường `Jwt__Secret` (≥ 32 ký tự), **khác nhau giữa các môi trường**, không nằm trong Git.

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidIssuer = cfg["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = cfg["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Secret"]!)),
        ClockSkew = TimeSpan.FromSeconds(30)
    });
```

---

## 9. Module Payment (cổng giả lập)

```csharp
internal interface IPaymentGateway { Task<GatewayResult> ChargeAsync(decimal amount, string reference, CancellationToken ct); }
internal sealed class FakeGateway(IConfiguration cfg) : IPaymentGateway   // DIP: thay bằng cổng thật sau này
{
    public async Task<GatewayResult> ChargeAsync(decimal amount, string reference, CancellationToken ct)
    {
        await Task.Delay(Random.Shared.Next(50, 150), ct);
        var failRate = cfg.GetValue("Payment:FailRate", 0.0);
        return Random.Shared.NextDouble() < failRate
            ? GatewayResult.Fail("DECLINED") : GatewayResult.Ok($"FAKE-{Guid.NewGuid():N}");
    }
}
```

`PaymentService.ChargeAsync` kiểm tra bảng `payments` theo `order_id` trước: đã có bản ghi thành công thì trả lại kết quả cũ, không gọi cổng lần hai. Dùng `Payment:FailRate` để mô phỏng lỗi và kiểm chứng bước bù trừ hoàn kho.

---

## 10. Host: Program.cs, migration, middleware

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext().WriteTo.Console(new CompactJsonFormatter()));

var cfg = builder.Configuration;
builder.Services
    .AddBuildingBlocks(cfg)
    .AddIdentityModule(cfg).AddCatalogModule(cfg).AddInventoryModule(cfg)
    .AddOrderingModule(cfg).AddPaymentModule(cfg).AddNotificationModule(cfg);
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer().AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddNpgSql(cfg.GetConnectionString("Default")!, name: "postgres")
    .AddRabbitMQ(name: "rabbitmq");        // gói AspNetCore.HealthChecks.*

var app = builder.Build();

// Chế độ migrator: chạy migration (và seed nếu có) rồi thoát. Host nền (consumer...) không khởi động.
if (args.Contains("--migrate"))
{
    foreach (var m in app.Services.GetServices<IModuleMigrator>()) await m.MigrateAsync(default);
    if (args.Contains("--seed"))
        foreach (var s in app.Services.GetServices<IModuleSeeder>()) await s.SeedAsync(default);
    return;
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<InstanceHeaderMiddleware>();    // thêm header X-Instance = INSTANCE_ID
app.UseSerilogRequestLogging();
app.UseAuthentication(); app.UseAuthorization();
app.UseSwagger(); app.UseSwaggerUI();

app.MapIdentityEndpoints(); app.MapCatalogEndpoints();
app.MapInventoryAdminEndpoints(); app.MapOrderingEndpoints();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.Run();
```

**Vì sao tách `--migrate` thành service riêng?** Nếu hai instance API cùng tự `Migrate()` lúc khởi động sẽ tranh nhau. Cho một container `migrator` chạy một lần rồi mới khởi động API (xem compose bên dưới).

Mỗi module đăng ký migrator riêng:

```csharp
internal sealed class InventoryMigrator(InventoryDbContext db) : IModuleMigrator
{ public Task MigrateAsync(CancellationToken ct) => db.Database.MigrateAsync(ct); }

// Trong DbContext options: bảng lịch sử migration nằm trong schema của chính module
o.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "inventory"));
```

Tạo migration cho từng module:

```bash
dotnet ef migrations add Init -p src/Modules/Inventory/ShopFlow.Modules.Inventory \
  -s src/ShopFlow.Host -c InventoryDbContext
```

`CorrelationIdMiddleware` đọc `X-Correlation-Id` (hoặc sinh mới), lưu vào `AsyncLocal` (`CorrelationContext.Current`), đẩy vào `LogContext` của Serilog và gắn vào header của message RabbitMQ để lần theo một đơn hàng xuyên API, queue và consumer.

---

## 11. Danh sách API

| Method | Path | Module | Quyền | Ghi chú |
|---|---|---|---|---|
| POST | `/auth/register`, `/auth/login`, `/auth/refresh` | Identity | Public | |
| GET | `/products`, `/products/{id}` | Catalog | Public | Phân trang, lọc |
| POST/PUT | `/products`, `/products/{id}` | Catalog | Admin | |
| POST | `/products/{id}/image-upload-url` | Catalog | Admin | Presigned PUT |
| POST | `/admin/flash-sales` | Catalog | Admin | Giá + khung giờ |
| PUT | `/admin/inventory/{skuId}` | Inventory | Admin | Đặt tồn kho |
| GET | `/admin/inventory/{skuId}` | Inventory | Admin | Phục vụ kiểm chứng sau load test |
| POST | `/orders` | Ordering | User | **Bắt buộc header `Idempotency-Key`** |
| GET | `/orders`, `/orders/{id}` | Ordering | User | Chỉ xem đơn của mình |
| GET | `/health/live`, `/health/ready` | Host | Public | |

Mã trả về cho `POST /orders`: `201` tạo mới, `200` replay do trùng Idempotency-Key, `409` hết hàng, `402` thanh toán thất bại, `400` thiếu header.

---

## 12. Docker, Compose, NGINX

### 12.1 Dockerfile (multi-stage, không chạy bằng root)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ShopFlow.Host/ShopFlow.Host.csproj
RUN dotnet publish src/ShopFlow.Host/ShopFlow.Host.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ShopFlow.Host.dll"]
```

`.dockerignore`: `**/bin`, `**/obj`, `.git`, `.env`, `*.md`, `tests/`, `loadtest/`.

### 12.2 `deploy/docker-compose.yml`

```yaml
x-api: &api
  build: { context: .., dockerfile: Dockerfile }
  image: shopflow-api:${TAG:-dev}
  env_file: .env
  environment:
    ConnectionStrings__Default: Host=db;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
    RabbitMq__Uri: amqp://${RABBIT_USER}:${RABBIT_PASSWORD}@rabbitmq:5672
    Minio__Endpoint: minio:9000
    Minio__PublicEndpoint: ${MINIO_PUBLIC_ENDPOINT}
    Minio__AccessKey: ${MINIO_ROOT_USER}
    Minio__SecretKey: ${MINIO_ROOT_PASSWORD}
    Jwt__Secret: ${JWT_SECRET}
  depends_on:
    migrator: { condition: service_completed_successfully }
    rabbitmq: { condition: service_healthy }
  restart: unless-stopped
  logging: { driver: json-file, options: { max-size: "10m", max-file: "3" } }

services:
  db:
    image: postgres:16
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      retries: 10

  rabbitmq:
    image: rabbitmq:3.13-management
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBIT_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBIT_PASSWORD}
    ports: ["15672:15672"]
    volumes: [rabbitdata:/var/lib/rabbitmq]
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      retries: 10

  minio:
    image: minio/minio:RELEASE.2025-04-22T22-12-26Z
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    ports: ["9000:9000", "9001:9001"]
    volumes: [miniodata:/data]

  migrator:
    <<: *api
    command: ["--migrate", "--seed"]
    depends_on:
      db: { condition: service_healthy }
    restart: "no"

  api1:
    <<: *api
    environment: { INSTANCE_ID: api1 }   # được gộp với environment chung (xem ghi chú)
  api2:
    <<: *api
    environment: { INSTANCE_ID: api2 }

  nginx:
    image: nginx:1.27
    ports: ["8080:80"]
    volumes: ["./nginx/nginx.conf:/etc/nginx/nginx.conf:ro"]
    depends_on: [api1, api2]

volumes: { pgdata: {}, rabbitdata: {}, miniodata: {} }
```

> **Ghi chú YAML:** merge key `<<:` **không gộp sâu** (deep merge) khối `environment`; khai báo lại `environment` ở `api1`/`api2` sẽ ghi đè cả khối. Cách an toàn: chuyển các biến chung sang `.env`/`env_file` (đã làm một phần), hoặc ghi lặp đầy đủ khối `environment` cho từng service. Kiểm tra bằng `docker compose config`.

### 12.3 `deploy/nginx/nginx.conf`

```nginx
events {}
http {
  limit_req_zone $binary_remote_addr zone=orders:10m rate=20r/s;

  upstream shopflow_api {
    server api1:8080;
    server api2:8080;
  }

  server {
    listen 80;
    client_max_body_size 10M;

    location / {
      proxy_pass http://shopflow_api;
      proxy_set_header Host              $host;
      proxy_set_header X-Real-IP         $remote_addr;
      proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
      proxy_set_header X-Forwarded-Proto $scheme;
    }
    location /orders {
      limit_req zone=orders burst=40 nodelay;
      proxy_pass http://shopflow_api;
      proxy_set_header Host              $host;
      proxy_set_header X-Real-IP         $remote_addr;
      proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
      proxy_set_header X-Forwarded-Proto $scheme;
    }
  }
}
```

Khi chạy load test, nới `rate`/`burst` hoặc tắt `limit_req` để không đo nhầm giới hạn của NGINX thay vì của hệ thống.

### 12.4 `deploy/.env.example` (file `.env` thật nằm trong `.gitignore`)

```
TAG=dev
POSTGRES_DB=shopflow
POSTGRES_USER=shopflow
POSTGRES_PASSWORD=change-me
RABBIT_USER=shopflow
RABBIT_PASSWORD=change-me
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=change-me-too
MINIO_PUBLIC_ENDPOINT=localhost:9000
JWT_SECRET=replace-with-at-least-32-random-chars
Jwt__Issuer=shopflow
Jwt__Audience=shopflow-clients
```

Chạy: `docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build`, sau đó mở Swagger qua `http://localhost:8080/swagger`.

---

## 13. Kiểm thử

### 13.1 Architecture test (CI đỏ nếu vi phạm ranh giới module)

```csharp
public class ModuleBoundaryTests
{
    static readonly string[] Modules = { "Identity", "Catalog", "Inventory", "Ordering", "Payment", "Notification" };

    [Fact]
    public void Module_must_not_depend_on_internals_of_other_modules()
    {
        foreach (var m in Modules)
        {
            var asm = Assembly.Load($"ShopFlow.Modules.{m}");
            var forbidden = Modules.Where(x => x != m).SelectMany(x => new[]
            {
                $"ShopFlow.Modules.{x}.Domain",
                $"ShopFlow.Modules.{x}.Application",
                $"ShopFlow.Modules.{x}.Infrastructure"
            }).ToArray();

            var result = Types.InAssembly(asm).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
            Assert.True(result.IsSuccessful,
                $"{m} vi phạm: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    [Fact]
    public void Domain_must_not_depend_on_Infrastructure()
    {
        foreach (var m in Modules)
        {
            var result = Types.InAssembly(Assembly.Load($"ShopFlow.Modules.{m}"))
                .That().ResideInNamespace($"ShopFlow.Modules.{m}.Domain")
                .ShouldNot().HaveDependencyOn($"ShopFlow.Modules.{m}.Infrastructure").GetResult();
            Assert.True(result.IsSuccessful);
        }
    }
}
```

### 13.2 Unit test (chạy nhanh, không DB)

- `PriceCalculator`: giá flash sale, giảm giá số lượng lớn, thứ tự áp quy tắc.
- `Order.Create`: tổng tiền, trạng thái ban đầu.
- `PlaceOrderHandler` với mock: hết hàng không gọi payment; payment lỗi thì gọi `ReleaseAsync` đúng một lần; trùng Idempotency-Key trả lại đơn cũ.

### 13.3 Integration test chống overselling (Testcontainers)

```csharp
[Fact]
public async Task Concurrent_reserve_never_oversells()
{
    const int stock = 50, requests = 500;
    await SeedStock(skuId, stock);

    var results = await Task.WhenAll(Enumerable.Range(0, requests).Select(async _ =>
    {
        await using var scope = CreateScope();   // mỗi task một DbContext riêng
        var svc = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        return await svc.ReserveAsync(new ReserveRequest(Guid.NewGuid(),
            new[] { new ReserveLine(skuId, 1) }, TimeSpan.FromMinutes(5)), default);
    }));

    Assert.Equal(stock, results.Count(r => r.Success));
    Assert.Equal(0, await GetAvailable(skuId));       // không âm, không dư
}
```

Bổ sung test: release hai lần chỉ hoàn kho một lần; reservation hết hạn được sweeper hoàn kho; consumer nhận cùng message hai lần chỉ commit một lần.

### 13.4 Load test với k6 (`loadtest/oversell.js`)

```javascript
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const SKU  = __ENV.SKU_ID;
const created = new Counter('orders_created');
const soldOut = new Counter('orders_sold_out');

export const options = {
  scenarios: { burst: { executor: 'per-vu-iterations', vus: 200, iterations: 3, maxDuration: '2m' } },
  thresholds: { http_req_failed: ['rate<0.01'], 'http_req_duration{name:place_order}': ['p(95)<800'] },
};

export function setup() {
  // Tạo sẵn 200 user và lấy token
  return Array.from({ length: 200 }, (_, i) => {
    const email = `load${i}@test.local`;
    http.post(`${BASE}/auth/register`, JSON.stringify({ email, password: 'P@ssw0rd!' }),
              { headers: { 'Content-Type': 'application/json' } });
    const r = http.post(`${BASE}/auth/login`, JSON.stringify({ email, password: 'P@ssw0rd!' }),
              { headers: { 'Content-Type': 'application/json' } });
    return r.json('accessToken');
  });
}

export default function (tokens) {
  const token = tokens[(__VU - 1) % tokens.length];
  const res = http.post(`${BASE}/orders`,
    JSON.stringify({ items: [{ skuId: SKU, quantity: 1 }] }),
    { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`,
                 'Idempotency-Key': uuidv4() }, tags: { name: 'place_order' } });
  if (res.status === 201) created.add(1);
  if (res.status === 409) soldOut.add(1);
  check(res, { 'status hợp lệ': r => [201, 409].includes(r.status) });
}
```

**Quy trình chạy và ghi bằng chứng:**
1. Đặt tồn kho: `PUT /admin/inventory/{sku}` với `available = 50`.
2. `k6 run -e SKU_ID=<id> loadtest/oversell.js`.
3. Gọi `GET /admin/inventory/{sku}` kiểm tra `available = 0`, đếm số đơn `Paid` trong DB bằng đúng 50.
4. Chụp màn hình/ghi lại: số request, `orders_created`, `orders_sold_out`, p95, kết quả kiểm tra DB. Đây là bằng chứng cho CV.

Chạy lần so sánh (tuỳ chọn): một bản cố tình viết sai (đọc `available` rồi mới trừ, không có điều kiện) để thấy bán vượt, rồi bản đúng. Cặp "trước/sau" rất thuyết phục khi báo cáo.

---

## 14. CI/CD (Jenkinsfile mẫu)

```groovy
pipeline {
  agent any
  environment {
    REGISTRY = 'ghcr.io/<user>'
    IMAGE    = "${REGISTRY}/shopflow-api"
    TAG      = "${env.BRANCH_NAME == 'main' ? 'prod' : env.BRANCH_NAME}-${env.BUILD_NUMBER}"
  }
  stages {
    stage('Checkout') { steps { checkout scm } }
    stage('Test') {
      steps { sh 'dotnet test ShopFlow.sln -c Release --logger "trx"' }  // fail thì dừng pipeline
    }
    stage('Build Image') { steps { sh 'docker build -t $IMAGE:$TAG .' } }
    stage('Push Image') {
      steps {
        withCredentials([usernamePassword(credentialsId: 'ghcr', usernameVariable: 'U', passwordVariable: 'P')]) {
          sh 'echo $P | docker login ghcr.io -u $U --password-stdin && docker push $IMAGE:$TAG'
        }
      }
    }
    stage('Deploy') {
      steps { sh 'TAG=$TAG docker compose -f deploy/docker-compose.yml --env-file /etc/shopflow/${BRANCH_NAME}.env up -d' }
    }
  }
  post { always { junit allowEmptyResults: true, testResults: '**/*.trx' } }
}
```

- Integration test dùng Testcontainers cần Jenkins truy cập được Docker.
- File `.env` thật của từng môi trường nằm sẵn trên máy chạy (không commit); mỗi môi trường có `JWT_SECRET` riêng.
- Nếu dùng GitHub Actions thay Jenkins, các stage tương đương: `dotnet test` → `docker build/push` → `ssh` hoặc `docker compose pull && up -d`.
- Rollback: chạy lại bước Deploy với `TAG` cũ (image đã có trong registry), không build lại.

---

## 15. Tách Inventory thành gRPC microservice (giai đoạn cuối)

### 15.1 Hợp đồng `inventory.proto`

```protobuf
syntax = "proto3";
option csharp_namespace = "ShopFlow.Inventory.Grpc";
package inventory;

service InventoryGrpc {
  rpc Reserve (ReserveRequest) returns (ReserveReply);
  rpc Commit  (ReservationId)  returns (Empty);
  rpc Release (ReservationId)  returns (Empty);
}
message Line { string sku_id = 1; int32 quantity = 2; }
message ReserveRequest { string order_id = 1; repeated Line lines = 2; int32 ttl_seconds = 3; }
message ReserveReply   { bool success = 1; string reservation_id = 2; string reason = 3; }
message ReservationId  { string id = 1; }
message Empty {}
```

### 15.2 Bốn thay đổi (đúng như tư duy Modular Monolith)

| # | Việc cần làm | Cụ thể |
|---|---|---|
| 1 | Tách database | Tạo DB `shopflow_inventory`, di chuyển schema `inventory` (dump/restore hoặc migration mới) |
| 2 | Đổi lời gọi in-process thành network | Viết `InventoryGrpcClient : IInventoryService` (map sang proto); trong Ordering **không đổi dòng nào**, chỉ đổi đăng ký DI |
| 3 | Đóng gói riêng | Project `ShopFlow.Inventory.Service` (gRPC server bọc `InventoryService` có sẵn + consumer + sweeper), Dockerfile riêng |
| 4 | Pipeline riêng | Jenkinsfile/job riêng cho service mới |

Đổi cách gọi bằng cấu hình:

```csharp
if (cfg["Inventory:Mode"] == "Grpc")
    services.AddGrpcClient<InventoryGrpc.InventoryGrpcClient>(o => o.Address = new Uri(cfg["Inventory:GrpcUrl"]!))
            .Services.AddScoped<IInventoryService, InventoryGrpcClient>();
else
    services.AddInventoryModule(cfg);   // chế độ in-process như cũ
```

Điều cần nói trong báo cáo: giao dịch cục bộ không còn bao trùm cả Inventory lẫn Ordering, nhưng thiết kế đã sẵn **bù trừ** (`Release`), **TTL** và **idempotency**, nên việc tách vẫn an toàn.

---

## 16. Lộ trình 8 tuần và tiêu chí hoàn thành

| Tuần | Công việc | Hoàn thành khi |
|---|---|---|
| 1 | Phân rã module, dựng solution, BuildingBlocks, Docker Compose với db/rabbit/minio | `docker compose up` chạy, Swagger mở được |
| 2 | Identity (JWT) + Catalog + MinIO upload | Đăng ký/đăng nhập, tạo sản phẩm có ảnh |
| 3 | Inventory: Reserve/Commit/Release, sweeper, integration test | Test 500 request/50 tồn kho đạt |
| 4 | Ordering + Payment + Idempotency + bù trừ | Luồng đặt hàng đủ/hết hàng/thanh toán lỗi đúng |
| 5 | Outbox + RabbitMQ + consumer idempotent + DLQ | Tắt consumer, đặt đơn, bật lại vẫn xử lý đủ |
| 6 | NGINX 2 instance, health check, Serilog + correlation ID, architecture test | Header `X-Instance` luân phiên, CI chặn vi phạm ranh giới |
| 7 | CI/CD, môi trường Dev/Staging, load test k6 | Pipeline xanh, có báo cáo load test |
| 8 | Tách Inventory sang gRPC, hoàn thiện README/ADR/báo cáo | Chạy được cả hai chế độ `InProcess` và `Grpc` |

### Checklist nghiệm thu

- [ ] Không có module nào tham chiếu project triển khai của module khác; architecture test xanh.
- [ ] 500 request đồng thời vào 50 tồn kho: đúng 50 đơn, `available = 0`.
- [ ] Gửi cùng `Idempotency-Key` hai lần: chỉ một đơn, một lần trừ kho, một lần thanh toán.
- [ ] Thanh toán lỗi: kho được hoàn lại.
- [ ] Tắt Notification consumer, đặt đơn, bật lại: email được xử lý sau đó; message lỗi vào DLQ.
- [ ] Hai instance API nhận request luân phiên qua NGINX.
- [ ] `.env` thật không có trong Git (`git log -p` chỉ thấy `.env.example`).
- [ ] Test fail thì pipeline dừng, không build image.
- [ ] README có sơ đồ kiến trúc, hướng dẫn chạy một lệnh, và 3–5 file ADR.

### Số liệu cần đo để điền vào CV

| Chỉ số | Cách đo | Giá trị |
|---|---|---|
| Số request đồng thời / tồn kho / đơn thành công / đơn vượt | k6 + truy vấn DB | ___ |
| p95 latency `POST /orders` | k6 | ___ ms |
| p95 trước và sau khi chuyển việc phụ sang bất đồng bộ | so sánh hai lần chạy | ___ → ___ ms |
| Thời gian push → chạy trên Staging | Jenkins | ___ phút |
| Thời gian rollback | Jenkins | ___ phút |
| Số unit/integration/architecture test | `dotnet test` | ___ |

---

## 17. Gợi ý nội dung ADR (mỗi file 10–20 dòng)

1. **ADR-001:** Chọn Modular Monolith thay vì Microservices ngay từ đầu (nguồn lực nhóm, ranh giới nghiệp vụ chưa ổn định).
2. **ADR-002:** Chống bán vượt bằng `UPDATE` có điều kiện thay vì khóa bi quan (`SELECT ... FOR UPDATE`) hoặc khóa phân tán; lý do và đánh đổi.
3. **ADR-003:** Outbox pattern + consumer idempotent thay vì publish trực tiếp trong request.
4. **ADR-004:** Một database nhiều schema, cấm truy cập chéo; điều kiện để tách DB sau này.
5. **ADR-005:** Đồng bộ (reserve, payment) và bất đồng bộ (thông báo, xác nhận kho): tiêu chí chọn.

---

## 18. Mô tả CV (điền số sau khi đo thật)

> **ShopFlow – Flash-Sale E-commerce Backend** | .NET 10, PostgreSQL 16, RabbitMQ (MassTransit), MinIO (AWS S3 SDK), gRPC, Docker Compose, NGINX
> - Built a 6-module modular monolith with per-module DB schemas; boundaries enforced by compile-time project references and architecture tests in CI.
> - Eliminated overselling under concurrent load via conditional atomic updates and TTL reservations; verified with k6 (**[N] concurrent requests, [S] stock, 0 oversold**).
> - Implemented the Outbox pattern, idempotent consumers, retry and dead-letter queues on RabbitMQ; reduced p95 order latency from **[A] ms to [B] ms** by moving side effects off the request path.
> - Containerized with multi-stage non-root images; load-balanced two API instances behind NGINX; automated test → build → push → deploy with versioned images and tag-based rollback.
> - Extracted the Inventory module into a standalone gRPC service with its own database without changing the Ordering use case.
