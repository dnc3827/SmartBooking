namespace SmartBookingAPI.Models
{
    public class Promotion
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty; // Ví dụ: VIP10, WELCOME

        // Loại giảm giá: "Percentage" (Phần trăm) hoặc "FixedAmount" (Số tiền cố định)
        public string DiscountType { get; set; } = "Percentage";

        public decimal DiscountValue { get; set; } // Giá trị giảm (Ví dụ: 10 cho 10%, hoặc 50000 cho 50k)
        public decimal MaxDiscountAmount { get; set; } // Giảm tối đa bao nhiêu (đối với loại % )

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int MaxUsage { get; set; } // Giới hạn tổng số lần sử dụng của mã này
        public int UsedCount { get; set; } // Số lần đã dùng thực tế
        public decimal MinOrderValue { get; set; } // Đơn hàng từ bao nhiêu tiền trở lên mới được dùng mã này
        public bool IsActive { get; set; } = true;
    }
}
