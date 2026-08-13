using SmartBookingAPI.Models;

namespace SmartBookingAPI.Services.Strategies
{
    public interface IPromotionStrategy
    {
        // Tên kiểu giảm giá mà Strategy này hỗ trợ (vd: "Percentage", "FixedAmount")
        string DiscountType { get; }

        // Hàm tính toán số tiền được giảm
        decimal CalculateDiscount(Promotion promotion, decimal originalPrice);
    }
}
