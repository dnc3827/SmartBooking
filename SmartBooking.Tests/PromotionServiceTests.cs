using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartBookingAPI.Models;
using SmartBookingAPI.Services.Strategies;
using SmartBookingAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartBooking.Tests
{
    public class PromotionServiceTests
    {
        [Fact]
        public async Task CalculateDiscountAsync_CodeHasReachedMaxUsage_ThrowsAppException()
        {
            // 1. Arrange (Chuẩn bị Database ảo)
            // Tạo cấu hình DB hoàn toàn chạy trong RAM. 
            // Mẹo: Dùng Guid để mỗi lần chạy test là một cái DB mới tinh, không bị trùng lặp dữ liệu với các bài test khác.
            var options = new DbContextOptionsBuilder<SmartBookingDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDB_" + Guid.NewGuid().ToString())
                .Options;

            // Mở kết nối tới DB ảo và "nhét" dữ liệu giả vào
            using var context = new SmartBookingDbContext(options);
            var expiredPromotion = new Promotion
            {
                Code = "HETLUOT",
                DiscountType = "Percentage",
                DiscountValue = 10,
                MaxUsage = 100,
                UsedCount = 100, // Điểm mấu chốt: Đã dùng 100/100 lượt
                IsActive = true,
                StartDate = DateTime.Now.AddDays(-1),
                EndDate = DateTime.Now.AddDays(1)
            };
            context.Promotions.Add(expiredPromotion);
            await context.SaveChangesAsync();

            // Khởi tạo Service với DbContext ảo. 
            // Vì mã đã hết lượt, Service sẽ báo lỗi ngay lập tức, không bao giờ chạy tới bước tính toán. 
            // Do đó, ta chỉ cần truyền một danh sách Strategy rỗng là đủ.
            var strategies = new List<IPromotionStrategy>();
            var promotionService = new PromotionService(context, strategies);

            // 2. Act (Hành động)
            // Thay vì thực thi ngay (sẽ làm crash bài test), ta gói nó vào một Func (delegate) để theo dõi
            Func<Task> action = async () => await promotionService.CalculateDiscountAsync("HETLUOT", 100000);

            // 3. Assert (Nghiệm thu)
            // Ép hệ thống chứng minh rằng: Nếu gọi hàm này, chắc chắn sẽ ném ra AppException
            await action.Should().ThrowAsync<AppException>();

            // Lưu ý: Nếu code gốc của bạn có quăng ra câu thông báo cụ thể như "Mã giảm giá đã hết lượt",
            // bạn có thể test luôn nội dung câu chữ đó bằng cách thêm:
            // .WithMessage("*hết lượt*"); 
        }
    }
}
 