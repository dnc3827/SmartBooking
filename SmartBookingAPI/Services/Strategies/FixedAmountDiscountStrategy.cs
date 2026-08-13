using SmartBookingAPI.Models;

namespace SmartBookingAPI.Services.Strategies
{
    public class FixedAmountDiscountStrategy : IPromotionStrategy
    {
        public string DiscountType => "FixedAmount";

        public decimal CalculateDiscount(Promotion promotion, decimal originalPrice)
        {
            decimal discount = promotion.DiscountValue;

            // Số tiền giảm không bao giờ vượt quá tổng tiền gốc của đơn hàng
            return Math.Min(discount, originalPrice);
        }
    }
}
