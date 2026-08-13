using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartBookingAPI.Services
{
    public class ServiceService : IServiceService
    {
        private readonly SmartBookingDbContext _context;

        public ServiceService(SmartBookingDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Service>> GetAllServicesAsync()
        {
            return await _context.Services.ToListAsync();
        }

        public async Task<Service> GetServiceByIdAsync(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                throw new AppException("Không tìm thấy dịch vụ", 404);
            }
            return service;
        }

        public async Task<Service> CreateServiceAsync(CreateServiceDto dto)
        {
            var newService = new Service
            {
                ServiceName = dto.ServiceName,
                Price = dto.Price,
                DurationMinutes = dto.DurationMinutes // Default 30 từ DTO
            };

            await _context.Services.AddAsync(newService);
            await _context.SaveChangesAsync();
            return newService;
        }

        public async Task<Service> UpdateServiceAsync(int id, UpdateServiceDto dto)
        {
            var service = await GetServiceByIdAsync(id);

            service.ServiceName = dto.ServiceName;
            service.Price = dto.Price;
            service.DurationMinutes = dto.DurationMinutes;

            _context.Services.Update(service);
            await _context.SaveChangesAsync();
            return service;
        }

        public async Task<bool> DeleteServiceAsync(int id)
        {
            var service = await GetServiceByIdAsync(id);

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
