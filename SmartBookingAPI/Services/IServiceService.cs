using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;

namespace SmartBookingAPI.Services
{
    public interface IServiceService
    {
        Task<IEnumerable<Service>> GetAllServicesAsync();
        Task<Service> GetServiceByIdAsync(int id);
        Task<Service> CreateServiceAsync(CreateServiceDto dto);
        Task<Service> UpdateServiceAsync(int id, UpdateServiceDto dto);
        Task<bool> DeleteServiceAsync(int id);
    }
}
