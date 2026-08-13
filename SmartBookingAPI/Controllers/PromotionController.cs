using Microsoft.AspNetCore.Mvc;
using SmartBookingAPI.Services;

namespace SmartBookingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromotionController : ControllerBase
    {
        private readonly IPromotionService _promotionService;

        public PromotionController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        [HttpGet("check-discount")]
        public async Task<IActionResult> CheckDiscount([FromQuery] string code, [FromQuery] decimal originalPrice)
        {
            try
            {
                // Thử nhờ Service tính toán
                var discountAmount = await _promotionService.CalculateDiscountAsync(code, originalPrice);

                // Nếu thành công êm đẹp, trả về HTTP 200 (OK) với JSON xịn xò
                return Ok(new
                {
                    success = true,
                    message = "Áp dụng mã thành công!",
                    discountAmount = discountAmount
                });
            }
            catch (Exception ex)
            {
                // Nếu Service "hét lên", nhảy ngay vào đây chụp lấy câu chửi (ex.Message)
                // Trả về HTTP 400 (Bad Request) cho Frontend
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    discountAmount = 0
                });
            }
        }

        [HttpPost("apply-discount")]
        public async Task<IActionResult> ApplyDiscount(string code, decimal originalPrice)
        {
            try
            {
                await _promotionService.ApplyPromotionAsync(code, originalPrice);
                return Ok(new { success = true, message = "Chốt đơn thành công! Đã trừ 1 lượt dùng mã." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
