using System.Text.Json;

namespace SmartBookingAPI.Models
{
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; } // Tùy chọn: Để hiện chi tiết lỗi khi đang fix bug

        // Hàm này giúp tự động biến Object thành chuỗi JSON
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
