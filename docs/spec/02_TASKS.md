# 02 — Danh sách task tuần tự (ShopFlow)

**Cách dùng:** làm đúng thứ tự, mỗi lần một task. Trước khi làm hãy đọc `00_AI_RULES.md`. Mục "SPEC" của mỗi task chỉ các phần cần đọc. Khi xong, tick `[x]` ở bảng dưới.

## Tổng quan

| ✔ | ID | Tên | Tuần | Phụ thuộc |
|---|---|---|---|---|
| [x] | T01 | Khung solution | 1 | — |
| [x] | T02 | BuildingBlocks nền | 1 | T01 |
| [x] | T03 | Host skeleton | 1 | T02 |
| [x] | T04 | Docker, Compose, NGINX | 1 | T03 |
| [x] | T05 | Architecture tests | 1 | T01 |
| [x] | T06 | Identity: đăng ký, đăng nhập | 2 | T03 |
| [x] | T07 | Identity: refresh, phân quyền, seed admin | 2 | T06 |
| [x] | T08 | Catalog: sản phẩm, flash sale, giá | 2 | T07 |
| [x] | T09 | Catalog: ảnh MinIO | 2 | T08 |
| [x] | T10 | Inventory: dữ liệu + admin API | 3 | T07 |
| [x] | T11 | Inventory: Reserve/Commit/Release | 3 | T10 |
| [x] | T12 | Inventory: ReservationSweeper | 3 | T11 |
| [x] | T13 | Inventory: integration tests đồng thời | 3 | T12 |
| [x] | T14 | Payment | 4 | T03 |
| [x] | T15 | Ordering: domain + dữ liệu | 4 | T08, T11, T14 |
| [x] | T16 | Ordering: tính giá | 4 | T15 |
| [x] | T17 | Ordering: PlaceOrder + API | 4 | T16 |
| [x] | T18 | Ordering: integration tests | 4 | T17 |
| [x] | T19 | Ordering: PendingOrderReaper | 4 | T18 |
| [x] | T20 | Messaging: bus + consumer nền | 5 | T19 |
| [x] | T21 | Outbox publisher | 5 | T20 |
| [x] | T22 | Notification | 5 | T21 |
| [x] | T23 | Kịch bản chịu lỗi messaging | 5 | T22 |
| [x] | T24 | NGINX + health nâng cao | 6 | T23 |
| [x] | T25 | Correlation xuyên suốt | 6 | T23 |
| [x] | T26 | k6 load test + bằng chứng | 7 | T24 |
| [x] | T27 | (Tuỳ chọn) Benchmark sync vs async | 7 | T26 |
| [x] | T28 | CI/CD | 7 | T26 |
| [x] | T29 | gRPC: Inventory Service | 8 | T28 |
| [x] | T30 | gRPC: client + chế độ kép | 8 | T29 |
| [x] | T31 | Tài liệu + nghiệm thu cuối | 8 | T30 |

**Mẫu mỗi task:** Mục tiêu · SPEC · Tạo/sửa · Yêu cầu · Nghiệm thu · Không làm.

---

# TUẦN 1 — Nền móng

## T01 — Khung solution
- **Mục tiêu:** solution biên dịch được, đúng cấu trúc và ma trận tham chiếu.
- **SPEC:** §3, §4.
- **Tạo/sửa:** `ShopFlow.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`, `.editorconfig`, `docs/spec/*` (chép 3 file spec + Guide cũ vào), `docs/PACKAGES.md`, `docs/OPEN_QUESTIONS.md`, các project theo Guide cũ §1.
- **Yêu cầu:**
  - `Directory.Build.props`: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true` (chỉ `NoWarn` cho mã đã có lý do ghi chú).
  - Quản lý gói tập trung (`Directory.Packages.props`). Chưa thêm gói ngoài những gì cần để build.
  - Mỗi module có 2 project: `ShopFlow.Modules.<X>` và `ShopFlow.Modules.<X>.Contracts` (Identity, Catalog, Inventory, Ordering, Payment, Notification). Thêm `ShopFlow.BuildingBlocks`, `ShopFlow.Host`, và 3 project test: `ShopFlow.ArchitectureTests`, `ShopFlow.UnitTests`, `ShopFlow.IntegrationTests`.
  - Tham chiếu đúng ma trận §4. Mỗi module có lớp `public static class <X>Module` với `Add<X>Module(IServiceCollection, IConfiguration)` và `Map<X>Endpoints(IEndpointRouteBuilder)` rỗng.
  - `InternalsVisibleTo` từ mỗi module sang `UnitTests` và `IntegrationTests`.
- **Nghiệm thu:** `dotnet build ShopFlow.sln -c Release` thành công; `dotnet list <project> reference` của Ordering chỉ chứa BuildingBlocks + 3 Contracts (Catalog, Inventory, Payment).
- **Không làm:** chưa viết nghiệp vụ, chưa Docker.

## T02 — BuildingBlocks nền
- **Mục tiêu:** các thành phần dùng chung, chưa phụ thuộc module.
- **SPEC:** §5.
- **Tạo/sửa:** trong `ShopFlow.BuildingBlocks`: `CorrelationContext` + `CorrelationIdMiddleware`, `InstanceHeaderMiddleware`, `ProblemDetails` helper + `ErrorCodes` (hằng số theo §9.2) + `ExceptionHandlingMiddleware`, `IModuleMigrator`, `IModuleSeeder`, đăng ký `TimeProvider.System`, helper `PostgresErrors.IsUniqueViolation(Exception, constraintName?)` (nhận diện 23505).
- **Yêu cầu:** middleware correlation đọc/tạo `X-Correlation-Id` và đẩy vào `LogContext` Serilog; exception middleware trả `ProblemDetails` đúng §5 (kèm `code`, `correlationId`), không lộ stack trace. `IModuleMigrator` có `Schema` và `MigrateAsync`.
- **Nghiệm thu:** unit test: (1) header có sẵn được giữ nguyên, thiếu thì sinh mới và có trong response; (2) exception không xác định → 500 `INTERNAL_ERROR` JSON đúng schema; (3) `IsUniqueViolation` đúng/sai với `PostgresException` giả.
- **Không làm:** chưa Serilog sink/cấu hình host.

## T03 — Host skeleton
- **Mục tiêu:** Host chạy được, có health, cờ `--migrate`/`--seed`/`--healthcheck`.
- **SPEC:** §5, §9.1 (health), §11.
- **Tạo/sửa:** `ShopFlow.Host/Program.cs`, `appsettings*.json`, `public partial class Program`.
- **Yêu cầu:**
  - Serilog (console, JSON ở Production), gọi `Add<X>Module`/`Map<X>Endpoints` cho 6 module (rỗng).
  - `/health/live` luôn 200; `/health/ready` kiểm tra Postgres (RabbitMQ thêm ở T20; chưa có thì bỏ qua).
  - Swagger chỉ bật ở Development.
  - Chế độ CLI: `--migrate` chạy tất cả `IModuleMigrator` (mỗi cái chạy `CREATE SCHEMA IF NOT EXISTS` trước), rồi thoát; `--seed` chạy tất cả `IModuleSeeder`, rồi thoát; `--healthcheck` gọi `GET http://localhost:8080/health/live` và thoát với mã 0 hoặc 1 (không khởi tạo host đầy đủ).
  - Validate cấu hình bắt buộc khi khởi động (thiếu `ConnectionStrings:Default` → lỗi rõ ràng).
- **Nghiệm thu:** `dotnet run --project src/ShopFlow.Host` (với Postgres cục bộ) → `curl /health/live` = 200; `dotnet run ... -- --healthcheck` trả mã 0 khi host đang chạy, 1 khi không.
- **Không làm:** chưa module nào có bảng.

## T04 — Docker, Compose, NGINX
- **Mục tiêu:** `docker compose up` dựng được toàn hệ thống nền, 2 instance sau NGINX.
- **SPEC:** §3, §5, §11; Errata dòng "§12.2 compose".
- **Tạo/sửa:** `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `deploy/nginx/nginx.conf`.
- **Yêu cầu:**
  - Dockerfile nhiều tầng (`sdk:10.0` → `aspnet:10.0`), chạy user không phải root, `ENTRYPOINT ["dotnet","ShopFlow.Host.dll"]`, `EXPOSE 8080`, `ASPNETCORE_URLS=http://+:8080`. `HEALTHCHECK CMD ["dotnet","ShopFlow.Host.dll","--healthcheck"]`.
  - Compose gồm: `db` (postgres:16 + volume + healthcheck `pg_isready`), `rabbitmq` (3.13-management + healthcheck), `minio` (kiểm tra tag còn pull được), `migrator` (chạy `--migrate --seed`, `restart: "no"`), `api1`, `api2`, `nginx`.
  - **Gộp biến môi trường đúng cách:** định nghĩa `x-api-env: &api-env {...}` (mọi `ConnectionStrings__Default`, `RabbitMq__Uri`, `Jwt__*`, `Minio__*`, ... lấy từ `.env`), mỗi service dùng `environment: { <<: *api-env, INSTANCE_ID: api1 }`. **Không** dùng `<<` ở mức service rồi khai báo lại `environment`.
  - `api1`/`api2` `depends_on`: `db` healthy, `rabbitmq` healthy, `migrator` `service_completed_successfully`. `nginx` `depends_on` api1/api2 `service_healthy`.
  - NGINX: `upstream shopflow { server api1:8080; server api2:8080; keepalive 32; }`, `proxy_http_version 1.1`, `proxy_set_header Connection ""`, chuyển `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Correlation-Id`; cổng publish `${NGINX_PORT:-8080}:80`. Chưa cần `limit_req` (T24).
  - `.env.example` chứa mọi khóa cần dùng với giá trị giả; `.env` nằm trong `.gitignore`.
- **Nghiệm thu:** `docker compose config` hợp lệ; `docker compose up -d --build` → tất cả `healthy`; `curl -i localhost:8080/health/live` = 200; gọi 10 lần và đếm `X-Instance` thấy cả `api1` và `api2`; `docker compose exec api1 printenv ConnectionStrings__Default` có giá trị.
- **Không làm:** chưa `limit_req`, chưa deploy script.

## T05 — Architecture tests
- **Mục tiêu:** vi phạm ranh giới module làm test đỏ ngay.
- **SPEC:** §4.
- **Tạo/sửa:** `ShopFlow.ArchitectureTests/*`.
- **Yêu cầu:** dùng NetArchTest (kiểm tra API thực tế khi build) và kiểm tra metadata tham chiếu assembly:
  1. Với mỗi module, assembly triển khai chỉ tham chiếu `ShopFlow.BuildingBlocks`, Contracts của chính nó và Contracts được phép theo ma trận §4. Danh sách phụ thuộc cho phép khai báo thành một bảng trong test.
  2. Không kiểu nào trong module (ngoài `Contracts` và `<X>Module`) là `public`.
  3. Không project `*.Contracts` tham chiếu project nào ngoài BCL (và gói thuần túy cho record nếu thật sự cần).
  4. Không type nào trong module X dùng namespace `Infrastructure`/`Domain`/`Application` của module khác.
- **Nghiệm thu:** `dotnet test ShopFlow.ArchitectureTests` xanh. **Kiểm thử âm tính:** thêm tạm tham chiếu `Ordering → Inventory` (không phải Contracts) → test đỏ; hoàn tác → xanh. Dán cả hai output.
- **Không làm:** không nới lỏng quy tắc để test qua.

---

# TUẦN 2 — Identity & Catalog

## T06 — Identity: đăng ký, đăng nhập
- **SPEC:** §6 (identity), §8.1, §9.2.
- **Tạo/sửa:** module Identity: `IdentityDbContext` (schema `identity`), entity `User`, migration, `IdentityMigrator`, endpoints `register`, `login`, `AddShopFlowJwtAuth` trong BuildingBlocks (JwtBearer, đọc `Jwt:*`).
- **Yêu cầu:** BCrypt băm mật khẩu; email chuẩn hóa chữ thường; trùng email → 409 `EMAIL_EXISTS` (dựa vào unique index + `IsUniqueViolation`, không "kiểm tra rồi chèn"); login sai → 401 `INVALID_CREDENTIALS` cùng thông báo; JWT 15 phút với claim `sub`, `email`, `role`; `Jwt:Secret` < 32 ký tự → lỗi khi khởi động.
- **Nghiệm thu:** unit test validate email/mật khẩu; integration test (Testcontainers Postgres, dùng `WebApplicationFactory<Program>` với `Messaging:Enabled=false`): đăng ký → 201; đăng ký trùng (kể cả khác hoa/thường) → 409; login đúng → có token, giải mã thấy role `User`; login sai → 401; đăng ký 20 request song song cùng email → đúng 1 thành công.
- **Không làm:** refresh token, phân quyền Admin (T07).

## T07 — Identity: refresh, phân quyền, seed admin
- **SPEC:** §8.1.
- **Tạo/sửa:** entity `RefreshToken`, migration, endpoints `refresh`, `logout`, `IdentitySeeder` (admin), policy `AdminOnly`, một endpoint thử `GET /admin/ping` (chỉ để test, ghi chú sẽ xóa ở T31 hoặc giữ như health admin).
- **Yêu cầu:** refresh token lưu băm SHA-256; refresh xoay vòng; dùng lại token đã thu hồi → thu hồi toàn bộ token của user + 401; logout idempotent 204; seeder idempotent, đọc `Admin:Email`/`Admin:Password`, chạy qua `--seed`.
- **Nghiệm thu:** integration test: refresh thành công cho cặp mới và token cũ không dùng lại được; tái sử dụng token cũ → 401 và token mới cũng bị vô hiệu; token hết hạn (dùng `FakeTimeProvider`) → 401; `GET /admin/ping` với user thường → 403, với admin → 200, không token → 401; chạy seed hai lần chỉ tạo một admin.
- **Không làm:** quên mật khẩu, xác minh email.

## T08 — Catalog: sản phẩm, flash sale, giá
- **SPEC:** §6 (catalog), §8.2, §9.1, §7 (`ICatalogService`).
- **Tạo/sửa:** `CatalogDbContext`, entity `Product`, `FlashSale`, migration (kèm exclusion constraint btree_gist bằng SQL thô nếu chạy được, nếu không thì ghi vào `OPEN_QUESTIONS.md` và chỉ validate ở ứng dụng), endpoints sản phẩm + flash sale, `CatalogService : ICatalogService`, Contracts.
- **Yêu cầu:** đúng quy tắc §8.2 (phân trang, lọc, người thường chỉ thấy `is_active`, validate flash sale, `FLASH_SALE_OVERLAP`). `GetPricesAsync` chỉ trả SKU của sản phẩm active; giá hiệu lực theo biên `starts_at <= t < ends_at`.
- **Nghiệm thu:** unit test giá hiệu lực tại các biên (đúng `starts_at`, ngay trước `ends_at`, đúng `ends_at`); integration test: tạo sản phẩm (Admin) → 201; người thường tạo → 403; flash sale chồng giờ → 409; `GET /products` phân trang đúng `total`; sản phẩm inactive không hiện với User; `GetPrices` bỏ sót SKU inactive.
- **Không làm:** ảnh (T09).

## T09 — Catalog: ảnh MinIO
- **SPEC:** §8.2 (ảnh), §11 (`Minio:*`).
- **Tạo/sửa:** `IImageStorage` + `MinioImageStorage`, endpoints `image-upload-url` và `PUT image`, bước seed tạo bucket `product-images`, thêm MinIO vào integration test (Testcontainers MinIO) hoặc đánh dấu rõ nếu bỏ qua.
- **Yêu cầu:** hai client: `Endpoint` nội bộ cho thao tác server, `PublicEndpoint` cho ký presigned URL; chỉ nhận 3 content type; key `products/{id}/{guid}{ext}`; `PUT image` từ chối key không bắt đầu bằng `products/{id}/`; `GET /products/{id}` trả presigned GET 10 phút khi có ảnh.
- **Nghiệm thu:** test: content type `text/html` → 400; key sai tiền tố → 400; upload thật qua presigned URL bằng `HttpClient` rồi `GET` presigned trả đúng bytes; URL trả về dùng host của `PublicEndpoint`.
- **Không làm:** resize, CDN.

---

# TUẦN 3 — Inventory

## T10 — Inventory: dữ liệu + admin API
- **SPEC:** §6 (inventory), §8.3 (admin), §7.
- **Tạo/sửa:** `InventoryDbContext`, entity `StockItem`, `Reservation`, migration (CHECK `available >= 0`, CHECK `quantity > 0`, CHECK status, PK ghép, index một phần), `PUT/GET /admin/inventory/{skuId}`, Contracts đúng §7.
- **Yêu cầu:** upsert idempotent; `GET` trả `available`, `held`, `committed` (tổng quantity theo trạng thái).
- **Nghiệm thu:** integration test: upsert hai lần giữ một dòng; `available = -1` bị API từ chối (400) **và** UPDATE SQL trực tiếp vi phạm CHECK; User thường → 403.
- **Không làm:** Reserve/Commit/Release (T11).

## T11 — Inventory: Reserve/Commit/Release
- **SPEC:** §8.3, §7, §5 (không `EnableRetryOnFailure`).
- **Tạo/sửa:** `InventoryService : IInventoryService`; đăng ký DI.
- **Yêu cầu:**
  - `ReserveAsync` theo đúng 4 bước §8.3: idempotent theo `ReservationId`, sắp dòng theo `SkuId`, `UPDATE ... WHERE available >= @q` kiểm tra số hàng, rollback + `ChangeTracker.Clear()` khi thất bại, ghi `reservations` với `expires_at` từ `TimeProvider`. Isolation mặc định Read Committed (không nâng).
  - `CommitAsync` và `ReleaseAsync` theo đúng ngữ nghĩa §8.3/§7 (Commit trả `true` nếu đã Committed; Release chỉ hoàn kho khi đang `Held`, bằng CTE một câu lệnh).
  - Tất cả SQL dùng `ExecuteSqlInterpolatedAsync`.
- **Nghiệm thu:** smoke test nhanh: Reserve thành công trừ đúng; hết hàng → `OutOfStock`; Release hoàn kho. (Bộ test đầy đủ ở T13.)
- **Không làm:** sweeper (T12), caching.

## T12 — Inventory: ReservationSweeper
- **SPEC:** §8.3 (Sweeper), §11.
- **Tạo/sửa:** `ReservationSweeper : BackgroundService`.
- **Yêu cầu:** chu kỳ `Inventory:SweeperIntervalSeconds`; mỗi vòng lấy ≤ 100 reservation `Held` quá hạn (distinct `reservation_id`) và gọi `ReleaseAsync`; một lỗi không làm dừng vòng lặp; mọi thời gian qua `TimeProvider`; log số lượng đã dọn. Thiết kế để có thể gọi một vòng thủ công trong test (`RunOnceAsync`).
- **Nghiệm thu:** test với `FakeTimeProvider` ở T13.
- **Không làm:** không xóa bản ghi reservation (giữ để đối soát).

## T13 — Inventory: integration tests đồng thời
- **SPEC:** §2 (NFR-1), §8.3, §12 (E1).
- **Tạo/sửa:** `Inventory*Tests` trong `ShopFlow.IntegrationTests` với fixture Postgres dùng chung (Testcontainers).
- **Các test bắt buộc:**
  1. 500 `ReserveAsync` song song (`Task.WhenAll`), tồn 50, mỗi lần 1 → đúng 50 `Success`, `available = 0`, 50 reservation `Held`. Mỗi task dùng DbContext/scope riêng.
  2. Hai SKU, 100 task xen kẽ thứ tự dòng (A,B) và (B,A): tất cả hoàn thành, không exception deadlock, tổng tồn bảo toàn.
  3. Dòng 1 đủ hàng, dòng 2 thiếu → rollback: tồn dòng 1 không đổi, không có reservation.
  4. `Release` hai lần → tồn chỉ hoàn một lần.
  5. `Commit` hai lần → `true`, `true`; `Commit` sau `Release` → `false`; `Commit` id không tồn tại → `false`; `Release` sau `Commit` không hoàn kho.
  6. `Reserve` hai lần cùng `ReservationId` → chỉ trừ kho một lần.
  7. Sweeper: reservation quá hạn được Release, reservation `Committed` quá hạn **không** bị động; dùng `FakeTimeProvider`.
- **Nghiệm thu:** `dotnet test --filter Inventory` xanh, chạy 3 lần liên tiếp đều xanh (không flaky). Dán output.
- **Không làm:** không dùng mock cho DB.

---

# TUẦN 4 — Payment & Ordering

## T14 — Payment
- **SPEC:** §6 (payment), §7 (`IPaymentService`), §8.5, §11.
- **Tạo/sửa:** `PaymentDbContext`, entity `Payment`, migration (unique `order_id`), `IPaymentGateway` + `FakeGateway`, `PaymentService : IPaymentService`, Contracts.
- **Yêu cầu:** `ChargeAsync` idempotent theo `OrderId` (tra bản ghi trước; chạm unique → tải bản ghi đang có); `GetByOrderAsync`; `FakeGateway` đọc `Payment:FailRate` và độ trễ min/max.
- **Nghiệm thu:** test: gọi `Charge` hai lần cùng `OrderId` → một bản ghi, gateway chỉ được gọi một lần (đếm bằng gateway giả); 20 lệnh `Charge` song song cùng `OrderId` → đúng một bản ghi, tất cả nhận cùng kết quả; `FailRate=1` → `Success=false`; `GetByOrder` trả `null` khi chưa có.
- **Không làm:** refund.

## T15 — Ordering: domain + dữ liệu
- **SPEC:** §6 (ordering), §8.4 (trạng thái).
- **Tạo/sửa:** `OrderingDbContext`, `Order` (aggregate), `OrderLine`, `OutboxMessage`, migration (unique `(user_id, idempotency_key)`, index một phần, `jsonb`, cột `correlation_id` của outbox), Contracts (`OrderCreated`, `OrderCreatedLine`).
- **Yêu cầu:**
  - `Order.CreatePending(...)` nhận `id`, `reservationId`, `idempotencyKey`, `requestHash`, lines; tính `total`.
  - Chuyển trạng thái qua các phương thức domain; **việc ghi DB cho `Pending→Paid/Failed` là `ExecuteUpdateAsync` có điều kiện** `WHERE status='Pending'` trả `bool` (thành công khi 1 hàng). Cung cấp qua một repository nội bộ (`IOrderStore`) để dễ mock.
  - `Order` không cho phép chuyển từ trạng thái cuối.
- **Nghiệm thu:** unit test `Order` (total đúng, không chuyển từ `Paid`/`Failed`); integration test cho `IOrderStore`: chuyển trạng thái hai lần song song → đúng một thành công; vi phạm unique `(user_id, idempotency_key)` ném lỗi mà `IsUniqueViolation` nhận diện đúng.
- **Không làm:** chưa PlaceOrder.

## T16 — Ordering: tính giá
- **SPEC:** §8.4 (Giá), §5 (làm tròn), §11.
- **Tạo/sửa:** `IDiscountRule`, `FlashSaleRule` (Order 10), `BulkDiscountRule` (Order 20), `PriceCalculator`, `OrderOptions` (bind `Order:*`).
- **Yêu cầu:** áp dụng rule theo `Order` tăng dần; `BulkDiscountRule` bỏ qua dòng flash sale; làm tròn `AwayFromZero` về VND nguyên.
- **Nghiệm thu:** unit test bảng giá: (a) thường, quantity 1; (b) thường, quantity = ngưỡng → giảm đúng; (c) flash sale, quantity ≥ ngưỡng → **không** giảm thêm; (d) làm tròn tại giá trị `.5`; (e) tổng nhiều dòng.
- **Không làm:** không thêm rule ngoài hai rule trên.

## T17 — Ordering: PlaceOrder + API
- **SPEC:** §8.4 (toàn bộ), §9.1, §9.2, §7.
- **Tạo/sửa:** `PlaceOrderHandler`, `PlaceOrderCommand/Result` (kết quả dạng union: `Created`, `Replayed`, `Failed(code)`, `InProgress`, ...), endpoints `POST /orders`, `GET /orders`, `GET /orders/{id}`, validator, request hash.
- **Yêu cầu:**
  - Cài đặt **đúng thứ tự P0–P9** của §8.4, không thêm/bớt bước. Dùng các tên cấu hình §11.
  - Kiểm tra `Idempotency-Key` (8–64 ký tự `[A-Za-z0-9_-]`) và body; `requestHash` là SHA-256 của JSON chuẩn hóa items sắp theo `skuId`.
  - `catch` ở P4 chỉ xử lý unique violation đúng constraint `(user_id, idempotency_key)`.
  - Exception ở bước Charge **không** Release và trả 503 `PAYMENT_UNAVAILABLE`.
  - Ánh xạ kết quả → HTTP đúng bảng §9.2, kèm `Idempotent-Replayed: true` và `Retry-After: 2` khi cần.
  - `GET /orders*` chỉ trả đơn của chính user (id lấy từ claim `sub`); đơn người khác → 404.
  - Ghi log có cấu trúc ở mỗi bước (orderId, userId, bước).
  - Khi đã có outbox: chưa cần publisher (T21); chỉ insert hàng.
- **Nghiệm thu:** unit test handler với mock `ICatalogService`, `IInventoryService`, `IPaymentService`, `IOrderStore` cho từng nhánh: thành công; SKU thiếu → 422 (không lưu đơn); Reserve thất bại → Failed(OUT_OF_STOCK); Charge declined → có gọi Release và Failed(PAYMENT_DECLINED); Charge ném exception → không Release, Pending, 503; Commit `false` → Failed(RESERVATION_LOST); chuyển Paid trả 0 hàng → trả theo đơn hiện có; replay cho mỗi trạng thái (Paid/Failed các lý do/Pending); hash khác → 422.
- **Không làm:** reaper (T19), publisher (T21), tests tích hợp (T18).

## T18 — Ordering: integration tests
- **SPEC:** §12 (E1–E5), §2.
- **Tạo/sửa:** test với `WebApplicationFactory<Program>`, Postgres Testcontainers, `Messaging:Enabled=false`, module thật (Catalog, Inventory, Payment thật). Seed bằng gọi trực tiếp DbContext hoặc API admin. `Payment:FailRate` và độ trễ ghi đè theo từng test.
- **Các test bắt buộc:**
  1. Happy path: 201, kho −1, đúng 1 payment, đúng 1 hàng outbox `OrderCreated` với `Paid`.
  2. Cùng key hai lần tuần tự: lần hai `200` + `Idempotent-Replayed: true`, cùng `orderId`; số payment/outbox/kho không đổi.
  3. **Cùng key, 20 request song song:** đúng một `201`; các request còn lại là `200` hoặc `409 ORDER_IN_PROGRESS`; payments = 1; kho giảm đúng 1; đúng 1 đơn.
  4. Cùng key, body khác → `422 IDEMPOTENCY_KEY_REUSED`.
  5. Hết hàng → `409`, đơn `Failed(OUT_OF_STOCK)`, replay vẫn `409`.
  6. `FailRate=1` → `402`, kho được hoàn đủ, replay vẫn `402`.
  7. Thiếu/sai `Idempotency-Key` → 400. Sản phẩm inactive → 422 và không có hàng `orders`.
  8. `GET /orders/{id}` của người khác → 404; `GET /orders` phân trang + sắp xếp mới nhất trước.
  9. 100 user khác nhau song song, tồn 10 → đúng 10 `Paid`, 90 `409`, `available = 0`.
  10. Tính bất biến cuối mỗi test: `tồn ban đầu = available + Σ quantity (Held+Committed)`.
- **Nghiệm thu:** `dotnet test --filter Ordering` xanh 3 lần liên tiếp. Dán output.
- **Không làm:** không dùng mock cho module thật.

## T19 — Ordering: PendingOrderReaper
- **SPEC:** §8.4 (Reaper, Ràng buộc cấu hình), §11.
- **Tạo/sửa:** `PendingOrderReaper : BackgroundService` (có `RunOnceAsync`), kiểm tra cấu hình lúc khởi động.
- **Yêu cầu:** đúng 3 bước §8.4; chu kỳ 30 giây; dùng chuyển trạng thái có điều kiện; khởi động ném lỗi rõ nếu `Order:PendingTimeoutMinutes >= Inventory:ReservationTtlMinutes`; đặt timeout request tổng 30 giây cho endpoint `POST /orders`.
- **Nghiệm thu:** integration test với `FakeTimeProvider`: (1) Pending không thanh toán → `Failed(EXPIRED)`, reservation `Released`, kho hoàn; (2) Pending có thanh toán thành công, reservation `Held` → Commit + `Paid` + một hàng outbox; (3) Pending có thanh toán thành công nhưng reservation đã `Released` → `Failed(RESERVATION_LOST)` + log Critical (kiểm bằng logger giả); (4) **race:** reaper chuyển đơn trước, sau đó handler cố chuyển Paid → handler nhận 0 hàng và trả theo trạng thái hiện có, không ghi đè; (5) cấu hình sai (timeout ≥ TTL) → host không khởi động.
- **Không làm:** không refund.

---

# TUẦN 5 — Messaging & Notification

## T20 — Messaging: bus + consumer nền
- **SPEC:** §10, §8.4 (Outbox), §5 (correlation), §11.
- **Tạo/sửa:** trong BuildingBlocks hoặc project Messaging nội bộ: `IMessageBus` + `RabbitMessageBus`, `RabbitConsumer<T>` (lớp nền), khai báo topology (exchange, DLX, DLQ, queue) idempotent, health check RabbitMQ thêm vào `/health/ready`.
- **Yêu cầu:**
  - Publisher **bật publisher confirms** (với `RabbitMQ.Client` 7.x: tạo channel có bật confirmations và tracking; **xác nhận tên/chữ ký API khi build**, không đoán). `PublishAsync` chỉ trả về sau khi broker xác nhận, ném lỗi nếu bị nack/timeout.
  - Message: `MessageId`, `Type`, `Persistent`, `application/json`, header `x-correlation-id`.
  - Consumer: `prefetch=10`, ack thủ công, retry tối đa 3 (2 s, 4 s), sau đó nack không requeue; exception thuộc nhóm không-thể-retry (`JsonException`) → nack ngay; phục hồi `x-correlation-id` vào `CorrelationContext`; automatic recovery bật; khởi động thử lại 20 lần cách 3 giây.
  - Khi `Messaging:Enabled=false` không đăng ký gì.
- **Nghiệm thu:** integration test (Testcontainers RabbitMQ): publish → consumer nhận đúng nội dung và correlation ID; handler ném lỗi 2 lần rồi thành công → được ack, đúng 3 lần gọi; handler luôn lỗi → message nằm trong `order.events.dead`; JSON hỏng → vào DLQ ngay (không retry); dừng broker khi publish → `PublishAsync` ném lỗi (không giả thành công).
- **Không làm:** chưa Outbox publisher, chưa Notification.

## T21 — Outbox publisher
- **SPEC:** §8.4 (Outbox publisher), §10.
- **Tạo/sửa:** `OutboxPublisher : BackgroundService` trong Ordering.
- **Yêu cầu:** vòng lặp: mở transaction → `SELECT ... WHERE processed_at IS NULL ORDER BY occurred_at LIMIT 50 FOR UPDATE SKIP LOCKED` → phát từng message qua `IMessageBus` (chờ confirm) → đặt `processed_at` cho những message đã xác nhận → commit. Một message thất bại: dừng lô, giữ nguyên các message chưa xác nhận (không đánh dấu). Tạm nghỉ khi lô rỗng. `MessageId` = `outbox.id`. Không giữ transaction qua chu kỳ nghỉ.
- **Nghiệm thu:** integration test (Postgres + RabbitMQ): chèn 100 hàng outbox → hàng đợi nhận đủ 100 (không thiếu), tất cả có `processed_at`; **hai** instance publisher chạy đồng thời → mỗi message xuất hiện đúng 1 lần trong trường hợp bình thường; dừng RabbitMQ → hàng vẫn `processed_at IS NULL`, bật lại → được phát; message nack → không đánh dấu.
- **Không làm:** không xóa hàng outbox (dọn dẹp để ngoài phạm vi).

## T22 — Notification
- **SPEC:** §6 (notification), §8.6, §10, §11.
- **Tạo/sửa:** `NotificationDbContext`, migration, `INotificationChannel` + `FakeEmailChannel`, `OrderCreatedConsumer`, đăng ký có điều kiện theo `Notification:ConsumerEnabled`, Contracts rỗng ngoài `INotificationService` (dùng ở T27).
- **Yêu cầu:** xử lý đúng 5 bước §8.6 trong **một transaction** (kể cả gọi `SendAsync` trước commit); unique `(order_id, channel)` là lớp chống trùng thứ hai; exception không-thể-retry → DLQ ngay.
- **Nghiệm thu:** integration test: cùng `MessageId` gửi hai lần → đúng 1 hàng `notifications`; handler gửi lỗi tạm thời (kênh giả ném 1 lần) → retry và thành công, không có hàng trùng; JSON hỏng → ở `order.events.dead`; `Notification:ConsumerEnabled=false` → không có consumer nhận message.
- **Không làm:** không thêm kênh thật (SMTP, SMS).

## T23 — Kịch bản chịu lỗi messaging
- **SPEC:** §12 (E7, E8).
- **Tạo/sửa:** `docs/runbooks/messaging-resilience.md` và một script `tools/resilience-check.sh` (curl + docker compose).
- **Yêu cầu:** runbook gồm lệnh chính xác và kết quả mong đợi cho: (a) đặt `Notification__ConsumerEnabled=false` ở cả hai API, `docker compose up -d`, đặt 5 đơn → 201, queue tăng; bật lại → queue về 0, `notifications` có đúng 5 hàng; (b) `docker compose stop rabbitmq`, đặt 5 đơn → vẫn 201 và 5 hàng outbox `processed_at IS NULL`; `start rabbitmq` → được phát, 5 thông báo, không trùng; (c) `docker compose kill api1` giữa lúc tải nhẹ → NGINX chuyển sang api2, không đơn nào bị trừ kho mà không có `Paid`/`Failed` sau 4 phút (reaper). Mỗi kịch bản có câu SQL kiểm tra.
- **Nghiệm thu:** chạy thực tế từng kịch bản, dán output SQL và `docker compose` vào `docs/evidence/resilience-<ngày>.md`. Nếu kết quả không đúng kỳ vọng → ghi lại và báo, **không sửa kỳ vọng**.
- **Không làm:** không thay đổi mã nghiệp vụ trong task này (nếu phát hiện lỗi thì báo để mở task sửa).

---

# TUẦN 6 — Vận hành

## T24 — NGINX + health nâng cao
- **SPEC:** §12 (E10), §9.1.
- **Tạo/sửa:** `deploy/nginx/nginx.conf` (hoàn thiện), `deploy/nginx/nginx.loadtest.conf`, `docker-compose.loadtest.yml` (override mount file loadtest), `tools/check-lb.sh`.
- **Yêu cầu:** bản thường có `limit_req_zone` (ví dụ 20 r/s theo IP, burst 40) **chỉ** cho `location /orders`, trả 429 khi vượt; log format có `$upstream_addr`, `$request_time`, `$http_x_correlation_id`; `proxy_next_upstream error timeout http_502 http_503` (mặc định NGINX không retry POST, **giữ nguyên**, không bật `non_idempotent`); `proxy_connect_timeout`/`proxy_read_timeout` hợp lý (≥ 30 giây cho `/orders`); bản loadtest **không** có `limit_req`. `/health/ready` kiểm tra Postgres + RabbitMQ.
- **Nghiệm thu:** `tools/check-lb.sh` gọi 20 lần và in số lần mỗi `X-Instance` (cả hai > 0); dừng `api1` → vẫn 200 qua `api2`; bản thường bắn 100 request nhanh vào `/orders` thấy có 429; bản loadtest không có 429; `docker compose stop rabbitmq` → `/health/ready` trả 503.
- **Không làm:** HTTPS/TLS.

## T25 — Correlation xuyên suốt
- **SPEC:** §5 (correlation), §10.
- **Tạo/sửa:** kiểm tra và bổ sung: `PlaceOrderHandler` ghi correlation ID hiện tại vào cột `correlation_id` của outbox (cột đã có từ T15), `OutboxPublisher` gắn vào header message, consumer phục hồi (đã ở T20).
- **Yêu cầu:** một `X-Correlation-Id` gửi từ client phải xuất hiện ở log của API (đặt đơn), log OutboxPublisher và log consumer Notification.
- **Nghiệm thu:** integration test: gửi `POST /orders` với `X-Correlation-Id: test-123` → log (bắt bằng sink trong bộ nhớ) có `test-123` ở cả ba nơi; chạy thật bằng compose và dán một đoạn `docker compose logs | grep test-123`.
- **Không làm:** không thêm OpenTelemetry.

---

# TUẦN 7 — Load test, CI/CD

## T26 — k6 load test + bằng chứng
- **SPEC:** §2 (NFR-1, NFR-2), §12 (E1), Errata dòng k6.
- **Tạo/sửa:** `loadtest/flash-sale.js`, `loadtest/verify.sql`, `loadtest/README.md`, `docs/evidence/loadtest-<ngày>.md`.
- **Yêu cầu cho script:**
  - Chạy qua cấu hình NGINX loadtest (`docker-compose.loadtest.yml`, T24); đặt `Payment:FailRate=0`, `Payment:SimulatedLatency` như mặc định.
  - `http.setResponseCallback(http.expectedStatuses(201, 409))` để 409 không bị tính là lỗi. Threshold: `http_req_duration{name:place_order}` p(95) < 800, và một `Counter` tự đặt cho các mã ngoài `201/409` (ví dụ 5xx, 429) phải bằng 0.
  - `setup()` đăng ký/đăng nhập **theo lô** bằng `http.batch` (ví dụ 20 user mỗi lô), `setupTimeout` ≥ 180s, bỏ qua `409 EMAIL_EXISTS` khi chạy lại; mỗi VU dùng token riêng theo `__VU`; `Idempotency-Key` là UUID mới mỗi lần lặp.
  - Kịch bản: 200 VU, mỗi VU 3 lần lặp, tồn ban đầu đúng 50, cả 600 request nhắm vào cùng 1 SKU (đã seed trước bằng API admin).
  - `teardown()` hoặc bước riêng in ra số `201`/`409`.
- **`verify.sql`:** in `Paid` (kỳ vọng đúng 50), `available` (kỳ vọng 0), `Σ quantity Held+Committed` (= 50), số đơn `Pending` (kỳ vọng 0 sau khi chờ), số hàng `payments` (= số `Paid`), số hàng outbox, và kiểm tra bất biến `tồn ban đầu = available + Σ quantity`.
- **Nghiệm thu:** chạy thực tế: `docker compose -f docker-compose.yml -f docker-compose.loadtest.yml up -d --build`, seed, `k6 run loadtest/flash-sale.js`, rồi `psql -f loadtest/verify.sql`. Dán **số thật** vào file evidence kèm phiên bản, cấu hình máy, lệnh. Nếu threshold đỏ hoặc invariants sai: báo đúng sự thật, không chỉnh ngưỡng cho qua.
- **Bản "trước" (để so sánh):** tùy chọn, tạo nhánh `demo/unsafe-oversell` có cách đọc-rồi-ghi không an toàn, chạy cùng kịch bản để chứng minh bán vượt; **không bao giờ merge nhánh này**.
- **Không làm:** không tối ưu hiệu năng trong task này.

## T27 — (Tuỳ chọn) Benchmark sync vs async
- **SPEC:** §11 (`Benchmark:InlineNotification`), §7 (`INotificationService`).
- **Mục tiêu:** có số liệu p95 chứng minh lợi ích của outbox + consumer bất đồng bộ.
- **Tạo/sửa:** `NotificationService : INotificationService` (dùng lại logic của consumer), cờ `Benchmark:InlineNotification` trong `PlaceOrderHandler` (khi bật: gọi `NotifyOrderPaidAsync` ngay trong request thay vì chỉ ghi outbox), thêm tham chiếu `Ordering → Notification.Contracts` và cập nhật bảng cho phép trong architecture test (đúng như SPEC §4 đã nêu).
- **Yêu cầu:** đặt `Notification:SimulatedLatencyMs=200`. Chạy k6 (T26) hai lần: `InlineNotification=false` rồi `true`. Mặc định luôn `false`.
- **Nghiệm thu:** bảng so sánh p50/p95/p99 và throughput trong `docs/evidence/async-vs-sync-<ngày>.md`; cờ mặc định `false` được xác nhận bằng test cấu hình; architecture test xanh.
- **Không làm:** không để cờ bật trong môi trường dev/staging.

## T28 — CI/CD
- **SPEC:** §12 (E12), §2 (NFR-5), §11.
- **Tạo/sửa:** `Jenkinsfile`, `deploy/deploy.sh`, `deploy/rollback.sh`, `deploy/env/*.example`, cập nhật `docker-compose.yml` để dùng `image: ${REGISTRY_IMAGE:-shopflow-api}:${TAG:-dev}` và `NGINX_PORT`. Tùy chọn: `.github/workflows/ci.yml` tương đương.
- **Yêu cầu:**
  - Stages: Checkout → Restore → Build → Test (`dotnet test`, gồm unit + architecture + integration; agent cần truy cập Docker cho Testcontainers) → Docker build + gắn tag `${GIT_COMMIT_SHORT}` và `latest-<env>` → Push → Deploy.
  - Test đỏ → pipeline dừng, **không** build/push image.
  - Môi trường theo nhánh: `develop` → dev, `main` → staging. Mỗi môi trường có file env riêng ngoài Git (`/etc/shopflow/<env>.env`), project compose riêng (`-p shopflow-<env>`), cổng khác nhau, `Jwt:Secret` khác nhau.
  - `deploy.sh`: ghi lại tag đang chạy vào `.last-good-tag` **trước** khi đổi; `docker compose up -d`; chờ `/health/ready` tối đa 90 giây; thất bại → tự gọi `rollback.sh`.
  - `rollback.sh TAG`: deploy lại tag chỉ định (mặc định `.last-good-tag`), kiểm tra health.
  - Migration chạy qua service `migrator` trước khi api khởi động; ghi chú quy ước "migration tương thích ngược" trong README.
- **Nghiệm thu:** (1) cố ý làm một test đỏ → pipeline dừng ở Test, không có image mới; (2) commit bình thường → deploy thành công, `/health/ready` 200; (3) deploy một tag cố tình hỏng (ví dụ cấu hình sai) → script tự rollback và health 200 về tag cũ; đo thời gian rollback (< 5 phút). Dán log/ảnh chụp vào `docs/evidence/cicd-<ngày>.md`. Nếu không có Jenkins thật để chạy: nói rõ là chưa chạy, và chạy được `deploy.sh`/`rollback.sh` cục bộ để chứng minh phần script.
- **Không làm:** không Kubernetes, không blue-green.

---

# TUẦN 8 — Tách gRPC

## [x] T29 — gRPC: Inventory Service
- **SPEC:** §14 (giới hạn khi tách), §8.3, §11 (`Inventory:Mode`, `ConnectionStrings:Inventory`).
- **Tạo/sửa:** project `ShopFlow.Inventory.Service` (ASP.NET Core + `Grpc.AspNetCore`), `inventory.proto`, `InventoryGrpcService`, `docker-compose.grpc.yml`, `deploy/nginx/nginx.grpc.conf`, DB riêng.
- **Yêu cầu:**
  - [x] `inventory.proto` định nghĩa `Reserve`, `Commit`, `Release` khớp ngữ nghĩa §7 (`ReserveReply` có `success`, `reason`, `failed_sku_id`; `Commit` trả `bool`).
  - [x] Service tham chiếu module Inventory (dùng `AddInventoryModule` công khai, không dùng kiểu `internal`), gọi `IInventoryService`; chạy sweeper trong service này.
  - [x] Hai cổng Kestrel: `8080` HTTP/1.1 cho REST admin (`/admin/inventory`, dùng lại `AddShopFlowJwtAuth` và policy `AdminOnly`) và health; `8081` HTTP/2 (h2c) cho gRPC.
  - [x] DB riêng: container `db-inventory`, database `shopflow_inventory`, service có cờ `--migrate`. Runbook di chuyển dữ liệu tồn kho hiện có (`pg_dump -n inventory`) trong `docs/runbooks/inventory-extraction.md`.
  - [x] NGINX bản gRPC route `/admin/inventory` tới `inventory-service:8080`.
  - [x] Trong chế độ `Grpc`, **Host không đăng ký** module Inventory (không sweeper, không endpoint admin inventory ở Host).
  - [x] Cập nhật architecture test để project `ShopFlow.Inventory.Service` được phép tham chiếu module Inventory (là host riêng của module đó); các quy tắc khác giữ nguyên.
- **Nghiệm thu:** `docker compose -f docker-compose.yml -f docker-compose.grpc.yml up -d` → `inventory-service` healthy; dùng `grpcurl` (hoặc client test) gọi `Reserve`/`Commit`/`Release` thành công; REST admin đặt tồn qua NGINX thành công; test tích hợp cho `InventoryGrpcService` qua `WebApplicationFactory<ServiceProgram>` + `GrpcChannel` xanh (hết hàng, idempotent theo `ReservationId`, commit sau release).
- **Không làm:** chưa viết client trong Ordering (T30).

## [x] T30 — gRPC: client + chế độ kép
- **SPEC:** §7, §11, §14, §12 (E13).
- **Tạo/sửa:** project `ShopFlow.Inventory.GrpcClient` (chứa stub sinh từ proto + `InventoryGrpcClient : IInventoryService`), đăng ký trong Host theo `Inventory:Mode`.
- **Yêu cầu:**
  - [x] Adapter ánh xạ đúng ngữ nghĩa §7. Đặt deadline (ví dụ 3 giây) cho mỗi lời gọi. `RpcException` (`Unavailable`, `DeadlineExceeded`) được ném ra (để `PlaceOrder` xử lý: ở bước Reserve → lỗi 5xx kèm đơn `Pending`; ở bước Commit/Release → đơn `Pending` để reaper xử lý).
  - [x] Retry cho `Reserve`, `Commit`, `Release` (cả ba idempotent) tối đa 3 lần với backoff ngắn qua cấu hình service config của gRPC.
  - [x] `Inventory:Mode=InProcess` (mặc định) dùng cài đặt trong tiến trình; `Grpc` dùng client. Không đổi một dòng nào trong `PlaceOrderHandler`.
  - [x] **Contract tests dùng chung:** một bộ test trừu tượng `InventoryServiceContractTests` chạy trên (a) `InventoryService` trong tiến trình, (b) `InventoryGrpcClient` kết nối tới service thật qua TestServer. Cả hai phải cho cùng kết quả cho các kịch bản T13 (1, 3, 4, 5, 6).
- **Nghiệm thu:** contract tests xanh ở cả hai chế độ; chạy lại toàn bộ T18 với `Inventory:Mode=Grpc` (nếu khó thì chạy bản nhỏ bằng compose + k6 với 50 VU, tồn 10) và `verify.sql` đúng; dừng `inventory-service` giữa lúc tải → đơn không bị thất lạc (`Pending` rồi reaper xử lý, kho không âm); ghi kết quả vào `docs/evidence/grpc-<ngày>.md` (bao gồm so sánh p95 InProcess và Grpc).
- **Không làm:** không thêm service discovery, mTLS.

## [x] T31 — Tài liệu + nghiệm thu cuối
- **SPEC:** toàn bộ §12, §14.
- **Tạo/sửa:** `README.md`, `docs/adr/ADR-001..005.md`, `docs/architecture.md` (sơ đồ mermaid), `docs/evidence/metrics.md`, dọn dẹp.
- **Yêu cầu:**
  - [x] `README.md`: mô tả, sơ đồ kiến trúc, cách chạy (`cp .env.example .env`, `docker compose up -d --build`), cách chạy test, cách chạy load test, cấu trúc thư mục, giới hạn đã biết (§14).
  - [x] 5 ADR (mỗi cái ghi bối cảnh, lựa chọn, đánh đổi, hệ quả): ADR-001 Modular Monolith + ranh giới module; ADR-002 giữ hàng bằng `UPDATE ... WHERE available >= q` + điểm nghẽn hàng nóng (số đo T26); ADR-003 Outbox + consumer idempotent + gửi trong transaction; ADR-004 Idempotency (Pending-first) và Commit đồng bộ; ADR-005 tách Inventory qua gRPC và bù trừ thay cho giao dịch cục bộ.
  - [x] Chạy lần lượt **E1–E14** của SPEC §12 và điền bảng kết quả vào `docs/evidence/acceptance.md` (đạt/không đạt, bằng chứng, lệnh).
  - [x] `docs/evidence/metrics.md`: tổng hợp **chỉ các số đã đo thật** từ T26/T27/T30 (p95, throughput, thời gian rollback, số test).
  - [x] Dọn dẹp: gỡ endpoint thử `GET /admin/ping` nếu không còn dùng; `git log -p | grep -iE "password|secret|apikey"` không lộ bí mật thật; `.env` không có trong Git.
- **Nghiệm thu:** `dotnet test` toàn bộ xanh; checklist E1–E14 điền đầy đủ; README đủ để người mới chạy được hệ thống trong ≤ 15 phút (thử bằng cách làm theo từng lệnh trên một thư mục sạch).
- **Không làm:** không thêm tính năng mới.

---

## Phụ lục — Vòng lặp xử lý khi một task thất bại

1. Test đỏ do mã: sửa mã trong phạm vi task.
2. Test đỏ do đặc tả sai/thiếu: dừng, ghi `docs/OPEN_QUESTIONS.md`, hỏi người phụ trách. Không đổi test.
3. Phát hiện lỗi ở task đã hoàn thành trước đó: **không** sửa lén. Ghi vào `OPEN_QUESTIONS.md` kèm cách tái hiện, đề xuất mở task sửa riêng (đặt tên `Tnn-fix-<mô tả>`), rồi tiếp tục task hiện tại nếu không bị chặn.
4. Hạ tầng bên ngoài không chạy được (Docker, Jenkins, k6 không có): nói rõ chưa chạy phần nào, chạy phần còn lại, không tuyên bố hoàn thành phần chưa chạy.
