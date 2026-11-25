using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RedLockNet;
using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
using SmartBookingAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartBooking.Tests
{
    public class AppointmentServiceTests
    {
        [Fact]
        public async Task CreateAppointmentAsync_ValidRequest_ReturnsPaymentUrl()
        {

            // --- 1. ARRANGE ---
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>() // Thay AppDbContext bằng tên DbContext của bạn
            .UseInMemoryDatabase(databaseName: "SmartBookingTestDb")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)) // <--- Bổ sung dòng này
            .Options;
            using var context = new SmartBookingDbContext(options);

                var customer = new Customer { CustomerId = 1, CustomerName = "Khách Hàng Test", Phone = "0987654321" };
                var barber = new Barber { BarberId = 1, BarberName = "Thợ Test" };
                var service = new Service { ServiceId = 1, ServiceName = "Cắt tóc nam", Price = 100000, DurationMinutes = 30 };

                context.Customers.Add(customer);
                context.Barbers.Add(barber);
                context.Services.Add(service);
                await context.SaveChangesAsync();

                // Tạo các Mock objects
                var mockPaymentGateway = new Mock<IPaymentGateway>() { DefaultValue = DefaultValue.Mock };
                var mockPromotionService = new Mock<IPromotionService>() { DefaultValue = DefaultValue.Mock };
            // Thêm diễn viên đóng thế cho hệ thống Khóa phân tán (Redis)
                var mockLockFactory = new Mock<IDistributedLockFactory>();

                // 1. Tạo một Kẻ đóng thế cho chính cái ổ khóa (thường là IRedLock hoặc IDistributedLock tùy thư viện bạn dùng)
                var mockRedLock = new Mock<IRedLock>();
                // BẮT BUỘC: Ép ổ khóa này luôn báo trạng thái "Đã khóa thành công", nếu không code sẽ văng AppException ở dòng 15
                mockRedLock.Setup(l => l.IsAcquired).Returns(true);

                // 2. Dạy LockFactory trả về cái ổ khóa giả này khi được gọi
                mockLockFactory.Setup(f => f.CreateLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )).ReturnsAsync(mockRedLock.Object);

            // Setup PayOS Mock với 3 tham số (int, int, string)
            mockPaymentGateway
                    .Setup(p => p.CreatePaymentLink(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                    .ReturnsAsync("https://payos.vn/fake-link-test");

                // Khởi tạo Service với đầy đủ 4 tham số
                var appointmentService = new AppointmentService(
                    context,
                    mockPaymentGateway.Object,
                    mockPromotionService.Object,
                    mockLockFactory.Object); // Bổ sung mockLockFactory vào đây

                // Cập nhật DTO cho chuẩn xác với định nghĩa của bạn
                var requestDto = new CreateAppointmentDto
                {
                    BarberId = 1,
                    ServiceId = new List<int> { 1 }, // Sửa ServiceIds thành ServiceId
                    StartTime = new DateTime(2026, 1, 1, 9, 0, 0) // Bỏ AppointmentDate, chỉ dùng StartTime
                };

                // --- 2. ACT ---
                string paymentUrl = await appointmentService.CreateAppointmentAsync(1, requestDto);

                // --- 3. ASSERT ---
                paymentUrl.Should().Be("https://payos.vn/fake-link-test");

                // Verify cũng cần khớp 3 tham số
                mockPaymentGateway.Verify(p => p.CreatePaymentLink(It.IsAny<int>(), 100000, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateAppointmentAsync_LockFails_ThrowsAppException()
        {
            // --- 1. ARRANGE (Dựng bối cảnh) ---
            // Vì lỗi văng ra ngay từ đầu hàm (dòng 15), chúng ta thậm chí không cần tạo dữ liệu giả cho Database!
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Tạo 1 DB trống hoàn toàn mới
                .Options;
            using var context = new SmartBookingDbContext(options);

            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();
            var mockLockFactory = new Mock<IDistributedLockFactory>();

            var mockRedLock = new Mock<IRedLock>();

            // ĐIỂM CHẾT TRỌNG TÂM: Cố tình dàn dựng cho ổ khóa báo bận!
            mockRedLock.Setup(l => l.IsAcquired).Returns(false);

            mockLockFactory.Setup(f => f.CreateLockAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()
            )).ReturnsAsync(mockRedLock.Object);

            var appointmentService = new AppointmentService(
                context, mockPaymentGateway.Object, mockPromotionService.Object, mockLockFactory.Object);

            var requestDto = new CreateAppointmentDto
            {
                BarberId = 1,
                StartTime = new DateTime(2026, 1, 1, 9, 0, 0)
            };

            // --- 2. ACT (Bấm máy) ---
            // MẸO: Khi test lỗi văng ra (Exception), ta không thể gán vào biến `string paymentUrl` được nữa.
            // Ta phải gom toàn bộ hành động gọi hàm vào một biến kiểu "Func" (tưởng tượng như gói nó vào 1 cái hộp chưa mở).
            Func<Task> act = async () => await appointmentService.CreateAppointmentAsync(1, requestDto);

            // --- 3. ASSERT (Nghiệm thu) ---
            // 3.1. Ép mở hộp và kỳ vọng nó ném ra đúng quả bom AppException, chứa chữ "thử lại sau"
            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*thử lại sau*");

            // 3.2. Check camera an ninh: Đảm bảo kẻ đóng thế PayOS không bao giờ nhận được lệnh tạo link
            mockPaymentGateway.Verify(p => p.CreatePaymentLink(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateAppointmentAsync_ScheduleConflict_ThrowsAppException()
        {
            // --- 1. ARRANGE ---
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)) // <--- BỔ SUNG DÒNG NÀY
            .Options;
            using var context = new SmartBookingDbContext(options);

            // 1.1. Tạo dữ liệu nền cơ bản
            var customer = new Customer { CustomerId = 1, CustomerName = "Khách Hàng Mới", Phone = "0909090909" };
            var barber = new Barber { BarberId = 1, BarberName = "Thợ Hot" };
            var service = new Service { ServiceId = 1, ServiceName = "Cắt tóc", Price = 100000, DurationMinutes = 30 };

            // 1.2. ĐIỂM CHẾT TRỌNG TÂM: Nhét sẵn một lịch hẹn cũ vào DB để cố tình gây trùng giờ
            var existingAppointment = new Appointment
            {
                AppointmentId = 999,
                CustomerId = 2, // Khách khác
                BarberId = 1,   // Cùng thợ
                AppointmentDate = new DateOnly(2026, 1, 1),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 30),
                Status = "Confirmed"
            };

            context.Customers.Add(customer);
            context.Barbers.Add(barber);
            context.Services.Add(service);
            context.Appointments.Add(existingAppointment);
            await context.SaveChangesAsync();

            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();

            // 1.3. Cho qua cửa ngõ Redis Lock bình thường
            var mockLockFactory = new Mock<IDistributedLockFactory>();
            var mockRedLock = new Mock<IRedLock>();
            mockRedLock.Setup(l => l.IsAcquired).Returns(true); // <--- Ổ khóa mở thành công
            mockLockFactory.Setup(f => f.CreateLockAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()
            )).ReturnsAsync(mockRedLock.Object);

            var appointmentService = new AppointmentService(
                context, mockPaymentGateway.Object, mockPromotionService.Object, mockLockFactory.Object);

            // 1.4. Yêu cầu tạo lịch CÙNG NGÀY, CÙNG GIỜ (9:00), CÙNG THỢ với lịch hẹn cũ
            var requestDto = new CreateAppointmentDto
            {
                BarberId = 1,
                ServiceId = new List<int> { 1 },
                StartTime = new DateTime(2026, 1, 1, 9, 0, 0)
            };

            // --- 2. ACT ---
            Func<Task> act = async () => await appointmentService.CreateAppointmentAsync(1, requestDto);

            // --- 3. ASSERT ---
            // 3.1. Đảm bảo hàm EnsureNoScheduleConflictAsync văng ra lỗi AppException
            await act.Should().ThrowAsync<AppException>();

            // 3.2. Đảm bảo PayOS tuyệt đối không được gọi đến
            mockPaymentGateway.Verify(p => p.CreatePaymentLink(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CancelAppointmentAsync_IdorAttempt_ThrowsAppException()
        {
            // --- 1. ARRANGE (Dàn trận) ---
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDB_IDOR_" + Guid.NewGuid().ToString())
                .Options;
            using var context = new SmartBookingDbContext(options);

            // 1.1 Tạo một lịch hẹn hợp lệ thuộc về Khách hàng số 1 (Nạn nhân)
            var victimAppointment = new Appointment
            {
                AppointmentId = 555,
                CustomerId = 1, // Chủ nhân thực sự của lịch hẹn
                BarberId = 1,
                AppointmentDate = new DateOnly(2026, 12, 31),
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(10, 30),
                Status = "Pending"
            };
            context.Appointments.Add(victimAppointment);
            await context.SaveChangesAsync();

            // 1.2 Khởi tạo Service với các Mock cơ bản
            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();
            var mockLockFactory = new Mock<IDistributedLockFactory>();

            var appointmentService = new AppointmentService(
                context, mockPaymentGateway.Object, mockPromotionService.Object, mockLockFactory.Object);

            // --- 2. ACT (Khai hỏa) ---
            int hackerCustomerId = 2; // Kẻ tấn công có ID là 2
            int targetAppointmentId = 555; // Nhưng lại đòi hủy đơn số 555 của người khác

            Func<Task> act = async () => await appointmentService.CancelAppointmentAsync(hackerCustomerId, targetAppointmentId);

            // --- 3. ASSERT (Nghiệm thu) ---
            // Đảm bảo hệ thống phát hiện ra IDOR và ném lỗi từ chối quyền
            await act.Should().ThrowAsync<AppException>();

            // Bồi thêm 1 lớp bảo vệ: Đảm bảo dữ liệu trong DB không bị thay đổi (Status vẫn là Pending)
            var dbAppointment = await context.Appointments.FindAsync(targetAppointmentId);
            dbAppointment.Status.Should().Be("Pending");
        }

        [Fact]
        public async Task CancelAppointmentAsync_CancelBefore24Hours_UpdatesStatusAndCallsRefund()
        {
            // --- 1. ARRANGE ---
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDB_Refund24h_" + Guid.NewGuid().ToString())
                .Options;
            using var context = new SmartBookingDbContext(options);

            // Tính toán thời gian động: Luôn cách hiện tại 48 tiếng (chắc chắn >= 24h)
            var futureDateTime = DateTime.Now.AddDays(2);
            decimal expectedRefundAmount = 150000;

            var appointment = new Appointment
            {
                AppointmentId = 1,
                CustomerId = 1, // Chủ nhân hợp lệ
                BarberId = 1,
                AppointmentDate = DateOnly.FromDateTime(futureDateTime),
                StartTime = TimeOnly.FromDateTime(futureDateTime),
                Status = "Pending",
                Services = new List<Service>
                {
                    new Service { ServiceId = 1, ServiceName = "Cắt tóc VIP",Price = expectedRefundAmount }
                }
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            // Khởi tạo các Mock (Dựa trên cấu trúc dự án của bạn)
            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();
            var mockLockFactory = new Mock<IDistributedLockFactory>();

            var service = new AppointmentService(
                context, mockPaymentGateway.Object, mockPromotionService.Object, mockLockFactory.Object);

            // --- 2. ACT ---
            var result = await service.CancelAppointmentAsync(customerId: 1, appointmentId: 1);

            // --- 3. ASSERT ---
            // Kiểm tra kết quả trả về
            result.Should().Contain(expectedRefundAmount.ToString("N0"));

            // Kiểm tra hiện trường DB (Status)
            var dbAppointment = await context.Appointments.FindAsync(1);
            dbAppointment.Status.Should().Be("Cancelled");

            // NGHIỆM THU QUAN TRỌNG NHẤT: Đảm bảo cổng thanh toán ĐƯỢC GỌI đúng 1 lần
            mockPaymentGateway.Verify(p => p.RefundCustomerAsync(1, expectedRefundAmount), Times.Once);
        }

        [Fact]
        public async Task CancelAppointmentAsync_CancelUnder24Hours_UpdatesStatusAndDoesNotRefund()
        {
            // --- 1. ARRANGE ---
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDB_NoRefund_" + Guid.NewGuid().ToString())
                .Options;
            using var context = new SmartBookingDbContext(options);

            // Thời gian động: Khách báo hủy trước giờ cắt có 2 tiếng (< 24h)
            var closeDateTime = DateTime.Now.AddHours(2);
            decimal servicePrice = 150000;

            var appointment = new Appointment
            {
                AppointmentId = 1,
                CustomerId = 1, // Đúng chủ nhân
                BarberId = 1,
                AppointmentDate = DateOnly.FromDateTime(closeDateTime),
                StartTime = TimeOnly.FromDateTime(closeDateTime),
                Status = "Pending",
                Services = new List<Service>
                {
                    new Service
                    {
                        ServiceId = 1,
                        ServiceName = "Cắt tóc tiêu chuẩn", // Đã vá lỗi EF Core
                        Price = servicePrice
                    }
                }
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            // Khởi tạo các Mock
            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();
            var mockLockFactory = new Mock<IDistributedLockFactory>();

            var service = new AppointmentService(
                context, mockPaymentGateway.Object, mockPromotionService.Object, mockLockFactory.Object);

            // --- 2. ACT ---
            var result = await service.CancelAppointmentAsync(customerId: 1, appointmentId: 1);

            // --- 3. ASSERT ---
            // Kiểm tra hiện trường DB (Vẫn cho phép hủy)
            var dbAppointment = await context.Appointments.FindAsync(1);
            dbAppointment.Status.Should().Be("Cancelled");

            // Kiểm tra câu thông báo trả về (Phải từ chối hoàn tiền)
            result.Should().Contain("không thể hoàn lại");
            result.Should().Contain(servicePrice.ToString("N0"));

            // CHỐT CHẶN QUAN TRỌNG: Không có đồng nào bị chuyển đi
            mockPaymentGateway.Verify(p => p.RefundCustomerAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task CancelAppointmentAsync_WhenAppointmentIsInThePast_ThrowsAppExceptionAndDoesNotRefund()
        {
            // 1. ARRANGE
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new SmartBookingDbContext(options);
            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();
            var mockLockFactory = new Mock<IDistributedLockFactory>();

            var service = new AppointmentService(
                context,
                mockPaymentGateway.Object,
                mockPromotionService.Object,
                mockLockFactory.Object);

            // Tạo lịch hẹn nằm ở thì quá khứ (Cách đây 2 tiếng)
            var pastDateTime = DateTime.Now.AddHours(-2);

            var appointment = new Appointment
            {
                AppointmentId = 1,
                CustomerId = 1,
                Status = "Pending",
                AppointmentDate = DateOnly.FromDateTime(pastDateTime),
                StartTime = TimeOnly.FromDateTime(pastDateTime),
                Services = new List<Service> { new Service { ServiceName = "Cắt tóc nam", Price = 500000 } }
            };

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            // 2. ACT
            Func<Task> act = async () => await service.CancelAppointmentAsync(customerId: 1, appointmentId: 1);

            // 3. ASSERT
            // Kỳ vọng hàm văng ra lỗi AppException có chứa chữ "quá khứ"
            await act.Should().ThrowAsync<AppException>()
                .WithMessage("*quá khứ*");

            // Kỳ vọng KHÔNG CÓ bất kỳ lệnh hoàn tiền nào được gọi
            mockPaymentGateway.Verify(
                x => x.RefundCustomerAsync(It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);

            // Đảm bảo trạng thái lịch trong DB không bị đổi thành Cancelled
            var dbAppointment = await context.Appointments.FindAsync(1);
            dbAppointment.Status.Should().Be("Pending");
        }

        [Fact]
        public async Task CancelAppointmentAsync_WhenAppointmentDoesNotExist_Throws404AppExceptionAndDoesNotRefund()
        {
            // 1. ARRANGE
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new SmartBookingDbContext(options);
            var mockPaymentGateway = new Mock<IPaymentGateway>();
            var mockPromotionService = new Mock<IPromotionService>();
            var mockLockFactory = new Mock<IDistributedLockFactory>();

            var service = new AppointmentService(
                context,
                mockPaymentGateway.Object,
                mockPromotionService.Object,
                mockLockFactory.Object);

            // DB InMemory để trống, không thêm bản ghi nào có AppointmentId = 999

            // 2. ACT
            Func<Task> act = async () => await service.CancelAppointmentAsync(customerId: 1, appointmentId: 999);

            // 3. ASSERT
            var exception = await act.Should().ThrowAsync<AppException>()
                .WithMessage("*Không tìm thấy lịch hẹn*");

            exception.Which.StatusCode.Should().Be(404);

            mockPaymentGateway.Verify(
                x => x.RefundCustomerAsync(It.IsAny<int>(), It.IsAny<decimal>()),
                Times.Never);
        }
    }
}
