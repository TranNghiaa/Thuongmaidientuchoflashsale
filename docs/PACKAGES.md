# Packages & Dependencies

Danh sách chi tiết các thư viện và phiên bản chính xác được quản lý tập trung trong [`Directory.Packages.props`](file:///d:/Maychu/Directory.Packages.props):

### 1. Framework & Runtime
- **Target Framework**: `.NET 10.0` (`net10.0`)
- `Microsoft.AspNetCore.OpenApi`: `10.0.12`
- `Microsoft.AspNetCore.Http`: `2.3.13`
- `Microsoft.Extensions.Configuration.Abstractions`: `10.0.0`
- `Microsoft.Extensions.DependencyInjection.Abstractions`: `10.0.0`
- `Microsoft.Extensions.Logging.Abstractions`: `10.0.0`

### 2. Database & ORM
- `Microsoft.EntityFrameworkCore`: `10.0.12`
- `Microsoft.EntityFrameworkCore.Design`: `10.0.12`
- `Npgsql`: `10.0.3`
- `Npgsql.EntityFrameworkCore.PostgreSQL`: `10.0.3`

### 3. Messaging & Event Bus (RabbitMQ + Outbox)
- `MassTransit`: `8.3.4`
- `MassTransit.EntityFrameworkCore`: `8.3.4`
- `MassTransit.RabbitMQ`: `8.3.4`

### 4. Object Storage (MinIO)
- `AWSSDK.S3`: `3.7.411.3` (S3 compatible SDK cho MinIO)

### 5. Microservice & gRPC
- `Grpc.AspNetCore`: `2.84.0`
- `Grpc.Net.Client`: `2.84.0`
- `Grpc.Tools`: `2.84.0`
- `Google.Protobuf`: `3.36.2`

### 6. Authentication & Security
- `Microsoft.AspNetCore.Authentication.JwtBearer`: `10.0.12`
- `BCrypt.Net-Next`: `4.2.0`

### 7. Logging & Diagnostics
- `Serilog.AspNetCore`: `10.0.0`
- `Swashbuckle.AspNetCore` (Swagger): `10.3.0`

### 8. Health Checks
- `AspNetCore.HealthChecks.NpgSql`: `9.0.0`
- `AspNetCore.HealthChecks.Rabbitmq`: `9.0.0`
- `AspNetCore.HealthChecks.Aws.S3`: `9.0.0`

### 9. Testing & Quality
- `Microsoft.NET.Test.Sdk`: `17.13.0`
- `xunit`: `2.9.3`
- `xunit.runner.visualstudio`: `3.0.2`
- `coverlet.collector`: `6.0.4`
- `NSubstitute`: `6.2.0`
- `NetArchTest.Rules`: `1.3.2`
- `Testcontainers.PostgreSql`: `4.16.0`
- `Microsoft.AspNetCore.Mvc.Testing`: `10.0.12`
