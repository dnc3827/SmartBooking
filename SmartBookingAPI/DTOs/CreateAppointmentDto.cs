using System.ComponentModel.DataAnnotations;

namespace SmartBookingAPI.DTOs
{
    public class CreateAppointmentDto : IValidatableObject
    {
        // ĐÃ XÓA CustomerId. Backend sẽ tự bóc từ Token!

        [Required(ErrorMessage = "Vui lòng chọn thợ cắt tóc.")]
        [Range(1, int.MaxValue, ErrorMessage = "Mã thợ cắt tóc phải lớn hơn 0.")]
        public int BarberId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn dịch vụ!")]
        [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 dịch vụ.")]
        public List<int> ServiceId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
        public DateTime StartTime { get; set; }
        public string? PromoCode { get; set; }

        // Viết luật tùy chỉnh ở đây
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // 1. Kiểm tra không cho đặt lịch trong quá khứ
            if (StartTime <= DateTime.Now)
            {
                yield return new ValidationResult(
                    "Thời gian đặt lịch phải ở trong tương lai.",
                    new[] { nameof(StartTime) }
                );
            }

            // 2. (Mở rộng) Bạn có thể thêm luật: Không cho đặt lịch ngoài giờ làm việc (ví dụ: chỉ cho đặt từ 8h sáng đến 8h tối)
            if (StartTime.Hour < 8 || StartTime.Hour > 20)
            {
                yield return new ValidationResult(
                    "Tiệm chỉ hoạt động từ 8h sáng đến 8h tối.",
                    new[] { nameof(StartTime) }
                );
            }
        }
    }
}
