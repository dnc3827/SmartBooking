using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
// Khai báo namespace của bạn ở đây...

namespace SmartBookingAPI.Services
{
    

    public class AppointmentService : IAppointmentService
    {
        private readonly SmartBookingDbContext _context;
        private readonly IPaymentGateway _paymentGateway;
        private readonly IPromotionService _promotionService;

        // Tiêm (Inject) Database và Cổng thanh toán vào Service
        public AppointmentService(SmartBookingDbContext context, IPaymentGateway paymentGateway, IPromotionService promotionService)
        {
            _context = context;
            _paymentGateway = paymentGateway;
            _promotionService = promotionService;
        }

        public async Task<List<Appointment>> GetMyAppointmentsAsync(int customerId)
        {
            return await _context.Appointments
                // Bổ sung Include để Entity Framework join các bảng liên quan
                .Include(a => a.Barber)
                .Include(a => a.Services)
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();
        }

        
        public async Task<string> CreateAppointmentAsync(int customerId, CreateAppointmentDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Tách DateOnly và TimeOnly từ DateTime StartTime của DTO
                var appointmentDate = DateOnly.FromDateTime(dto.StartTime);
                var startTimeOnly = TimeOnly.FromDateTime(dto.StartTime);

                // 1. Lấy dịch vụ và tính tổng thời gian
                var (services, totalDuration) = await GetSelectedServicesAndDurationAsync(dto.ServiceId);
                var endTimeOnly = startTimeOnly.AddMinutes(totalDuration);

                // 2. Kiểm tra trùng lịch
                await EnsureNoScheduleConflictAsync(dto.BarberId, appointmentDate, startTimeOnly, endTimeOnly);

                // 3. Tính toán số tiền cuối cùng (bao gồm khuyến mãi)
                var originalPrice = services.Sum(s => s.Price);
                var finalAmount = await CalculateFinalAmountAsync(dto.PromoCode, originalPrice);

                // 4. LƯU DATABASE TRƯỚC ĐỂ LẤY MÃ ĐƠN HÀNG (AppointmentId)
                var appointment = new Appointment
                {
                    CustomerId = customerId,
                    BarberId = dto.BarberId,
                    AppointmentDate = appointmentDate,
                    StartTime = startTimeOnly,
                    EndTime = endTimeOnly,
                    Status = "Pending", // Đợi khách quét mã QR xong Webhook sẽ đổi thành Paid
                    CreatedAt = DateTime.Now,
                    Services = services
                };

                _context.Appointments.Add(appointment);

                

                await _context.SaveChangesAsync(); // Lúc này EF Core sẽ tự sinh ra appointment.AppointmentId

                // 5. Áp dụng mã giảm giá (tăng lượt dùng)
                if (!string.IsNullOrEmpty(dto.PromoCode))
                {
                    await _promotionService.ApplyPromotionAsync(dto.PromoCode, originalPrice);
                }

                // 6. GỌI PAYOS ĐỂ TẠO LINK THANH TOÁN
                // Ép kiểu decimal sang int cho amount, và dùng AppointmentId làm orderCode
                string paymentUrl = await _paymentGateway.CreatePaymentLink(
                    orderCode: appointment.AppointmentId,
                    amount: (int)finalAmount,
                    description: $"Thanh toan lich hen {appointment.AppointmentId}"
                );

                // 7. Chốt giao dịch
                await transaction.CommitAsync();

                // 8. Trả về đường link để Controller gửi cho Frontend (khách hàng sẽ click vào để quét QR)
                return paymentUrl;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // --------------------------------------------------------
        // CÁC HÀM PHỤ TRỢ (Helper Methods)
        // --------------------------------------------------------

        private async Task<(List<Service> Services, int TotalDuration)> GetSelectedServicesAndDurationAsync(List<int> serviceIds)
        {
            var services = await _context.Services
                .Where(s => serviceIds.Contains(s.ServiceId))
                .ToListAsync();

            if (!services.Any() || services.Count != serviceIds.Count)
                throw new AppException("Một hoặc nhiều dịch vụ không tồn tại.", 400);

            int totalDuration = services.Sum(s => s.DurationMinutes);
            return (services, totalDuration);
        }

        private async Task EnsureNoScheduleConflictAsync(int barberId, DateOnly date, TimeOnly startTime, TimeOnly endTime)
        {
            // Allen Interval Overlap: (StartA < EndB) AND (EndA > StartB)
            var isConflict = await _context.Appointments.AnyAsync(a =>
                a.BarberId == barberId &&
                a.AppointmentDate == date &&
                a.Status != "Cancelled" &&
                a.StartTime < endTime &&
                a.EndTime > startTime);

            //if (isConflict)
            //    throw new AppException("Thợ cắt tóc đã kẹt lịch trong khoảng thời gian này.", 409);
        }

        private async Task<decimal> CalculateFinalAmountAsync(string promoCode, decimal originalPrice)
        {
            if (string.IsNullOrEmpty(promoCode))
                return originalPrice;

            var discountAmount = await _promotionService.CalculateDiscountAsync(promoCode, originalPrice);
            var finalAmount = originalPrice - discountAmount;

            return finalAmount >= 0 ? finalAmount : 0;
        }

        public async Task<string> CancelAppointmentAsync(int customerId, int appointmentId)
        {
            // BƯỚC 1: Tìm hồ sơ lịch hẹn (BẮT BUỘC phải .Include(a => a.Services) để lấy được các dịch vụ đi kèm)
            var appointment = await _context.Appointments
                .Include(a => a.Services)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
                return "Lỗi: Không tìm thấy lịch hẹn!";

            // Kiểm tra Quyền sở hữu (Bảo mật IDOR)
            if (appointment.CustomerId != customerId)
                return "Lỗi: Bạn không có quyền hủy lịch hẹn của người khác!";

            // Kiểm tra Trạng thái
            if (appointment.Status == "Cancelled")
                return "Lỗi: Lịch hẹn này đã bị hủy từ trước rồi!";
            if (appointment.Status == "Completed")
                return "Lỗi: Lịch hẹn đã cắt xong, không thể hủy ăn vạ!";

            // BƯỚC 4: CHÍNH SÁCH HOÀN TIỀN THỰC TẾ (Quy tắc 24h)
            DateTime appointmentStartDateTime = appointment.AppointmentDate.ToDateTime(appointment.StartTime);
            TimeSpan timeUntilAppointment = appointmentStartDateTime - DateTime.Now;

            bool isRefunded = false;

            // Tính tổng số tiền thực tế của các dịch vụ trong lịch hẹn này để hoàn lại
            decimal totalAmount = appointment.Services.Sum(s => s.Price);

            if (timeUntilAppointment.TotalHours >= 24)
            {
               
            }

            // Lưu vết lịch sử (Soft Delete)
            appointment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            if (isRefunded)
            {
                return $"Hủy lịch thành công! Hệ thống đã hoàn lại {totalAmount:N0} VNĐ vào tài khoản của bạn.";
            }
            else
            {
                return $"Hủy lịch thành công! Tuy nhiên, do bạn báo hủy sát giờ (dưới 24h), hệ thống không thể hoàn lại số tiền {totalAmount:N0} VNĐ theo quy định.";
            }
        }

        public async Task UpdatePaymentStatusAsync(long appointmentId, bool isSuccess)
        {
            // Tìm lịch hẹn dựa trên OrderCode (chính là AppointmentId)
            var appointment = await _context.Appointments.FindAsync(appointmentId);

            if (appointment == null)
            {
                throw new AppException($"Không tìm thấy lịch hẹn với ID {appointmentId} từ Webhook.", 404);
            }

            // Chỉ cập nhật nếu trạng thái đang là Pending
            if (isSuccess && appointment.Status == "Pending")
            {
                appointment.Status = "Paid";
                // Nếu muốn lưu thêm mã giao dịch, bạn có thể thêm trường TransactionId vào entity Appointment sau này

                await _context.SaveChangesAsync();
            }
            else if (!isSuccess && appointment.Status == "Pending")
            {
                appointment.Status = "Failed";
                await _context.SaveChangesAsync();
            }
        }
    }
}