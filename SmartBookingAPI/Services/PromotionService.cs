using Microsoft.EntityFrameworkCore;
using SmartBookingAPI.Models;
using SmartBookingAPI.Services.Strategies;

namespace SmartBookingAPI.Services
{
    public class PromotionService : IPromotionService
    {
        private readonly SmartBookingDbContext _context;
        private readonly IEnumerable<IPromotionStrategy> _strategies;
        public PromotionService(SmartBookingDbContext context, IEnumerable<IPromotionStrategy> strategies)
        {
            _context = context;
            _strategies = strategies;
        }

        public async Task<decimal> CalculateDiscountAsync(string code, decimal originalPrice)
        {
            // 1. Tìm mã trong DB
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Code == code && p.IsActive);

            // 2. Nếu không tìm thấy mã, hoặc mã hết hạn, hoặc hết lượt dùng -> Không giảm
            if (promotion == null)
                throw new Exception("Mã giảm giá không tồn tại hoặc đã bị khóa.");

            if (DateTime.Now < promotion.StartDate)
                throw new Exception("Mã giảm giá chưa đến thời gian áp dụng.");

            if (DateTime.Now > promotion.EndDate)
                throw new Exception("Mã giảm giá đã hết hạn sử dụng.");

            if (promotion.UsedCount >= promotion.MaxUsage)
                throw new Exception("Mã giảm giá đã hết lượt sử dụng.");

            if (originalPrice < promotion.MinOrderValue)
                throw new Exception($"Đơn hàng chưa đạt mức tối thiểu {promotion.MinOrderValue:N0}đ để áp dụng mã này.");

            // 2. TÌM VÀ ÁP DỤNG STRATEGY (Thay thế hoàn toàn mớ if/else cũ)
            var strategy = _strategies.FirstOrDefault(s => s.DiscountType == promotion.DiscountType);

            if (strategy == null)
            {
                // Đề phòng trường hợp trong DB có loại giảm giá lạ mà code chưa hỗ trợ
                throw new AppException($"Hệ thống chưa hỗ trợ loại giảm giá: {promotion.DiscountType}", 500);
            }

            // Giao việc tính toán cho Strategy phù hợp
            return strategy.CalculateDiscount(promotion, originalPrice);
        }

        public async Task<bool> ApplyPromotionAsync(string code, decimal originalPrice)
        {
            // 1. Tận dụng lại hàm tính tiền để kiểm tra. 
            // Nếu có lỗi (hết hạn, chưa đủ tiền...), nó sẽ tự động ném ra Exception chặn lại ngay tại đây.
            await CalculateDiscountAsync(code, originalPrice);

            // 2. Nếu chạy vượt qua được dòng trên mà không bị ném lỗi, nghĩa là mã hoàn toàn hợp lệ!
            // Tiến hành lấy mã đó ra và trừ đi 1 lượt sử dụng.
            var promotion = await _context.Promotions.FirstAsync(p => p.Code == code);

            promotion.UsedCount += 1; // Cộng thêm 1 vào số lượt đã dùng

            await _context.SaveChangesAsync(); // Lưu thay đổi xuống Database

            return true;
        }
    }
}
