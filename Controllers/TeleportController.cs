using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace VibeCity_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeleportController : ControllerBase
    {
        // Khóa bí mật dùng để ký Token (Bắt buộc phải dài từ 32 ký tự trở lên)
        private const string SecretKey = "DayLaChiaKhoaBiMatSieuCapVipProCuaVibeCity2026!";

        [HttpPost("request-teleport")]
        public IActionResult RequestTeleport([FromBody] TeleportRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Name))
            {
                return BadRequest(new { success = false, message = "Thiếu thông tin người chơi" });
            }

            try
            {
                // 1. Đóng gói thông tin người chơi vào "Claims"
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, request.Name),
                    new Claim("score", request.Score.ToString())
                };

                // 2. Tạo khóa bảo mật từ SecretKey
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                // 3. Thiết lập Token (Đặt thời gian sống cực ngắn: 1 phút)
                var token = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(1),
                    signingCredentials: creds
                );

                // 4. Xuất Token ra chuỗi mã hóa JWT
                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                // Trả về cho Unity Game 1
                return Ok(new { success = true, token = tokenString });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    // Class hứng dữ liệu JSON gửi lên từ Unity
    public class TeleportRequest
    {
        public string Name { get; set; }
        public int Score { get; set; }
    }
}