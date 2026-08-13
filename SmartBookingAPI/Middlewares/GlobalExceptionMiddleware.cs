using SmartBookingAPI.Models;
using System.Net;

namespace SmartBookingAPI.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        // Bác sĩ trưởng trạm: Nhận vào băng chuyền Request và Cuốn sổ ghi log
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        // Hàm này tự động chạy mỗi khi có Request bay vào API
        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                // Trạm y tế mở cửa cho Request đi tiếp vào Controller
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                // Ôi không! Có lỗi văng ra (Crash) ở bên trong rồi!
                // 1. Ghi âm thầm vào sổ tay (Log) để Kỹ sư sửa
                _logger.LogError($"[CÓ BIẾN]: {ex.Message}");

                // 2. Phản hồi lịch sự lại cho Khách hàng
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Mặc định là lỗi 500 (Lỗi server)
            int statusCode = (int)HttpStatusCode.InternalServerError;
            string message = "Đã có lỗi từ phía máy chủ. Xin lỗi vì sự bất tiện này!";

            // KIỂM TRA: Nếu lỗi là do ta chủ động ném ra (AppException)
            if (exception is AppException appEx)
            {
                statusCode = appEx.StatusCode;
                message = appEx.Message;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            // Dùng chính cái ErrorResponse của bạn để đóng gói
            var response = new ErrorResponse
            {
                StatusCode = statusCode,
                Message = message,
                Detail = _env.IsDevelopment() ? exception.StackTrace : null// Hoặc để trống nếu là môi trường Production
            };

            await context.Response.WriteAsync(response.ToString());
        }
    }
}
