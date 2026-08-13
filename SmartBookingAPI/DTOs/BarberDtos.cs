using System.ComponentModel.DataAnnotations;
namespace SmartBookingAPI.DTOs
{
    
    public class CreateBarberDto
    {
        [Required(ErrorMessage = "Tên thợ cắt tóc không được để trống")]
        public string BarberName { get; set; } = string.Empty;

        // Điểm mặc định là 5.0 theo như tài liệu thiết kế
        [Range(1.0, 5.0)]
        public double Rating { get; set; } = 5.0;
    }

    public class UpdateBarberDto
    {
        [Required(ErrorMessage = "Tên thợ cắt tóc không được để trống")]
        public string BarberName { get; set; } = string.Empty;

        [Range(1.0, 5.0)]
        public double Rating { get; set; }
    }
}
