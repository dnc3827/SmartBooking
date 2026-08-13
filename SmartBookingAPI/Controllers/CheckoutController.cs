using Microsoft.AspNetCore.Mvc;
using SmartBookingAPI.DTOs;
using SmartBookingAPI.Services;

namespace SmartBookingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CheckoutController : ControllerBase
    {
        private readonly IPaymentGateway _paymentGateway;
        private readonly IAppointmentService _appointmentService;

        // Tiêm Interface vào thay vì thư viện gốcA
        public CheckoutController(IPaymentGateway paymentGateway, IAppointmentService appointmentService)
        {
            _paymentGateway = paymentGateway;
            _appointmentService = appointmentService;
        }

        [HttpPost("create-payment-link")]
        public async Task<IActionResult> CreatePaymentLink()
        {
            try
            {
                int randomOrderCode = int.Parse(DateTimeOffset.Now.ToString("ffffff"));

                // Controller chỉ cần gọi đúng 1 dòng này
                string checkoutUrl = await _paymentGateway.CreatePaymentLink(randomOrderCode, 50000, "Phòng Deluxe");

                return Ok(new
                {
                    error = 0,
                    message = "Success",
                    data = new { url = checkoutUrl }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = -1, message = ex.Message });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookDto payload)
        {
            try
            {
                // 1. Kiểm tra trạng thái giao dịch từ PayOS
                // "00" là mã thành công chuẩn của PayOS
                bool isSuccess = (payload.Code == "00" || payload.Data.Code == "00");

                long appointmentId = payload.Data.OrderCode;

                // 2. Gọi Service để cập nhật DB
                await _appointmentService.UpdatePaymentStatusAsync(appointmentId, isSuccess);

                // 3. Phản hồi 200 OK cho PayOS biết là đã nhận được tin nhắn
                // PayOS yêu cầu response phải trả về JSON format chuẩn xác để họ ngừng gửi lại webhook
                return Ok(new
                {
                    error = 0,
                    message = "Ok",
                    data = (object)null! // Bắt buộc phải có để format chuẩn
                });
            }
            catch (Exception ex)
            {
                // Trả về BadRequest nếu có lỗi để Serilog hoặc Middleware ghi nhận
                return BadRequest(new
                {
                    error = -1,
                    message = ex.Message,
                    data = (object)null!
                });
            }
        }
    }
}
