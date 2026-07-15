using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VibeCity_API.Data;
using VibeCity_API.Services;

namespace VibeCity_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeleportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;

        public TeleportController(AppDbContext context, IJwtTokenService jwtTokenService)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
        }

        // BƯỚC 1: Map 1 đăng nhập gửi request yêu cầu dịch chuyển sang Map 2
        [Authorize]
        [HttpPost("request")]
        public async Task<IActionResult> RequestTeleport()
        {
            var studentId = User.Identity?.Name;
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized(new { success = false, message = "Không xác thực được sinh viên từ Token Map 1." });
            }

            try
            {
                // Tạo mã vé dịch chuyển duy nhất
                string ticketCode = Guid.NewGuid().ToString("N");

                var ticket = new TeleportTicket
                {
                    StudentId = studentId,
                    TicketCode = ticketCode,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(2) // Hạn sử dụng vé 2 phút
                };

                _context.TeleportTickets.Add(ticket);
                await _context.SaveChangesAsync();

                Console.WriteLine($"🎫 [Teleport] Đã cấp vé dịch chuyển dùng 1 lần cho sinh viên '{studentId}'. Mã vé: {ticketCode}");

                return Ok(new { success = true, code = ticketCode });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi sinh vé dịch chuyển.", detail = ex.Message });
            }
        }

        // BƯỚC 2: Map 2 gửi mã code lên để đổi lấy Token hoạt động chính thức
        [AllowAnonymous]
        [HttpPost("exchange")]
        public async Task<IActionResult> ExchangeTeleport([FromBody] ExchangeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, message = "Mã xác thực vé không hợp lệ hoặc bị trống!" });
            }

            try
            {
                // Tìm kiếm vé trên DB
                var ticket = await _context.TeleportTickets
                    .FirstOrDefaultAsync(t => t.TicketCode == request.Code);

                if (ticket == null)
                {
                    return BadRequest(new { success = false, message = "Vé dịch chuyển không tồn tại hoặc đã được sử dụng trước đó!" });
                }

                // Kiểm tra hạn sử dụng của vé dịch chuyển
                if (DateTime.UtcNow > ticket.ExpiresAt)
                {
                    _context.TeleportTickets.Remove(ticket);
                    await _context.SaveChangesAsync();
                    return BadRequest(new { success = false, message = "Vé dịch chuyển này đã hết hạn sử dụng (quá 2 phút)!" });
                }

                // Vé hợp lệ, tìm kiếm tài khoản sinh viên sở hữu vé này
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.StudentId == ticket.StudentId);

                if (student == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản sinh viên gắn liền với vé này." });
                }

                // Xóa vé dịch chuyển ngay lập tức để tránh việc Refresh lại trang web Map 2 hack vé (Sử dụng 1 lần duy nhất)
                _context.TeleportTickets.Remove(ticket);
                await _context.SaveChangesAsync();

                // Tạo Token mới cho Map 2 bằng JwtTokenService thống nhất
                string token = _jwtTokenService.CreateStudentToken(student);

                Console.WriteLine($"🚀 [Teleport] Đã xác thực vé thành công! Hủy vé '{request.Code}' và đưa Sinh viên {student.FullName} vào Map 2.");

                return Ok(new
                {
                    success = true,
                    token = token,
                    studentId = student.StudentId,
                    fullName = student.FullName,
                    major = student.Major,
                    gpa = student.Gpa,
                    unlockedSkills = student.UnlockedSkills,
                    survivedDays = student.SurvivedDays,
                    vibeCoin = student.VibeCoin
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xử lý xác thực vé tại server.", detail = ex.Message });
            }
        }
    }

    public class ExchangeRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}