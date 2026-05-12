using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google_GenerativeAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VibeCity_API.Data;
using Newtonsoft.Json; // Đảm bảo đã cài package Newtonsoft.Json
using System.Linq;

namespace VibeCity_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AiController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // --- Định nghĩa cấu trúc dữ liệu để Unity dễ đọc ---
        public class QuizQuestion
        {
            public string question { get; set; }
            public List<string> options { get; set; }
            public int answer { get; set; }
        }

        public class AiResponse
        {
            public string advice { get; set; }
            public string closingQuestion { get; set; }
            public List<QuizQuestion> quiz { get; set; }
        }

        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] List<string> subjects)
        {
            try
            {
                // 1. Lấy thông tin sinh viên (giữ nguyên logic của ông Anh)
                var student = await _context.Students
                                            .OrderBy(s => s.Id)
                                            .FirstOrDefaultAsync();

                string name = student?.FullName ?? "Lê Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                // 2. Cấu hình API Key
                var apiKey = Environment.GetEnvironmentVariable("Gemini_API_Key")
                             ?? _configuration["Gemini_API_Key"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    return BadRequest(new { error = "Chưa cấu hình API Key trên Render!" });
                }

                var client = new GenerativeModel(apiKey, "gemini-2.5-flash");

                // 3. Prompt ép AI trả về đúng JSON
                string subjectList = (subjects != null && subjects.Count > 0)
                                     ? string.Join(", ", subjects)
                                     : "các môn đại cương";

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. " +
                                $"Hãy tư vấn ngắn gọn về {subjectList}. " +
                                "Yêu cầu: Trả về DUY NHẤT một khối JSON (không kèm lời chào) có cấu trúc sau: " +
                                "{" +
                                "  \"advice\": \"nội dung tóm tắt tư vấn\", " +
                                "  \"closingQuestion\": \"Câu hỏi gợi mở bạn có thắc mắc gì không?\", " +
                                "  \"quiz\": [ { \"question\": \"...\", \"options\": [\"a\", \"b\", \"c\", \"d\"], \"answer\": 0 } ] " +
                                "}" +
                                "Lưu ý: Tạo đúng 5 câu hỏi trắc nghiệm liên quan đến nội dung tư vấn.";

                // 4. Gọi API Gemini
                var response = await client.GenerateContentAsync(prompt);
                string rawText = response?.Text ?? "";

                // 5. Xử lý chuỗi (Lọc sạch các ký tự ```json nếu AI lỡ tay viết vào)
                string cleanJson = rawText.Replace("```json", "").Replace("```", "").Trim();

                // Giải mã thử để kiểm tra tính hợp lệ của JSON trước khi gửi đi
                var resultObject = JsonConvert.DeserializeObject<AiResponse>(cleanJson);

                // Trả về trực tiếp Object để ASP.NET Core tự convert sang JSON sạch
                return Ok(resultObject);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI Error]: {ex.Message}");
                return StatusCode(500, new { error = "AI trả về dữ liệu không đúng cấu trúc, hãy thử lại!" });
            }
        }
    }
}