# ADR-005: Tách Inventory service bằng gRPC và sử dụng chế độ Dual-mode

## Context
Theo SPEC §14, hệ thống ban đầu được thiết kế theo kiến trúc Modular Monolith để giảm độ phức tạp khi deploy và quản lý transaction (sử dụng in-process calls giữa các module). Tuy nhiên, có yêu cầu tách module Inventory ra thành một microservice độc lập để chịu tải tốt hơn trong các sự kiện Flash Sale và cho phép scale riêng biệt. Quá trình tách cần đảm bảo không phá vỡ logic cốt lõi của hệ thống và cho phép chuyển đổi mượt mà giữa hai kiến trúc.

## Quyết định (Decision)
1. Sử dụng **gRPC** để giao tiếp giữa Host (đóng vai trò client, đặc biệt là Ordering module) và Inventory service.
2. Thiết kế chế độ **Dual-mode** thông qua cấu hình `Inventory:Mode` (InProcess hoặc Grpc).
3. Đóng gói gRPC client vào project `ShopFlow.Inventory.GrpcClient` và implement interface `IInventoryApi` tương tự như API in-process.

## Đánh đổi (Trade-offs)
*   **Ưu điểm**:
    *   Hỗ trợ chuyển đổi nhanh chóng qua cấu hình (feature toggle) giữa Monolith và Microservices mà không sửa đổi logic nghiệp vụ trong `PlaceOrderHandler`.
    *   Sử dụng gRPC mang lại hiệu suất cao với HTTP/2, payload nhị phân (protobuf) nhẹ hơn REST.
    *   Tái sử dụng chung các Contract (interfaces, DTOs).
*   **Nhược điểm**:
    *   Phải xử lý lỗi mạng (network partitions) bằng các cơ chế retry, timeout, và circuit breaker (như đã thêm retry 3 lần và deadline 3s).
    *   Gia tăng độ phức tạp khi triển khai (cần 2 container, cấu hình NGINX gRPC routing riêng).
    *   Type conflict tiềm ẩn giữa mã sinh ra từ Proto và các type đã có (đã giải quyết bằng cách tách project Client chỉ reference Contracts).

## Hệ quả (Consequences)
*   Module Ordering không quan tâm Inventory đang chạy ở chế độ nào. Mọi thao tác Reserve, Commit, Release vẫn đồng bộ (synchronous) đối với người gọi, nhưng ngầm gọi qua mạng nếu ở mode `Grpc`.
*   Tình huống lỗi (Unavailable, DeadlineExceeded) từ gRPC sẽ ném exception, được module Ordering bắt để đánh dấu đơn hàng là `Pending` (cho phép sweeper/reaper xử lý bù trừ sau).
