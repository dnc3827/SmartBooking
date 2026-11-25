using SmartBookingAPI.Models;
using Xunit;
using SmartBookingAPI.Services;
using SmartBookingAPI.Services.Strategies;
using FluentAssertions;

namespace SmartBooking.Tests
{
    public class PercentageDiscountStrategyTests
    {
        [Fact]
        public void CalculateDiscount_PercentageExceedsMaxAmount_ReturnsMaxDiscountAmount()
        {
            // 1. Arrange (Dàn tr?n)
            // Kh?i t?o Strategy tính %
            var strategy = new PercentageDiscountStrategy();

            // Gi? l?p m?t mã gi?m giá 50%, nh?ng t?i ?a ch? ???c gi?m 30.000 VN?
            var promotion = new Promotion
            {
                Code = "GIAM50",
                DiscountType = "Percentage",
                DiscountValue = 50, // 50%
                MaxDiscountAmount = 30000,
                IsActive = true,
                StartDate = DateTime.Now.AddDays(-1),
                EndDate = DateTime.Now.AddDays(1)
            };

            // Khách hàng c?t bill 100.000 VN?
            decimal originalPrice = 100000;

            // 2. Act (Khai h?a)
            // 50% c?a 100k là 50k, nh?ng Max là 30k. V?y hàm ph?i tr? ra 30k.
            decimal actualDiscount = strategy.CalculateDiscount(promotion, originalPrice);

            // 3. Assert (Nghi?m thu)
            actualDiscount.Should().Be(30000);
        }
    }
}