namespace SmartBookingAPI.Services
{
    public interface IPromotionService
    {
        Task<decimal> CalculateDiscountAsync(string code, decimal originalPrice);
        Task<bool> ApplyPromotionAsync(string code, decimal originalPrice);
    }
}
