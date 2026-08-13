using SmartBookingAPI.Models;

namespace SmartBookingAPI.Services
{
    public interface IAppointmentService
    {
        // Khai báo 1 hành động: Xử lý đặt lịch
        // Trả về chuỗi (string) chứa thông báo thành công hoặc lỗi
        Task<string> CreateAppointmentAsync(int customerId, DTOs.CreateAppointmentDto request);
        // Lấy danh sách lịch hẹn của một khách hàng cụ thể
        Task<List<Appointment>> GetMyAppointmentsAsync(int customerId);
        Task<string> CancelAppointmentAsync(int customerId, int appointmentId);
        Task UpdatePaymentStatusAsync(long appointmentId, bool isSuccess);
    }
}
