using SmartBookingAPI.Models;

namespace SmartBookingAPI.Services.Strategies
{
    public class PercentageDiscountStrategy : IPromotionStrategy
    {
        public string DiscountType => "Percentage";

        public decimal CalculateDiscount(Promotion promotion, decimal originalPrice)
        {
            // Tính số tiền giảm theo %
            decimal discount = originalPrice * (promotion.DiscountValue / 100m);

            // Nếu có quy định mức giảm tối đa (MaxDiscountAmount) thì áp dụng trần
            if (promotion.MaxDiscountAmount > 0 && discount > promotion.MaxDiscountAmount)
            {
                discount = promotion.MaxDiscountAmount;
            }

            // Số tiền giảm không bao giờ vượt quá tổng tiền gốc của đơn hàng
            return Math.Min(discount, originalPrice);
        }
    }
}
