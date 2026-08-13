# PROJECT_CONTEXT.md — SmartBooking API

> **Tài liệu nhắc việc dành cho AI.** 
> Cập nhật lần cuối: 2026-08-11
> **Chú ý:** Không copy/paste code chi tiết — chỉ mô tả nghiệp vụ và kiến trúc.

---

## 1. Tech Stack

| Lớp | Công nghệ |
|---|---|
| **Runtime** | .NET 8.0 (ASP.NET Core Web API) |
| **ORM** | Entity Framework Core 9.0 (Database-First scaffold) |
| **Database** | SQL Server Express — `SmartBookingDB` trên `DUY38\SQLEXPRESS` |
| **Auth** | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` v8) |
| **Logging** | Serilog — ghi ra Console + file `logs/log-{date}.txt` (rolling daily) |
| **API Docs** | Swashbuckle / Swagger UI (chỉ bật ở Development) + XML Comments |
| **JWT Libs** | `System.IdentityModel.Tokens.Jwt` + `Microsoft.IdentityModel.Tokens` v8 |

---

## 2. Models & Relationships

### Entities cốt lõi

| Entity | Bảng DB | Trường đáng chú ý |
|---|---|---|
| `Customer` | `Customer` | `CustomerId`, `CustomerName`, `Phone`, `Email?` |
| `Barber` | `Barber` | `BarberId`, `BarberName`, `Rating` (default 5.0) |
| `Service` | `Service` | `ServiceId`, `ServiceName`, `Price` (decimal), `DurationMinutes` (default 30) |
| `Appointment` | `Appointment` | `AppointmentId`, `CustomerId`, `BarberId`, `AppointmentDate` (DateOnly), `StartTime`/`EndTime` (TimeOnly), `Status` (default "Pending"), `CreatedAt` |
| `Promotion` | `Promotions` | *(Xem chi tiết bên dưới)* |

### Quan hệ

*   **`Customer` ←1:N→ `Appointment`** (FK: `CustomerID`, `ClientSetNull`)
*   **`Barber` ←1:N→ `Appointment`** (FK: `BarberID`, `ClientSetNull`)
*   **`Appointment` ←N:N→ `Service`** qua bảng trung gian `Appointment_Service` (`AppointmentID`, `ServiceID`) — được EF quản lý tự động, **không có Entity riêng**.
*   **`Promotion`** — Entity độc lập, **chưa có FK liên kết với `Appointment`**.

### Promotion Fields

*   `Code`, `DiscountType` ("Percentage" | "FixedAmount"), `DiscountValue`, `MaxDiscountAmount`
*   `StartDate`, `EndDate`, `MaxUsage`, `UsedCount`, `MinOrderValue`, `IsActive`

---

## 3. Business Logic đã hoàn thành

### AppointmentService (`IAppointmentService`)

#### `CreateAppointmentAsync(customerId, CreateAppointmentDto)` — [Refactored theo SRP]
1. Bắt đầu **Transaction** (DB).
2. Tách `DateTime StartTime` từ DTO thành `DateOnly` và `TimeOnly`.
3. Gọi helper `GetSelectedServicesAndDurationAsync`: lấy thông tin danh sách dịch vụ và tính tổng thời gian cắt.
4. Gọi helper `EnsureNoScheduleConflictAsync`: kiểm tra trùng lịch của Barber (Allen Interval Overlap).
5. Gọi helper `CalculateFinalAmountAsync`: thẩm định mã giảm giá qua `IPromotionService` và tính số tiền thanh toán cuối cùng (`finalAmount`).
6. Tạo đối tượng `Appointment` với trạng thái `"Pending"` và lưu DB ngay lập tức để EF Core sinh ra `AppointmentId`.
7. Nếu có `PromoCode`, gọi `ApplyPromotionAsync` để tăng số lượt dùng mã.
8. Gọi `IPaymentGateway.CreatePaymentLink`: dùng `AppointmentId` vừa lấy được làm `orderCode` để tạo URL thanh toán qua cổng PayOS thật.
9. `Commit` Transaction và trả về chuỗi `paymentUrl` (đường link thanh toán) cho Controller/Frontend.
10. Nếu lỗi bất kỳ: `Rollback` toàn bộ.

#### `CancelAppointmentAsync(customerId, appointmentId)`
1. Tìm appointment theo `appointmentId`; ném `AppException(404)` nếu không có.
2. Kiểm tra **IDOR**: Nếu `appointment.CustomerId != customerId` → từ chối.
3. Guard: không hủy nếu `Status == "Cancelled"` hoặc `"Completed"`.
4. **Chính sách hoàn tiền 24h**: nếu còn ≥ 24h đến giờ cắt → gọi `RefundCustomerAsync(100,000)`; ngược lại không hoàn.
5. Soft delete: set `Status = "Cancelled"`, `SaveChanges`.

#### `GetMyAppointmentsAsync(customerId)`
*   Lấy tất cả `Appointment` theo `customerId` từ DB. 
*   Đã sử dụng `.Include()` để join thông tin chi tiết của `Barber` và danh sách `Services`. 
*   *(Đã xử lý cấu hình `IgnoreCycles` bên trong JSON serializer để tránh vòng lặp dữ liệu vô tận).*

#### `UpdatePaymentStatusAsync(appointmentId, isSuccess)`
*   Tìm `Appointment` theo `appointmentId` (`OrderCode`). 
*   Nếu `isSuccess = true` và `Status == "Pending"`, cập nhật trạng thái thành `"Paid"`. Nếu thất bại thì chuyển thành `"Failed"`.

### PromotionService (`IPromotionService`)

#### `CalculateDiscountAsync(code, originalPrice)`
*Pure calculation, không thay đổi DB.*
1. Tìm promotion active theo `code`.
2. Validate tuần tự: tồn tại → chưa hết hạn → còn lượt → đủ `MinOrderValue`.
3. Tính `discountAmount`: Giao việc tính toán cho các Strategy tương ứng (`Percentage` hoặc `FixedAmount`).
4. Trả về `discountAmount` (không vượt `originalPrice`).

#### `ApplyPromotionAsync(code, originalPrice)`
1. Gọi lại `CalculateDiscountAsync` để validate (nếu lỗi thì exception chặn).
2. Tăng `UsedCount += 1`, `SaveChanges`.

### Security & Refactoring (Bảo mật & Tái cấu trúc)
*   **[Refactor Kiến trúc] Tuân thủ SRP (SOLID)**: Đã giải phẫu và tách nhỏ hàm `CreateAppointmentAsync` trong `AppointmentService`. Rút trích các logic kiểm tra dịch vụ, kiểm tra trùng lịch, tính toán giảm giá thành các private helper methods độc lập. Đồng thời đảo ngược luồng để lưu DB lấy `AppointmentId` trước khi gọi PayOS.
*   **[Refactor Đồng bộ DTO & Model]**: Xử lý triệt để xung đột kiểu dữ liệu giữa `CreateAppointmentDto` (`DateTime StartTime`, `ServiceId` dạng list) và Entity Models (tách thành `DateOnly`, `TimeOnly`).
*   **[Refactor Kiến trúc] Strategy Pattern**: Đã gỡ bỏ hoàn toàn logic `if/else` thủ công trong `PromotionService`. Đã xây dựng `IPromotionStrategy`, `PercentageDiscountStrategy`, và `FixedAmountDiscountStrategy`, đồng thời inject tự động dưới dạng tập hợp `IEnumerable<IPromotionStrategy>` qua DI container trong `Program.cs`.
*   **[Fix Bảo mật] Ẩn StackTrace trên Production**: Đã cập nhật `GlobalExceptionMiddleware` tiêm `IHostEnvironment` để chỉ xuất `StackTrace` khi ở Development, loại bỏ hoàn toàn rủi ro lộ lọt cấu trúc hệ thống (Information Disclosure) trên Production.
*   **[Fix Bảo mật] JWT Secret Key**: Đã loại bỏ hoàn toàn key hardcode trong `Program.cs` và `AuthController.cs`. Hệ thống hiện đã đọc key an toàn thông qua `IConfiguration` từ file `appsettings.json` (`JwtConfig:SecretKey`).
*   **[Fix Bảo mật] Endpoint Hủy lịch hẹn (`AppointmentController`)**: Đã bọc khiên `[Authorize]`. Ngăn chặn lỗ hổng IDOR bằng cách loại bỏ tham số `customerId` lộ thiên, tự động bóc tách ID chính chủ từ JWT Claim (`ClaimTypes.NameIdentifier`).
*   **[Refactor Code] Tách file DTO**: Di chuyển class `LoginRequest` dùng chung ra khỏi file Controller, đặt vào một file class độc lập ngăn nắp trong thư mục `DTOs`.
*   **[Refactor Nghiệp vụ] Tính tiền thực tế (`AppointmentService`)**: Đã vá lỗ hổng hardcode 100,000 VNĐ. Áp dụng LINQ (`selectedServices.Sum(s => s.Price)`) để tính tổng tiền thanh toán lúc tạo đơn và số tiền hoàn lại lúc hủy đơn (sử dụng `.Include(a => a.Services)` để load dữ liệu).
*   **[Refactor Cấu hình]**: Đã xóa sổ các file mẫu mặc định của .NET (WeatherForecast). Di dời an toàn chuỗi kết nối Database từ DbContext ra file cấu hình `appsettings.json` (`ConnectionStrings:DefaultConnection`) và tiêm qua `Program.cs`.
*   **[Feature Auth] Đăng ký & Đăng nhập**: Đã khai tử cơ chế Auth giả lập. Hệ thống hiện đã có luồng tạo tài khoản mới qua POST `/api/auth/register`, mật khẩu được mã hóa an toàn bằng BCrypt.Net-Next. Endpoint Login đã truy vấn DB thật và trả về JWT Token chứa CustomerId chính xác.
*   **[Feature Payment & Webhook] Tích hợp cổng thanh toán PayOS**: Đã tích hợp luồng thanh toán PayOS thật và xây dựng thành công trạm thu sóng Webhook (`POST /api/payment/webhook`), kết hợp `PayOSWebhookDto` để lắng nghe kết quả chuyển khoản và tự động cập nhật trạng thái đơn đặt lịch trong DB.

---

## 4. API Endpoints hiện có

| Method | Route | Auth | Mô tả |
|---|---|---|---|
| `POST` | `/api/auth/register` | ❌ Public | Đăng ký tài khoản Customer mới, mã hóa mật khẩu bằng BCrypt |
| `POST` | `/api/auth/login` | ❌ Public | Đăng nhập tài khoản thật, truy vấn DB, trả JWT Token hợp lệ |
| `GET` | `/api/customer` | ❌ Public | Lấy toàn bộ danh sách Customer (trực tiếp từ DbContext, không qua Service) |
| `GET / POST / PUT / DELETE` | `/api/barber` | Hỗn hợp | CRUD quản lý thợ cắt tóc. GET là Public, thay đổi dữ liệu yêu cầu ✅ Bearer |
| `GET / POST / PUT / DELETE` | `/api/service` | Hỗn hợp | CRUD quản lý dịch vụ. GET là Public, thay đổi dữ liệu yêu cầu ✅ Bearer |
| `POST` | `/api/appointment` | ✅ Bearer | Đặt lịch mới — trả về link thanh toán PayOS |
| `GET` | `/api/appointment/my-history` | ✅ Bearer | Xem lịch sử lịch hẹn của chính mình |
| `DELETE` | `/api/appointment/cancel/{appointmentId}` | ✅ Bearer | Hủy lịch — đã bọc `[Authorize]`, chống IDOR bằng claim từ Token |
| `GET` | `/api/appointment/test-loi` | ❌ Public | Endpoint test — cố ý throw Exception để test Middleware + Serilog |
| `GET` | `/api/promotion/check-discount` | ❌ Public | Tính số tiền giảm (không trừ lượt dùng) |
| `POST` | `/api/promotion/apply-discount` | ❌ Public | Áp dụng mã (trừ 1 lượt dùng) |
| `POST` | `/api/payment/webhook` | ❌ Public | Lắng nghe callback từ PayOS, tự động cập nhật trạng thái Appointment |

---

## 5. Mock / TODO / Chưa hoàn thiện

### ✅ Đã giải quyết (Dọn khỏi TODO)
*   **Tuân thủ SRP (SOLID)**: Tách nhỏ hàm `CreateAppointmentAsync` thành các helper methods, đảo ngược luồng để lấy `AppointmentId` làm `orderCode` cho PayOS.
*   **Đồng bộ kiểu dữ liệu** giữa DTO và Entity Models (xử lý sự kiện `ServiceId` và `StartTime`).
*   **Áp dụng Strategy Pattern**: Gỡ bỏ các câu lệnh `if/else` khi tính toán giảm giá (`Percentage`, `FixedAmount`) trong `PromotionService`.
*   **Bảo mật**: Lỗ hổng rò rỉ thông tin `StackTrace` ở mọi môi trường qua `ErrorResponse.Detail` đã được khắc phục triệt để bằng cách inject `IHostEnvironment`.
*   **Tối ưu truy vấn**: Data trả về của `GetMyAppointmentsAsync` đã được nạp đủ bảng `Barber` và `Services` bằng lệnh `.Include()`.
*   **Thanh toán**: `FakePaymentGateway` đã được thay thế bằng cổng PayOS thật. Hardcode số tiền 100k đã được tính toán bằng tổng giá tiền thật từ DB. Webhook PayOS xử lý tự động cập nhật trạng thái lịch hẹn.
*   **Authorization**: Endpoint hủy lịch thiếu `[Authorize]` đã được bọc lại an toàn. Hoàn thiện API CRUD cho Entity `Barber` và `Service`, luồng Master Data hoạt động trơn tru.
*   **Authentication**: JWT SecretKey hardcode đã được dời vào cấu hình `appsettings.json`. Endpoint Register/Signup đã được implement.
*   **Database & Docs**: Khởi tạo bản EF Core Migration đầu tiên (`InitialCreate`) và đồng bộ Baseline với DB có sẵn. Cấu hình Swagger UI đọc file XML Comments để hiển thị tài liệu API chuyên nghiệp.

### 📋 Chưa implement
*(Tạm thời chưa có task tính năng mới nào tồn đọng)*

### 🌟 Giai đoạn 3: Refactoring & Tối ưu hóa (Chuẩn bị cho CV)
*   **Chuẩn bị tài liệu dự án chuyên nghiệp:** Tạo và viết file `README.md` (sơ đồ kiến trúc, hướng dẫn chạy Migration, kịch bản Chaos Test/Load Test).

---

## 6. Quyết định kỹ thuật đã chốt

### Patterns áp dụng
*   **Interface + DI (Dependency Injection)**: Mọi service đều có interface (`IAppointmentService`, `IPromotionService`, `IPaymentGateway`, `IBarberService`, `IServiceService`) → đăng ký `Scoped` trong `Program.cs`.
*   **Service Layer Pattern**: Controller chỉ xử lý HTTP (extract claim, map response), toàn bộ business logic nằm trong Service.
*   **Strategy Pattern**: Xử lý logic tính toán khuyến mãi linh hoạt, dễ mở rộng thêm các loại giảm giá mới mà không cần sửa code cũ.
*   **Helper Methods (SRP)**: Phân rã hàm phức tạp thành các hàm con xử lý đơn nhiệm.
*   **Soft Delete**: `Appointment` hủy bằng cách set `Status = "Cancelled"`, không xóa khỏi DB.
*   **Custom Exception & Global Middleware**: Dùng `AppException(message, statusCode)` để phân biệt lỗi nghiệp vụ vs lỗi hệ thống. Middleware catch mọi exception để trả HTTP status code phù hợp.
*   **DTO Validation**: Dùng các Validation Attributes (`[Required]`, `[Range]`) ở các DTO.
*   **Baseline Migration**: Bỏ trống hàm `Up()` trong bản `InitialCreate` để đồng bộ EF Core Snapshot với SQL Database đã tạo thủ công.
*   **Swagger XML Comments**: Bật `<GenerateDocumentationFile>` và bỏ qua cảnh báo 1591 (`<NoWarn>`).

### Thư viện / Package đã chọn

| Package | Lý do |
|---|---|
| **Serilog** | Logging có structured log, rolling file, dễ mở rộng sink (Seq, Elasticsearch...) |
| **EF Core 9** | ORM chính; scaffold từ DB có sẵn (`Database-First`) |
| **JwtBearer v8** | Match với .NET 8 target framework |
| **Swashbuckle** | Swagger UI tích hợp sẵn, đã cấu hình Bearer token input |

### Đã bỏ / Không dùng
*   **Repository Pattern**: `DbContext` được inject thẳng vào Service.
*   **AutoMapper / Mapster**: Map thủ công.
*   **FluentValidation**: Dùng DataAnnotations + `IValidatableObject` thay thế.
*   **ASP.NET Identity**: Tự implement Auth với BCrypt.Net-Next + JWT thủ công.
*   **CQRS / MediatR**: Chưa áp dụng.

> **Ghi chú của tác giả (AI không thể suy ra từ code)**
> *   Dự án này dùng để học/demo kỹ thuật testing nâng cao (load test, chaos, malicious user).
> *   Ưu tiên fix bảo mật TRƯỚC khi thêm feature mới.
> *   Sau khi có Auth thật: CustomerId phải luôn lấy từ JWT claim, không bao giờ từ query string.
> *   Cổng thanh toán chính thức: PayOS.
> *   **Lưu ý PayOS v2.1.0:** Namespace đúng là `PayOS.Models.V2.PaymentRequests` — tài liệu NuGet ghi sai `PayOS.Models`. Luôn dùng IntelliSense để verify với SDK ít phổ biến.

---

## 7. Cấu trúc thư mục nhanh

```text
SmartBookingAPI/
├── Controllers/
│   ├── AppointmentController.cs   # CRUD lịch hẹn
│   ├── AuthController.cs          # Login / Register → JWT
│   ├── BarberController.cs        # CRUD cho Barber (Yêu cầu Token cho ghi/xóa)
│   ├── CheckoutController.cs      # Xử lý thanh toán & Webhook PayOS
│   ├── CustomerController.cs      # Lấy danh sách customer
│   ├── PromotionController.cs     # Check + Apply mã giảm giá
│   └── ServiceController.cs       # CRUD cho Services (Yêu cầu Token cho ghi/xóa)
│   
├── DTOs/
│   ├── BarberDtos.cs              # DTOs cho Create/Update Barber
│   ├── CreateAppointmentDto.cs    # Input đặt lịch (BarberId, ServiceId[], StartTime)
│   ├── LoginRequest.cs            # Input đăng nhập
│   ├── PayOSWebhookDto.cs         # Envelope dữ liệu Webhook từ PayOS
│   ├── RegisterDto.cs             # Input đăng ký
│   └── ServiceDtos.cs             # DTOs cho Create/Update Service
├── logs/                          # Thư mục sinh file log tự động của Serilog
├── Middlewares/
│   └── GlobalExceptionMiddleware.cs
├── Migrations/                    # Đã sinh bản InitialCreate đồng bộ Snapshot
├── Models/
│   ├── AppException.cs            # Custom exception với StatusCode
│   ├── Appointment.cs
│   ├── Barber.cs
│   ├── Customer.cs
│   ├── ErrorResponse.cs           # JSON error envelope
│   ├── Promotion.cs
│   ├── Service.cs
│   └── SmartBookingDbContext.cs   # EF DbContext + Fluent API config
├── Services/
│   ├── AppointmentService.cs      # Logic xử lý đặt lịch & cập nhật trạng thái
│   ├── BarberService.cs           # Logic CRUD thợ cắt tóc
│   ├── IAppointmentService.cs
│   ├── IBarberService.cs          
│   ├── IPaymentGateway.cs
│   ├── IPromotionService.cs
│   ├── IServiceService.cs         
│   ├── PaymentService.cs          # Giao tiếp cổng thanh toán (PayOS)
│   ├── PromotionService.cs        # Logic xử lý khuyến mãi
│   ├── ServiceService.cs          # Logic CRUD dịch vụ
│   └── Strategies/                # Các chiến lược tính toán giảm giá (Strategy Pattern)
│       ├── FixedAmountDiscountStrategy.cs
│       ├── IPromotionStrategy.cs
│       └── PercentageDiscountStrategy.cs
├── appsettings.json               # JwtConfig, ConnectionStrings, PayOS Keys
├── SmartBookingAPI.xml            # File sinh tự động chứa XML Comments cho Swagger
└── Program.cs                     # DI, JWT, Serilog, Middleware pipeline, cấu hình Swagger XML

## 8. Luyện tập Git Commit
1. commit 1
2. commit 02


