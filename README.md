# ShopFlow – High-Performance Flash Sale E-Commerce Backend

[![.NET 10](https://img.shields.io/badge/.NET-10.0%20(LTS)-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql)](https://www.postgresql.org/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3.13-FF6600?logo=rabbitmq)](https://www.rabbitmq.com/)
[![MassTransit](https://img.shields.io/badge/MassTransit-8.3.4-2E8555)](https://masstransit.io/)
[![MinIO](https://img.shields.io/badge/MinIO-S3--Compatible-C72C48?logo=minio)](https://min.io/)
[![Docker Compose](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://www.docker.com/)
[![NGINX](https://img.shields.io/badge/NGINX-Alpine-009639?logo=nginx)](https://nginx.org/)

**ShopFlow** là giải pháp backend thương mại điện tử chuyên biệt cho các chiến dịch **Flash Sale**, được thiết kế theo kiến trúc **Modular Monolith** chặt chẽ, đảm bảo **tuyệt đối không bán vượt tồn kho (Zero-Overselling)**, xử lý việc phụ bất đồng bộ qua **RabbitMQ + Transactional Outbox Pattern**, và có sẵn cơ chế tiến hóa tách module thành **gRPC Microservice**.

---

## 🏛️ Kiến trúc tổng thể (Modular Monolith)

Hệ thống được chia thành 6 module độc lập ranh giới, mỗi module quản lý một schema riêng biệt trên PostgreSQL và giao tiếp với nhau duy nhất qua các project `*.Contracts`:

```
ShopFlow/
├── src/
│   ├── ShopFlow.Host/             # Composition Root, API Gateway middleware, Swagger
│   ├── ShopFlow.BuildingBlocks/   # Shared Messaging, Correlation, Auth, Errors
│   └── Modules/
│       ├── Identity/              # Quản lý người dùng, JWT Bearer, Refresh Token
│       ├── Catalog/               # Sản phẩm, danh mục, Flash Sale, upload ảnh (MinIO S3)
│       ├── Inventory/             # Quản lý kho, giữ hàng (Reserve) TTL, hỗ trợ InProcess & gRPC
│       ├── Ordering/              # Xử lý đơn hàng Idempotent, giá flash sale, Outbox
│       ├── Payment/               # Xử lý thanh toán mô phỏng (FakeGateway)
│       └── Notification/          # Xử lý thông báo bất đồng bộ từ RabbitMQ consumer
├── deploy/                        # Docker Compose, NGINX Load Balancer configs
├── docs/                          # Tài liệu đặc tả kỹ thuật, ADR, kết quả benchmark k6
└── tests/                         # Unit tests, Integration tests (Testcontainers), Architecture tests
```

### 🎯 Quy tắc thiết kế cốt lõi
1. **Schema-Per-Module Isolation**: Mỗi module sở hữu schema PostgreSQL riêng (`identity`, `catalog`, `inventory`, `ordering`, `payment`, `notification`). Cấm tuyệt đối truy cập hoặc JOIN chéo schema.
2. **Compile-time Boundary**: Các module chỉ được phép tham chiếu project `*.Contracts` của module khác, kiểu triển khai (`internal`) được kiểm soát bằng Architecture Test (`NetArchTest`).
3. **Dual-Mode Inventory**: Module `Inventory` có thể chạy **InProcess** trực tiếp hoặc tách riêng thành **gRPC service** độc lập mà không cần sửa đổi nghiệp vụ của `Ordering`.

---

## 🛠️ Công nghệ sử dụng

| Lĩnh vực | Công nghệ / Thư viện | Phiên bản |
|---|---|---|
| **Platform** | .NET 10 (LTS, `net10.0`), C# 13, ASP.NET Core Minimal API | 10.0 |
| **Database & ORM** | PostgreSQL 16, Entity Framework Core 10, Npgsql | 10.0.12 / 10.0.3 |
| **Messaging** | RabbitMQ, MassTransit (Transactional Outbox & Consumer) | 3.13 / 8.3.4 |
| **Object Storage** | MinIO (AWS S3 Compatible), AWS SDK for .NET S3 | 3.7.411.3 |
| **Microservice IPC** | gRPC for .NET, Protocol Buffers (`Google.Protobuf`) | 2.84.0 |
| **Reverse Proxy** | NGINX Alpine (Load balancer round-robin 2 instances API) | Alpine |
| **Authentication** | JWT Bearer, BCrypt.Net-Next | 10.0.12 / 4.2.0 |
| **Logging & Metrics** | Serilog, HealthChecks (NpgSql, RabbitMQ, S3) | 10.0.0 / 9.0.0 |
| **Testing** | xUnit, NSubstitute, Testcontainers (PostgreSQL), k6 | 2.9.3 / 4.16.0 |

---

## ⚡ Cơ chế chống bán vượt (Zero-Overselling)

1. **Conditional Atomic Update**:
   ```sql
   UPDATE inventory.stock_items 
   SET available = available - @Quantity, updated_at = @Now
   WHERE sku_id = @SkuId AND available >= @Quantity;
   ```
   * PostgreSQL khóa hàng trong lúc update. Nếu `available < Quantity`, câu lệnh ảnh hưởng 0 hàng -> Lập tức trả về lỗi `OUT_OF_STOCK` (HTTP 409) mà không cần khóa bi quan dài hạn.
   * Ràng buộc cứng `CHECK (available >= 0)` tại database là lớp bảo vệ cuối cùng.

2. **TTL Reservation & Sweeper**:
   * Khi đơn hàng bắt đầu, hàng được chuyển vào trạng thái `Held` kèm thời gian hết hạn (`expires_at`).
   * Background service `ReservationSweeper` quét định kỳ để hoàn lại tồn kho cho các đơn hàng quá hạn thanh toán.

3. **Idempotency & Transactional Outbox**:
   * API `POST /orders` yêu cầu header bắt buộc `Idempotency-Key`.
   * Sự kiện `OrderCreated` được ghi đồng thời vào bảng `outbox_messages` trong cùng một database transaction trước khi được phát lên RabbitMQ, đảm bảo không bao giờ mất message.

---

## 🚀 Hướng dẫn khởi chạy bằng Docker Compose

### 1. Yêu cầu hệ thống
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) đã cài đặt và đang chạy.

### 2. Thiết lập biến môi trường
Sao chép file cấu hình mẫu:
```bash
cp .env.example .env
```

### 3. Khởi chạy toàn bộ hệ thống
Khởi động tất cả các service (Postgres, RabbitMQ, MinIO, Migrator/Seeder, 2 API Instances, NGINX):
```bash
docker compose up -d
```

### 4. Kiểm tra sức khỏe hệ thống (Health Check)
```bash
# Kiểm tra Liveness probe
curl http://localhost:8080/health/live

# Kiểm tra Readiness probe (Postgres, RabbitMQ, MinIO S3)
curl http://localhost:8080/health/ready
```
Kết quả trả về HTTP 200 `{"status": "Healthy"}` chứng tỏ hệ thống đã sẵn sàng 100%.

---

## 📖 Tài liệu API & Endpoints

Tài liệu tương tác trực quan được tích hợp sẵn qua Swagger UI:
👉 **[http://localhost:8080/swagger](http://localhost:8080/swagger)**

### Danh sách Endpoint chính:

| Nhóm | Method | Endpoint | Quyền hạn | Mô tả |
|---|---|---|---|---|
| **Auth** | `POST` | `/auth/register` | Public | Đăng ký tài khoản người dùng |
| | `POST` | `/auth/login` | Public | Đăng nhập lấy JWT Access & Refresh Token |
| | `POST` | `/auth/refresh` | Public | Cấp lại access token |
| **Catalog** | `GET` | `/products` | Public | Danh sách sản phẩm kèm phân trang |
| | `GET` | `/products/{id}` | Public | Chi tiết sản phẩm kèm Presigned URL ảnh từ MinIO |
| | `POST` | `/products` | Admin | Tạo mới sản phẩm |
| | `POST` | `/admin/flash-sales` | Admin | Thiết lập chiến dịch Flash Sale |
| **Inventory** | `GET` | `/admin/inventory/{skuId}` | Admin | Tra cứu tồn kho SKU |
| | `PUT` | `/admin/inventory/{skuId}` | Admin | Nhập thêm tồn kho SKU |
| **Ordering** | `POST` | `/orders` | User | Đặt hàng flash sale (bắt buộc `Idempotency-Key`) |
| | `GET` | `/orders` | User | Xem lịch sử đơn hàng của bản thân |
| | `GET` | `/orders/{id}` | User | Xem chi tiết đơn hàng |
| **Health** | `GET` | `/health/live` | Public | Liveness probe |
| | `GET` | `/health/ready` | Public | Readiness probe kiểm tra DB, Queue, Storage |

---

## 📚 Tài liệu chi tiết trong repository

* **[docs/spec/01_SPEC.md](docs/spec/01_SPEC.md)**: Đặc tả chi tiết nguồn sự thật duy nhất (Single Source of Truth).
* **[docs/spec/ShopFlow_Implementation_Guide.md](docs/spec/ShopFlow_Implementation_Guide.md)**: Hướng dẫn triển khai từng module và kinh nghiệm thực chiến.
* **[docs/PACKAGES.md](docs/PACKAGES.md)**: Danh mục các package NuGet và phiên bản chính xác.
* **[docs/adr/](docs/adr/)**: Thư mục ghi lại các quyết định kiến trúc (ADR-001 đến ADR-005).

---

## 📄 License
Dự án được phát triển cho mục đích học tập và nghiên cứu kiến trúc backend hiệu năng cao.
