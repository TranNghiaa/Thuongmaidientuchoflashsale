# Báo Cáo Tổng Kết Chi Tiết Triển Khai ShopFlow

Báo cáo này cung cấp cái nhìn chi tiết và phân rã sâu về toàn bộ vòng đời phát triển dự án ShopFlow, được ánh xạ trực tiếp từ `02_TASKS.md` và `01_SPEC.md`.

---

## 1. Phân Rã Chi Tiết Kết Quả Các Task (T01 - T31)

### Tuần 1: Khởi Tạo & Nền Tảng (T01 - T03)
*   **T01 (Cấu trúc Solution)**: Khởi tạo toàn bộ kiến trúc Modular Monolith. Phân chia rõ ràng giữa `ShopFlow.Modules.*` và `ShopFlow.Modules.*.Contracts`. Sử dụng Central Package Management (`Directory.Packages.props`) để đồng bộ version thư viện. Cấu hình `Directory.Build.props` ép kiểu `net10.0` và `TreatWarningsAsErrors=true`.
*   **T02 (BuildingBlocks Nền)**: Viết các thành phần dùng chung:
    *   `ExceptionHandlingMiddleware` chuẩn hóa lỗi trả về theo RFC 7807 (`ProblemDetails`).
    *   `CorrelationIdMiddleware` gắn `X-Correlation-Id` vào Serilog `LogContext` để trace log phân tán.
    *   `IModuleMigrator` và helper nhận diện lỗi DB (như trùng lặp unique constraint 23505 của Postgres).
*   **T03 (Host Skeleton)**: Xây dựng `ShopFlow.Host` làm entry point. Đăng ký HealthChecks (`/health/live`, `/health/ready`), Kestrel hỗ trợ HTTP/1.1 và HTTP/2. Thêm hỗ trợ nhận diện command line arguments (`--migrate`, `--seed`, `--healthcheck`).

### Tuần 2: Identity & Access (T04 - T07)
*   **T04 & T05 (Cấu trúc Identity & Auth Middleware)**: Tạo module Identity, thiết lập `JwtOptions` lấy từ cấu hình. Cấu hình `AddAuthentication` và `AddJwtBearer`. Bảo vệ ranh giới module bằng architecture tests.
*   **T06 (Đăng ký, Đăng nhập)**: Xây dựng API `/identity/register` và `/identity/login`. Băm mật khẩu bằng `BCrypt.Net-Next`. Trả về JWT Access Token.
*   **T07 (Phân quyền & Seed)**: Mở rộng JWT để thêm claim Roles (`Admin`, `Customer`). Thêm API Refresh token. Tạo cơ chế Seed tạo sẵn user admin mặc định.

### Tuần 3: Catalog & File Upload (T08 - T09)
*   **T08 (Quản lý sản phẩm)**: Module Catalog với API CRUD cho danh mục và sản phẩm. Tích hợp `Npgsql.EntityFrameworkCore.PostgreSQL`. Tối ưu HTTP GET bằng header `Cache-Control`.
*   **T09 (Tích hợp MinIO)**: Lưu trữ ảnh sản phẩm bằng `AWSSDK.S3` trỏ tới MinIO (S3-compatible). Presigned URL để upload trực tiếp ảnh từ client. Cấu hình HealthCheck MinIO tại Host.

### Tuần 4: Inventory & Khóa Lạc Quan (T10 - T13)
*   **T10 (Dữ liệu Inventory)**: Thiết lập DB Inventory. Tách biệt hoàn toàn DB schema (`shopflow_inventory`) với các module khác.
*   **T11 (Cơ chế Reserve/Commit/Release)**: Triển khai pattern **Idempotency** (dựa vào `ReferenceId`). Tránh Race Condition khi Flash Sale bằng câu lệnh SQL trực tiếp `UPDATE ... SET available_quantity = ... WHERE available_quantity >= @requested`.
*   **T12 (Reservation Sweeper)**: Background worker (IHostedService) quét các lệnh Reserve bị treo (Pending) quá 15 phút để tự động Release trả lại hàng, chống giam hàng (dead stock).
*   **T13 (Integration Tests Đồng Thời)**: Viết các bài test giả lập gửi 10 requests giữ hàng đồng thời để chứng minh cơ chế an toàn không bao giờ bán vượt quá số tồn kho.

### Tuần 5: Ordering, Payment & Saga (T14 - T23)
*   **T14 (Mock Payment)**: Module Payment cung cấp API `ProcessPayment` mô phỏng thanh toán thành công (90%) hoặc thất bại (10%).
*   **T15 & T16 (Đặt Hàng & Tính Giá)**: Tính toán tổng tiền dựa trên gọi nội bộ tới Catalog (qua `ICatalogApi`) để lấy giá sản phẩm chính xác.
*   **T17 & T18 (PlaceOrder & Orchestration)**: Orchestrator `PlaceOrderHandler` điều phối luồng: Tạo đơn (Pending) -> Gọi Inventory giữ hàng -> Gọi Payment thanh toán -> Commit/Release Inventory. Xử lý lỗi theo mô hình **Saga Choreography/Orchestration hỗn hợp**.
*   **T19 (Pending Order Reaper)**: Xóa các đơn hàng kẹt ở trạng thái Pending nếu người dùng không thanh toán hoặc bị rớt mạng.
*   **T20 - T23 (Messaging & Transactional Outbox)**:
    *   Cài đặt **MassTransit** với RabbitMQ.
    *   Dùng **Transactional Outbox** để lưu event `OrderPaidIntegrationEvent` vào bảng outbox cùng giao dịch EF Core, sau đó MassTransit sẽ relay event lên RabbitMQ để module Notification lấy thông báo.
    *   Viết bài kịch bản chịu lỗi để chứng minh hệ thống không sập khi RabbitMQ rớt.

### Tuần 6: NGINX & Trải Nghiệm API (T24 - T25)
*   **T24 (NGINX Proxy)**: Đóng gói NGINX làm Reverse Proxy. Routing traffic `/api/catalog`, `/api/orders`. Áp dụng giới hạn Rate Limiting (10 req/s cho người dùng, 100 req/s cho admin).
*   **T25 (Correlation ID)**: Chuyền `X-Correlation-Id` qua NGINX vào Host, vào LogContext và theo suốt các HTTP request, Message Broker event.

### Tuần 7: Hiệu Năng & Tự Động Hóa (T26 - T28)
*   **T26 (k6 Load Test)**: Đo lường hiệu năng thực tế. Setup kịch bản Flash Sale với hàng trăm VU (Virtual Users). Phân tích p95 duration < 800ms. Xác thực bằng SQL script không có dòng lệnh nào bán lố hàng.
*   **T27 (Benchmark Sync vs Async)**: Báo cáo đo lường lợi ích của Outbox (Async) giúp giảm đáng kể thời gian phản hồi ở API `PlaceOrder` so với gửi trực tiếp (Sync).
*   **T28 (CI/CD)**: Viết Jenkinsfile và các Bash script (`deploy.sh`, `rollback.sh`) tự động hóa Build -> Test -> Tag Docker Image -> Deploy -> Verify Health -> Rollback nếu lỗi.

### Tuần 8: Gỡ Rối Hệ Thống & Tách gRPC (T29 - T31)
*   **T29 (Tách gRPC Inventory)**: Chuyển Inventory từ In-Process sang Microservice chạy độc lập qua giao thức gRPC (HTTP/2). Setup cổng riêng `8081` cho gRPC, `8080` cho Admin.
*   **T30 (Dual-mode Client)**: Cấu hình linh hoạt ở Host. Bật cờ `Inventory:Mode=Grpc`, Host sẽ không tiêm module Inventory cục bộ mà sử dụng `ShopFlow.Inventory.GrpcClient`. Hỗ trợ logic Timeout (3 giây) và Retry (3 lần) khi rớt mạng. Đảm bảo Contract Tests chạy Passed ở cả 2 mode.
*   **T31 (Nghiệm thu toàn hệ thống)**: Chạy full test suite E1 - E14, rà soát lại config, lập các ADR, viết báo cáo tổng kết chi tiết.

---

## 2. Chi Tiết Lệnh Đã Thực Thi Ở Các Task Trọng Điểm

*   **Tạo khung dự án (T01)**:
    ```bash
    dotnet new sln -n ShopFlow
    dotnet new webapi -n ShopFlow.Host
    dotnet new classlib -n ShopFlow.Modules.Inventory
    dotnet sln add ...
    ```
*   **Cài đặt thư viện cốt lõi (T02, T20)**:
    ```bash
    dotnet add package MassTransit.RabbitMQ
    dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
    dotnet add package Grpc.AspNetCore
    ```
*   **Build và Test xuyên suốt (T13, T18, T30)**:
    ```bash
    dotnet build ShopFlow.sln -c Release
    dotnet test tests/ShopFlow.IntegrationTests/ShopFlow.IntegrationTests.csproj
    dotnet test tests/ShopFlow.ArchitectureTests/ShopFlow.ArchitectureTests.csproj
    ```
*   **Migration DB (T10, T15)**:
    ```bash
    dotnet run --project src/ShopFlow.Host -- --migrate
    dotnet run --project src/ShopFlow.Host -- --seed
    ```
*   **Xử lý lỗi gRPC Sinh Code (T29, T30)**:
    *   Điều tra timestamps: `Get-Item .LastWriteTime`
    *   Xem compile errors: `dotnet build --verbosity detailed`
    *   Fixing Type Conflicts: Chỉnh sửa thẻ `<ProjectReference>` trong `ShopFlow.Inventory.GrpcClient.csproj` để loại trừ mã implementation của `ShopFlow.Modules.Inventory`.
*   **Thực thi Load Test (T26)**:
    ```bash
    docker compose -f docker-compose.yml -f docker-compose.loadtest.yml up -d --build
    k6 run loadtest/flash-sale.js
    psql -f loadtest/verify.sql
    ```
*   **Chạy Deploy CI/CD (T28)**:
    ```bash
    ./deploy/deploy.sh
    ./deploy/rollback.sh
    ```

---

## 3. Các Quyết Định Kỹ Thuật Chuyên Sâu (ADRs)

1.  **ADR-001 (Modular Monolith & Phân Tách)**:
    *   **Bối cảnh**: Dự án cần đi nhanh, ít chi phí DevOps ban đầu. Tuy nhiên, quy mô hệ thống tăng cao trong tương lai.
    *   **Quyết định**: Khởi tạo bằng Modular Monolith (các module chạy chung 1 tiến trình host), che giấu logic `internal`, giao tiếp qua `Contracts`. Cho phép tách rời (như Inventory) dễ dàng khi cần thiết.
2.  **ADR-002 (Giữ Hàng Bằng SQL UPDATE Khóa Lạc Quan - Không dùng Lock Mức Ứng Dụng)**:
    *   **Bối cảnh**: Flash sale dẫn đến Race Condition cực lớn lên 1 dòng DB sản phẩm.
    *   **Quyết định**: Sử dụng câu lệnh `UPDATE InventoryItems SET available = available - q WHERE id = X AND available >= q`.
    *   **Hệ quả**: Nếu rows bị ảnh hưởng = 0, báo hết hàng. DB engine tự xử lý lock row, hiệu năng cao nhất, loại trừ triệt để bán lố.
3.  **ADR-003 (Transactional Outbox Pattern)**:
    *   **Bối cảnh**: Khi lưu đơn hàng vào DB và đẩy Message lên RabbitMQ. Nếu DB commit xong nhưng RabbitMQ lỗi, message bị mất.
    *   **Quyết định**: Ghi Message vào bảng `OutboxMessage` cùng transaction với DB đơn hàng. Dùng background worker (hoặc MassTransit Outbox) quét bảng đẩy lên RabbitMQ. Đạt được Eventually Consistency.
4.  **ADR-004 (Pending-First & Idempotency)**:
    *   **Bối cảnh**: Giao tiếp qua mạng (hoặc giữa các service) bị đứt kết nối, gây lặp request.
    *   **Quyết định**: Mọi đơn hàng sinh ra luôn ở trạng thái `Pending`. Các lệnh `Reserve`, `Commit` bắt buộc truyền `ReferenceId` (Idempotent Key). Nếu gọi lại lần 2 bằng Id cũ, hệ thống tự trả về kết quả thành công mà không trừ tiền/trừ hàng lại.
5.  **ADR-005 (Kiến trúc Dual-Mode gRPC)**:
    *   **Bối cảnh**: Việc tách Inventory ra một cổng riêng có thể gây gián đoạn mã hiện tại nếu chuyển đổi đột ngột.
    *   **Quyết định**: Giữ nguyên `PlaceOrderHandler` tham chiếu tới `IInventoryApi`. Ở Host, dựa vào biến môi trường `Inventory:Mode` để DI (Dependency Injection) tiêm implementation là `InventoryModule` (In-Process) hoặc `InventoryGrpcClient` (gRPC call). Bổ sung timeout và logic retry để bao bọc lỗi mạng.

---

## 4. Ánh Xạ Task Vào Vị Trí Code Mới Nhất

*   **T01 - T03**: Kiến trúc lõi -> `src/ShopFlow.BuildingBlocks/` (Middlewares, Constants, Interfaces chung) và `src/ShopFlow.Host/Program.cs`.
*   **T04 - T08**: Cấp quyền người dùng -> `src/Modules/Identity/ShopFlow.Modules.Identity`. Tách riêng logic User Context trong `BuildingBlocks.Auth`.
*   **T09 - T11**: Quản lý Catalog & Hình ảnh MinIO -> `src/Modules/Catalog/ShopFlow.Modules.Catalog`.
*   **T12 - T16**: Module Inventory In-Process -> `src/Modules/Inventory/ShopFlow.Modules.Inventory`. Các service như `ReservationSweeper`, logic CQRS giữ hàng ở `Application/`.
*   **T17 - T20**: Xử lý Đơn hàng & Saga -> `src/Modules/Ordering/ShopFlow.Modules.Ordering`. Chứa `PlaceOrderHandler` (nhạc trưởng điều phối luồng).
*   **T21 - T23**: MassTransit Outbox -> Cấu hình trong `src/ShopFlow.BuildingBlocks/Messaging/` và các Consumer chạy ngầm nhận event ở `ShopFlow.Modules.Notification`.
*   **T24 - T25**: Cấu hình NGINX -> `deploy/nginx/nginx.conf` và các middleware gắn header Correlation ở `ShopFlow.Host`.
*   **T26 - T28**: Kịch bản Test, Bash Scripts CI/CD -> `loadtest/` (file js và sql verify) và thư mục `deploy/` (`deploy.sh`, `rollback.sh`).
*   **T29**: Service gRPC Độc Lập -> Cổng API gRPC được setup trong `ShopFlow.Modules.Inventory/Grpc/InventoryGrpcService.cs`, chia port tại `Program.cs`. File mô tả Protocol Buffers tại `ShopFlow.Modules.Inventory/Protos/inventory.proto`.
*   **T30**: Client gRPC Chế Độ Kép -> `src/Modules/Inventory/ShopFlow.Inventory.GrpcClient`. DI mở rộng tại `InventoryClientExtensions.cs`. Tests kiểm định song song hai mode ở `tests/ShopFlow.IntegrationTests/Inventory/InventoryContractTests.cs`.
*   **T31**: Tài liệu Báo Cáo -> Nằm rải rác ở gốc dự án (`README.md`), `docs/adr/`, `docs/evidence/`, và `docs/spec/02_TASKS.md` (nơi đã tick hoàn tất).
