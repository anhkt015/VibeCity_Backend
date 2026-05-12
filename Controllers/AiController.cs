using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google_GenerativeAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration; // Phải thêm dòng này để dùng IConfiguration
using VibeCity_API.Data;

namespace VibeCity_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration; // 1. Khai báo IConfiguration

        // 2. Tiêm cả DbContext và Configuration vào Constructor
        public AiController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] List<string> subjects)
        {
            try
            {
                // 1. Lấy thông tin sinh viên (Sửa lỗi EF cảnh báo bằng OrderBy)
                var student = await _context.Students
                                            .OrderBy(s => s.Id)
                                            .FirstOrDefaultAsync();

                string name = student?.FullName ?? "Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                // 2. Cấu hình AI - Ưu tiên lấy từ Environment (Render) rồi mới đến appsettings
                var apiKey = Environment.GetEnvironmentVariable("Gemini_API_Key")
                             ?? _configuration["Gemini_API_Key"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    return BadRequest(new { error = "Chưa cấu hình API Key trên Render!" });
                }

                // Khởi tạo model
                var client = new GenerativeModel(apiKey, "gemini-2.5-flash");

                // 3. Prompt cá nhân hóa cho Nhật Anh
                string subjectList = (subjects != null && subjects.Count > 0)
                                     ? string.Join(", ", subjects)
                                     : "các môn đại cương";

                string prompt = $"Chào, tôi là sinh viên {name}, đang theo học ngành {major} tại HCMUTE. " +
                                $"Tôi đang tìm hiểu về: {subjectList}. " +
                                "Hãy tư vấn lộ trình học tập ngắn gọn và tạo 3 câu hỏi trắc nghiệm kiến thức dưới dạng JSON.";

                // 4. Gọi API Gemini
                var response = await client.GenerateContentAsync(prompt);

                return Ok(new
                {
                    studentName = name,
                    major = major,
                    advice = response?.Text ?? "AI đang bận, thử lại sau nhé!"
                });
            }
            catch (Exception ex)
            {
                // Log lỗi chi tiết ra console của Render để dễ debug
                Console.WriteLine($"❌ [AI Error]: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}