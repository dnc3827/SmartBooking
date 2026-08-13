using System;
using System.Threading.Tasks;
using PayOS;
using PayOS.Models.V2.PaymentRequests; 

namespace SmartBookingAPI.Services
{
    // Lưu ý: Nếu bạn định dùng service này để thay thế FakePaymentGateway, 
    // bạn có thể đổi interface thành IPaymentGateway.
    public class PaymentService : IPaymentGateway
    {
        private readonly PayOSClient _payOsClient;

        public PaymentService(PayOSClient payOsClient)
        {
            _payOsClient = payOsClient;
        }

        public async Task<string> CreatePaymentLink(int orderCode, int amount, string description)
        {
            try
            {
                // Khởi tạo Request theo chuẩn V2.1.0 (Đã bỏ ItemData)
                var paymentRequest = new CreatePaymentLinkRequest
                {
                    OrderCode = orderCode, // C# sẽ tự động ép kiểu ngầm định từ int sang long
                    Amount = amount,
                    Description = description,
                    ReturnUrl = "http://localhost:3000/success",
                    CancelUrl = "http://localhost:3000/cancel"
                };

                // Gọi hàm tạo link
                var result = await _payOsClient.PaymentRequests.CreateAsync(paymentRequest);

                // Trả về CheckoutUrl (PascalCase)
                return result.CheckoutUrl;
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi tạo link thanh toán: {ex.Message}");
            }
        }
    }
}