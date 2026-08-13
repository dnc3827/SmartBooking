using System.ComponentModel.DataAnnotations;

namespace SmartBookingAPI.DTOs
{
    public class CreateServiceDto
    {
        [Required(ErrorMessage = "Tên dịch vụ không được để trống")]
        public string ServiceName { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Giá tiền không hợp lệ")]
        public decimal Price { get; set; }

        // Mặc định 30 phút theo tài liệu thiết kế
        [Range(1, 1440)]
        public int DurationMinutes { get; set; } = 30;
    }

    public class UpdateServiceDto
    {
        [Required(ErrorMessage = "Tên dịch vụ không được để trống")]
        public string ServiceName { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Giá tiền không hợp lệ")]
        public decimal Price { get; set; }

        [Range(1, 1440)]
        public int DurationMinutes { get; set; }
    }
}
