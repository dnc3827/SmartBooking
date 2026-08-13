using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
using SmartBookingAPI.Services;
using System.Security.Claims;

namespace SmartBookingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        // 1. Khai báo "Bản hợp đồng" thay vì Database
        private readonly IAppointmentService _appointmentService;

        // Tiêm Service vào Controller
        public AppointmentController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        /// <summary>
        /// API đặt lịch cắt tóc
        /// </summary>
        [Authorize] // Bắt buộc phải có thẻ JWT
        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentDto request)
        {
            // Lấy CustomerId từ JWT Token
            var customerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(customerIdStr, out int customerId))
            {
                return Unauthorized(new { Message = "Token không hợp lệ" });
            }

            // Gọi Service
            var checkoutUrl = await _appointmentService.CreateAppointmentAsync(customerId, request);

            // Trả về JSON chứa URL thanh toán
            return Ok(new
            {
                Message = "Tạo lịch hẹn thành công, vui lòng thanh toán để xác nhận.",
                CheckoutUrl = checkoutUrl
            });
        }

        [Authorize]
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory()
        {
            // BƯỚC A: Lấy ID từ Token (Lại là dòng code quen thuộc)
            var customerIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(customerIdString, out int customerId)) return Unauthorized();

            // BƯỚC B: Giao việc cho Service
            var history = await _appointmentService.GetMyAppointmentsAsync(customerId);

            // BƯỚC C: Trả kết quả
            return Ok(history);
        }

        [HttpGet("test-loi")]
        public IActionResult TestLoi()
        {
            // Dòng code này sẽ đóng vai trò như một cú nổ làm sập luồng này
            throw new Exception("BÙM! Đây là lỗi cố ý để test hệ thống Serilog và Middleware!");

            // (Các code dưới này sẽ không bao giờ chạy tới)
            return Ok("Thành công");
        }

        // Tạo API để gọi hàm Cancel
        [Authorize]
        [HttpDelete("cancel/{appointmentId}")]
        public async Task<IActionResult> CancelAppointment(int appointmentId)
        {
            // 2. Tự động lấy CustomerId từ Claims bí mật trong Token chứ không tin dùng dữ liệu từ Client gửi lên
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int customerId))
            {
                return Unauthorized(new { success = false, message = "Token không hợp lệ hoặc đã hết hạn!" });
            }

            try
            {
                // 3. Gọi Service xử lý nghiệp vụ với ID chính chủ
                var result = await _appointmentService.CancelAppointmentAsync(customerId, appointmentId);

                // Đoạn này xử lý dựa trên việc Service của bạn đang return chuỗi string báo lỗi/thành công
                if (result.StartsWith("Lỗi:"))
                {
                    return BadRequest(new { success = false, message = result });
                }

                return Ok(new { success = true, message = result });
            }
            catch (AppException ex) // Bắt custom exception nếu có
            {
                return StatusCode(ex.StatusCode, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Có lỗi hệ thống xảy ra: " + ex.Message });
            }
        }
    }
}
