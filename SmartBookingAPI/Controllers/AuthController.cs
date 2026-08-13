using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
using Microsoft.EntityFrameworkCore;
using BCryptNet = BCrypt.Net.BCrypt;

namespace SmartBookingAPI.Controllers
{
    [ApiController] // Thêm dòng này
    [Route("api/[controller]")] // Thêm dòng này để đường dẫn là api/auth
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly SmartBookingDbContext _context;

        // Tiêm cấu hình hệ thống vào Controller
        public AuthController(IConfiguration configuration, SmartBookingDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto request)
        {
            // 1. Kiểm tra tên đăng nhập đã tồn tại trong DB chưa
            var isUsernameTaken = await _context.Customers
                .AnyAsync(c => c.Username == request.Username);

            if (isUsernameTaken)
            {
                // Sử dụng Custom Exception của bạn để Middleware tự bắt và trả về lỗi 400 sạch sẽ
                throw new AppException("Tên đăng nhập đã tồn tại trong hệ thống.", 400);
            }

            // 2. Tiến hành băm (hash) mật khẩu thô bảo mật tuyệt đối
            string passwordHash = BCryptNet.HashPassword(request.Password);

            // 3. Tạo đối tượng Customer mới để lưu vào DB
            var newCustomer = new Customer
            {
                Username = request.Username,
                PasswordHash = passwordHash,
                CustomerName = request.CustomerName,
                Phone = request.Phone,
                Email = request.Email,
                Role = "Customer" // Mặc định tài khoản đăng ký qua web là khách hàng
            };

            _context.Customers.Add(newCustomer);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng ký tài khoản thành công!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] DTOs.LoginRequest request) // Giả định DTO của bạn tên là LoginRequest
        {
            // 1. Tìm khách hàng trong Database dựa vào Username
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Username == request.Username);

            // 2. Kiểm tra xem User có tồn tại và Mật khẩu có khớp không?
            // Dùng BCrypt.Verify để so sánh mật khẩu thô gõ vào với mã Hash trong DB
            if (customer == null || !BCryptNet.Verify(request.Password, customer.PasswordHash))
            {
                // 💡 Mẹo bảo mật: Trả về chung 1 câu thông báo để Hacker không biết là sai tài khoản hay sai mật khẩu
                throw new AppException("Tên đăng nhập hoặc mật khẩu không chính xác.", 401);
            }

            // 3. Nếu đúng, tiến hành tạo JWT Token
            var secretKey = _configuration["JwtConfig:SecretKey"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            // Đưa thông tin THẬT của khách hàng vào Token
            var claims = new[]
            {
                // TRỌNG TÂM: Truyền CustomerId thật từ DB vào Claim
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                new Claim(ClaimTypes.Name, customer.CustomerName),
                new Claim(ClaimTypes.Role, customer.Role)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = "HeThongDatLich",
                Audience = "KhachHang",
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                token = tokenHandler.WriteToken(token)
            });
        }

    }
}
