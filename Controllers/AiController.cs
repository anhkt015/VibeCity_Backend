using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google_GenerativeAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VibeCity_API.Data;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;

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

        public class QuizQuestion
        {
            public string? question { get; set; }
            public List<string>? options { get; set; }
            public int answer { get; set; }
        }

        public class AiResponse
        {
            public string? advice { get; set; }
            public string? closingQuestion { get; set; }
            public List<QuizQuestion>? quiz { get; set; }
        }

        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] List<string> subjects)
        {
            try
            {
                // 1. Lấy thông tin sinh viên
                var student = await _context.Students
                                            .OrderBy(s => s.Id)
                                            .FirstOrDefaultAsync();

                string name = student?.FullName ?? "Lê Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                // 2. Lấy 2 API Key
                var apiKey1 = Environment.GetEnvironmentVariable("Gemini_API_Key")
                              ?? _configuration["Gemini_API_Key"];

                var apiKey2 = Environment.GetEnvironmentVariable("Gemini_API_Key_Backup")
                              ?? _configuration["Gemini_API_Key_Backup"];

                string subjectList = (subjects != null && subjects.Count > 0)
                                     ? string.Join(", ", subjects)
                                     : "các môn đại cương";

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. " +
                                $"Hãy tư vấn ngắn gọn về {subjectList}. " +
                                "YÊU CẦU: Trả về DUY NHẤT một khối JSON. " +
                                "Trong 'closingQuestion' PHẢI dặn: 'Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!'. " +
                                "Cấu trúc JSON: " +
                                "{" +
                                "  \"advice\": \"nội dung tư vấn\", " +
                                "  \"closingQuestion\": \"...\", " +
                                "  \"quiz\": [ { \"question\": \"...\", \"options\": [\"a\", \"b\", \"c\", \"d\"], \"answer\": 0 } ] " +
                                "}" +
                                "Lưu ý: Tạo đúng 5 câu hỏi trắc nghiệm liên quan.";

                string rawText = "";

                // --- CHIẾN THUẬT: 2.5 FLASH SONG KIẾM HỢP BÍCH ---
                try
                {
                    // Thử Key 1 với 2.5 Flash
                    var client = new GenerativeModel(apiKey1, "gemini-2.5-flash");
                    var response = await client.GenerateContentAsync(prompt);
                    rawText = response?.Text ?? "";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Key chính lỗi: {ex.Message}. Đang thử Key dự phòng bằng bản 2.5...");

                    if (!string.IsNullOrEmpty(apiKey2))
                    {
                        try
                        {
                            // Thử Key 2 cũng với bản 2.5 Flash
                            var backupClient = new GenerativeModel(apiKey2, "gemini-2.5-flash");
                            var backupResponse = await backupClient.GenerateContentAsync(prompt);
                            rawText = backupResponse?.Text ?? "";
                        }
                        catch (Exception exBackup)
                        {
                            Console.WriteLine($"❌ Cả 2 Key đều lỗi: {exBackup.Message}");
                            // Để trống rawText để xuống dưới báo hết hạn mức
                        }
                    }
                }

                // 3. Xử lý bóc tách và trả về
                var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);

                if (match.Success)
                {
                    string cleanJson = match.Value;
                    var resultObject = JsonConvert.DeserializeObject<AiResponse>(cleanJson);

                    if (resultObject != null && !string.IsNullOrEmpty(resultObject.advice))
                    {
                        return Ok(resultObject);
                    }
                }

                // Nếu chạy xuống đây nghĩa là không có rawText (Cả 2 key đều 429 hoặc lỗi)
                return StatusCode(429, new
                {
                    error = "Hết hạn mức rồi Anh ơi!",
                    message = "Cả 2 API Key đều đã dùng hết 20 lượt/ngày. Nhật Anh hãy đổi Key mới hoặc đợi đến ngày mai nhé!"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi nghiêm trọng: {ex.Message}");
                return StatusCode(500, new { error = "Hệ thống AI gặp sự cố kỹ thuật!" });
            }
            // --- ĐOẠN CODE KIỂM TRA KEY (Dán vào cuối class AiController) ---
            [HttpGet("check-config")]
            public IActionResult CheckConfig()
            {
                var key1 = Environment.GetEnvironmentVariable("Gemini_API_Key");
                var key2 = Environment.GetEnvironmentVariable("Gemini_API_Key_Backup");

                var report = new
                {
                    Key1_Status = string.IsNullOrEmpty(key1) ? "TRỐNG (NULL)" : $"Đã nhận: {key1.Substring(0, 5)}***",
                    Key2_Status = string.IsNullOrEmpty(key2) ? "TRỐNG (NULL)" : $"Đã nhận: {key2.Substring(0, 5)}***",
                    Server_Time = DateTime.Now.ToString("HH:mm:ss"),
                    Note = "Nếu báo TRỐNG, hãy kiểm tra lại tên biến trên Render (phải đúng chữ hoa/thường)."
                };

                return Ok(report);
            }
        }
    }
}