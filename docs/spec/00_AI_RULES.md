# 00 — Quy tắc làm việc dành cho AI coder (ShopFlow)

Bộ tài liệu gồm 4 file. Đặt tất cả vào `docs/spec/` của repo:

| File | Vai trò |
|---|---|
| `00_AI_RULES.md` | File này: cách làm việc, quy tắc cứng, định nghĩa "xong" |
| `01_SPEC.md` | Đặc tả chuẩn: nghiệp vụ, dữ liệu, API, luồng, cấu hình. **Nguồn sự thật duy nhất** |
| `02_TASKS.md` | Danh sách task tuần tự. Mỗi lần chỉ làm **một** task |
| `ShopFlow_Implementation_Guide.md` | Tài liệu cũ, chỉ dùng làm **khung mã tham chiếu**. Có lỗi đã biết, xem `01_SPEC.md` §13 (Errata) |

**Thứ tự ưu tiên khi mâu thuẫn:** `01_SPEC.md` > `02_TASKS.md` > Guide cũ.

---

## 1. Quy trình cho mỗi task (làm đúng thứ tự)

1. Đọc task trong `02_TASKS.md` và **chỉ** các mục SPEC mà task trỏ tới.
2. Kiểm tra mục **Phụ thuộc** đã hoàn thành (checkbox ở bảng đầu `02_TASKS.md`). Chưa xong thì dừng và báo.
3. Liệt kê ngắn gọn kế hoạch (file sẽ tạo/sửa) trước khi viết mã.
4. Viết mã **chỉ trong phạm vi task**. Xem mục "Không làm" của task.
5. Viết test đi kèm theo mục **Nghiệm thu** của task.
6. Chạy `dotnet build` và `dotnet test` (và lệnh riêng của task nếu có). Dán kết quả thật.
7. Chạy architecture test (từ T05 trở đi). Phải xanh.
8. Tick checkbox task, commit: `T11: inventory reserve/commit/release`.
9. Báo cáo theo mẫu ở mục 5, rồi **dừng**. Không tự bắt đầu task kế tiếp.

## 2. Quy tắc cứng

1. **Một task một lần.** Không làm trước task sau, không "tiện tay" sửa module khác hay refactor ngoài phạm vi.
2. **Ranh giới module:** project module chỉ tham chiếu `*.Contracts` của module khác. Mọi kiểu triển khai là `internal`; chỉ `Contracts` và lớp `XModule` là `public`. Mỗi module chỉ đọc/ghi schema của chính nó.
3. **Không đoán phiên bản/chữ ký API.** Không hardcode số phiên bản NuGet từ trí nhớ. Dùng `dotnet add package` (bản ổn định mới nhất tương thích `net10.0`), rồi ghi phiên bản thực dùng vào `docs/PACKAGES.md`. Với `RabbitMQ.Client` 7.x, `Minio`, `NetArchTest`, `Testcontainers`, `Grpc.*`: xác nhận chữ ký bằng cách build, không viết theo trí nhớ.
4. **Không làm yếu kiểm tra để qua mặt:** không `[Fact(Skip=...)]`, không `#pragma warning disable`, không `catch { }` nuốt lỗi, không sửa test cho khớp mã sai.
5. **Thời gian** dùng `TimeProvider` được inject, luôn UTC. Không gọi `DateTime.UtcNow` trực tiếp trong mã nghiệp vụ.
6. **SQL** luôn tham số hóa (`ExecuteSqlInterpolatedAsync`, `FromSqlInterpolated`). Không nối chuỗi.
7. **Bí mật** (mật khẩu, JWT secret, key MinIO) không nằm trong Git. Chỉ commit `.env.example`.
8. **Không sửa SPEC.** Nếu SPEC sai, thiếu, hoặc hai chỗ mâu thuẫn: dừng, ghi vào `docs/OPEN_QUESTIONS.md` (câu hỏi, các lựa chọn, đề xuất), và hỏi người phụ trách. Không tự quyết rồi làm tiếp.
9. **Trung thực về kết quả.** Chưa chạy thì nói "chưa chạy". Test đỏ thì báo đỏ. Không viết "đã kiểm chứng" khi chưa có output.
10. **Không tối ưu sớm / không thêm tính năng** ngoài SPEC (không thêm cache, không thêm API, không thêm thư viện lớn) trừ khi task yêu cầu.

## 3. Định nghĩa "Hoàn thành" (DoD) cho mọi task

- [ ] `dotnet build ShopFlow.sln -c Release` không lỗi; số cảnh báo không tăng so với trước task.
- [ ] Test của task (và toàn bộ test cũ) xanh.
- [ ] Architecture test xanh (từ T05).
- [ ] Mọi lệnh trong mục **Nghiệm thu** đã chạy, output dán trong báo cáo.
- [ ] Không có file bí mật/bin/obj trong commit.
- [ ] Checkbox task đã tick; `docs/PACKAGES.md` cập nhật nếu thêm gói.

## 4. Khi gặp mơ hồ

Thứ tự xử lý: (1) tìm trong SPEC đúng mục liên quan → (2) tìm trong Guide cũ nhưng nhớ Errata → (3) nếu vẫn mơ hồ, **hỏi**, không đoán. Một câu hỏi tốt gồm: tình huống, 2 lựa chọn, hệ quả của mỗi lựa chọn, đề xuất của bạn.

## 5. Mẫu báo cáo cuối task

```
Task: T__ — <tên>
Kết quả: HOÀN THÀNH / CHƯA HOÀN THÀNH (lý do)
File tạo/sửa: <danh sách>
Lệnh đã chạy + kết quả thật: <dán output rút gọn: build, test, lệnh nghiệm thu>
Quyết định kỹ thuật nhỏ trong phạm vi task: <nếu có>
Vấn đề mở (đã ghi vào OPEN_QUESTIONS.md): <nếu có>
Việc KHÔNG làm vì ngoài phạm vi: <nếu có>
```

## 6. Prompt mẫu để giao một task

```
Bạn là AI coder của dự án ShopFlow. Hãy đọc docs/spec/00_AI_RULES.md và tuân thủ tuyệt đối.
Làm đúng task T11 trong docs/spec/02_TASKS.md, chỉ đọc các mục SPEC mà task trỏ tới.
Chưa làm các task sau T11. Hoàn thành xong thì báo cáo theo mẫu ở mục 5 và dừng.
```
