using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google_GenerativeAI; // Namespace của Gunpal Jain
using System.Collections.Generic;
using System.Threading.Tasks;
using VibeCity_API.Data; // Để nó thấy AppDbContext và Student từ BuildingController

namespace VibeCity_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] List<string> subjects)
        {
            try
            {
                // 1. Lấy thông tin Nhật Anh
                // Cần chỉ định rõ <Student> nếu VS bị rối giữa các bảng
                var student = await _context.Students.FirstOrDefaultAsync();
                string name = student?.FullName ?? "Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                // 2. Cấu hình AI
                var apiKey = "AIzaSyAykbGxfRJoDasMBiSP61bpiiuOyJf01p8";

                // Khởi tạo model theo đúng cấu trúc Gunpal Jain
                var client = new GenerativeModel(apiKey, "gemini-1.5-flash");

                // 3. Prompt
                string prompt = $"Chào, tôi là sinh viên {name}, ngành {major}. " +
                                $"Tôi đang học: {string.Join(", ", subjects)}. " +
                                "Tư vấn lộ trình học ngắn gọn và 3 câu trắc nghiệm JSON.";

                // 4. GỌI HÀM (QUAN TRỌNG: Phải có Async ở cuối)
                var response = await client.GenerateContentAsync(prompt);

                return Ok(new
                {
                    studentName = name,
                    major = major,
                    advice = response?.Text ?? "AI không trả lời"
                });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}