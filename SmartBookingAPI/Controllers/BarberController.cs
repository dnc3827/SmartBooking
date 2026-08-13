using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBookingAPI.DTOs;
using SmartBookingAPI.Models;
using SmartBookingAPI.Services;

namespace SmartBookingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BarberController : ControllerBase
    {
        private readonly IBarberService _barberService;

        public BarberController(IBarberService barberService)
        {
            _barberService = barberService;
        }

        [HttpGet("{id}/available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int id, [FromQuery] string date)
        {
            // Parse chuỗi ngày tháng từ URL (VD: "2026-08-11") sang DateOnly
            if (!DateOnly.TryParse(date, out DateOnly parsedDate))
            {
                return BadRequest(new ErrorResponse { Detail = "Định dạng ngày không hợp lệ. Vui lòng dùng định dạng yyyy-MM-dd." });
            }

            var slots = await _barberService.GetAvailableSlotsAsync(id, parsedDate);

            return Ok(new
            {
                BarberId = id,
                Date = parsedDate,
                AvailableSlots = slots
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBarbers()
        {
            var result = await _barberService.GetAllBarbersAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBarberById(int id)
        {
            var result = await _barberService.GetBarberByIdAsync(id);
            return Ok(result);
        }

        [HttpPost]
        [Authorize] // Yêu cầu đăng nhập để thêm/sửa/xóa
        public async Task<IActionResult> CreateBarber([FromBody] CreateBarberDto dto)
        {
            var result = await _barberService.CreateBarberAsync(dto);
            return CreatedAtAction(nameof(GetBarberById), new { id = result.BarberId }, result);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateBarber(int id, [FromBody] UpdateBarberDto dto)
        {
            var result = await _barberService.UpdateBarberAsync(id, dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteBarber(int id)
        {
            await _barberService.DeleteBarberAsync(id);
            return NoContent();
        }
    }
}
