using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartBookingAPI.Services
{
    public class BarberService : IBarberService
    {   
        private readonly SmartBookingDbContext _context;

        public BarberService(SmartBookingDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<string>> GetAvailableSlotsAsync(int barberId, DateOnly date)
        {
            // 1. Kiểm tra Barber có tồn tại không
            var barberExists = await _context.Barbers.AnyAsync(b => b.BarberId == barberId);
            if (!barberExists)
            {
                throw new AppException("Không tìm thấy thông tin thợ cắt tóc.", 404);
            }

            // 2. Lấy danh sách các lịch bận của thợ trong ngày được chọn
            // Bỏ qua các lịch có Status là "Cancelled" hoặc "Failed" vì các giờ đó vẫn trống
            var busyAppointments = await _context.Appointments
                .Where(a => a.BarberId == barberId
                         && a.AppointmentDate == date
                         && a.Status != "Cancelled"
                         && a.Status != "Failed")
                .Select(a => new { a.StartTime, a.EndTime })
                .ToListAsync();

            // 3. Khởi tạo giờ làm việc (Giả sử tiệm mở từ 08:00 đến 20:00, mỗi ca 30 phút)
            var workStart = new TimeOnly(8, 0);
            var workEnd = new TimeOnly(20, 0);
            var slotDuration = TimeSpan.FromMinutes(30);

            var availableSlots = new List<string>();
            var currentSlot = workStart;

            // 4. Quét từng khung giờ để lọc ra giờ trống
            while (currentSlot < workEnd)
            {
                var slotEnd = currentSlot.Add(slotDuration);

                // Kiểm tra xem khung giờ hiện tại có bị "đè" lên lịch bận nào không
                // Công thức Allen Interval Overlap: StartA < EndB VÀ EndA > StartB
                bool isOverlap = busyAppointments.Any(appt =>
                    currentSlot < appt.EndTime && slotEnd > appt.StartTime);

                // Nếu không đè lên lịch nào -> Khung giờ này rảnh
                if (!isOverlap)
                {
                    availableSlots.Add(currentSlot.ToString("HH:mm"));
                }
                
                currentSlot = slotEnd;
            }

            return availableSlots;
        }

        public async Task<IEnumerable<Barber>> GetAllBarbersAsync()
        {
            return await _context.Barbers.ToListAsync();
        }

        public async Task<Barber> GetBarberByIdAsync(int id)
        {
            var barber = await _context.Barbers.FindAsync(id);
            if (barber == null)
            {
                // Sử dụng AppException theo chuẩn của dự án
                throw new AppException("Không tìm thấy thợ cắt tóc", 404);
            }
            return barber;
        }

        public async Task<Barber> CreateBarberAsync(CreateBarberDto dto)
        {
            var newBarber = new Barber
            {
                BarberName = dto.BarberName,
                Rating = dto.Rating // Default 5.0 từ DTO
            };

            await _context.Barbers.AddAsync(newBarber);
            await _context.SaveChangesAsync();
            return newBarber;
        }

        public async Task<Barber> UpdateBarberAsync(int id, UpdateBarberDto dto)
        {
            var barber = await GetBarberByIdAsync(id);

            barber.BarberName = dto.BarberName;
            barber.Rating = dto.Rating;

            _context.Barbers.Update(barber);
            await _context.SaveChangesAsync();
            return barber;
        }

        public async Task<bool> DeleteBarberAsync(int id)
        {
            var barber = await GetBarberByIdAsync(id);

            // Xóa cứng khỏi DB (hoặc bạn có thể chuyển thành Soft Delete nếu cần)
            _context.Barbers.Remove(barber);
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
