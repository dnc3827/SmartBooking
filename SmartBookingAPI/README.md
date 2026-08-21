# 💈 SmartBooking API

SmartBooking API là một hệ thống backend RESTful được thiết kế để xử lý nghiệp vụ đặt lịch hẹn cắt tóc trực tuyến. Dự án không chỉ dừng lại ở các thao tác CRUD cơ bản mà còn giải quyết các bài toán thực tế như chống trùng lịch, tính toán khuyến mãi động và tích hợp thanh toán qua Webhook.

## 🛠 Tech Stack

| Thành phần | Công nghệ sử dụng |
| :--- | :--- |
| **Framework** | .NET 8.0 (ASP.NET Core Web API) |
| **Database & ORM** | SQL Server Express, Entity Framework Core 9.0 (Database-First) |
| **Authentication** | JWT (JSON Web Token), BCrypt.Net-Next |
| **Logging** | Serilog (Console & Rolling File) |
| **API Documentation** | Swagger UI (Swashbuckle) tích hợp XML Comments[cite: 2] |
| **Payment Gateway** | PayOS SDK (v2.1.0)[cite: 2] |

## Những vấn đề kỹ thuật đã giải quyết

- **Race condition**: Phát hiện overbooking khi 2 user 
  đặt cùng khung giờ → implement Allen Interval Overlap 
  + Database Transaction

- **PayOS SDK**: Tài liệu NuGet v2.1.0 ghi sai namespace
  (`PayOS.Models`) → tự tra source code tìm ra namespace 
  đúng (`PayOS.Models.V2.PaymentRequests`)

- **IDOR vulnerability**: Cancel endpoint lấy customerId 
  từ query string → fix bằng JWT claim

- **Captive Dependency**: Phát hiện và fix Singleton 
  inject Scoped service trong BackgroundService

## 🌟 Tính năng nổi bật & Điểm nhấn kỹ thuật (Selling Points)

### 1. Kiến trúc & Design Patterns
*   **Service Layer & DI:** Tách biệt hoàn toàn Controller (HTTP Logic) và Service (Business Logic)[cite: 2]. Sử dụng Dependency Injection để quản lý vòng đời (Scoped) cho các dịch vụ[cite: 2].
*   **Strategy Pattern:** Thiết kế hệ thống tính toán giảm giá (`IPromotionStrategy`) cho phép dễ dàng mở rộng các loại mã khuyến mãi (`Percentage`, `FixedAmount`) mà không vi phạm nguyên tắc Open/Closed[cite: 2].
*   **Single Responsibility Principle (SRP):** Tái cấu trúc các luồng nghiệp vụ phức tạp (như tạo đơn đặt lịch) thành các Helper methods đơn nhiệm dễ kiểm thử[cite: 2].

### 2. Xử lý nghiệp vụ (Business Logic)
*   **Thuật toán chống trùng lịch:** Áp dụng thuật toán Allen Interval Overlap để đảm bảo thợ cắt tóc không bị xếp trùng lịch trong cùng một khung giờ[cite: 2].
*   **Tính toán linh hoạt:** Tự động tính tổng thời gian cắt và tổng tiền dựa trên danh sách dịch vụ khách hàng chọn[cite: 2].
*   **Tích hợp thanh toán thực tế:** Khởi tạo link thanh toán qua PayOS và xây dựng hệ thống thu sóng Webhook để tự động cập nhật trạng thái đơn hàng khi khách chuyển khoản thành công[cite: 2].

### 3. Bảo mật & Tính ổn định
*   **Phòng chống IDOR:** Ngăn chặn việc người dùng thao tác trên dữ liệu của người khác bằng cách trích xuất trực tiếp `CustomerId` từ JWT Claims (`ClaimTypes.NameIdentifier`) thay vì nhận từ URL[cite: 2].
*   **Global Exception Handling:** Middleware tập trung bắt mọi ngoại lệ. Đặc biệt, hệ thống tự động ẩn `StackTrace` trên môi trường Production để ngăn ngừa rò rỉ thông tin (Information Disclosure)[cite: 2].
*   **Database Transactions:** Đảm bảo tính toàn vẹn dữ liệu (ACID) khi tạo lịch hẹn kết hợp gọi API bên thứ 3 (Rollback nếu xảy ra lỗi)[cite: 2].

## 🗄 Cấu trúc Database (Core Entities)

*   `Customer`: Quản lý thông tin khách hàng.
*   `Barber`: Quản lý danh sách thợ cắt tóc.
*   `Service`: Quản lý danh mục dịch vụ và giá cả.
*   `Appointment`: Entity trung tâm lưu trữ thông tin đặt lịch, trạng thái thanh toán và khóa ngoại liên kết với Customer/Barber[cite: 2]. Quan hệ N:N với `Service` được EF Core tự động quản lý qua bảng `Appointment_Service`[cite: 2].
*   `Promotion`: Quản lý các chiến dịch giảm giá, giới hạn số lượt dùng và điều kiện áp dụng[cite: 2].

## 🚀 Hướng dẫn cài đặt & Chạy dự án

### Yêu cầu hệ thống
*   .NET 8 SDK
*   SQL Server (hoặc SQL Server Express)

### Các bước thiết lập
1. **Clone repository:**
   ```bash
   git clone <your-repo-url>
   cd SmartBookingAPI
