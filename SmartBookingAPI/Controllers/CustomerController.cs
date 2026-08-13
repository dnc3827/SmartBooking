using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBookingAPI.Models;

namespace SmartBookingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly SmartBookingDbContext _context;

        public CustomerController(SmartBookingDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCustomers()
        {
            // Nhờ Entity Framework lặn xuống Database, lấy toàn bộ bảng Customers biến thành dạng List
            var customers = await _context.Customers.ToListAsync();

            // Trả về cho khách hàng (Frontend) mã 200 OK kèm theo gói dữ liệu
            return Ok(customers);
        }
    }
}
