namespace SmartBookingAPI.Services
{
    public interface IPaymentGateway
    {
        Task<string> CreatePaymentLink(int orderCode, int amount, string description);
    }
}
