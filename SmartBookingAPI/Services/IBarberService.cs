using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;

namespace SmartBookingAPI.Services
{
    public interface IBarberService
    {
        Task<IEnumerable<Barber>> GetAllBarbersAsync();
        Task<IEnumerable<string>> GetAvailableSlotsAsync(int barberId, DateOnly date);
        Task<Barber> GetBarberByIdAsync(int id);
        Task<Barber> CreateBarberAsync(CreateBarberDto dto);
        Task<Barber> UpdateBarberAsync(int id, UpdateBarberDto dto);
        Task<bool> DeleteBarberAsync(int id);
    }
}
