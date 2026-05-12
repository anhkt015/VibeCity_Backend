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
                var student = await _context.Students
                                            .OrderBy(s => s.Id)
                                            .FirstOrDefaultAsync();

                string name = student?.FullName ?? "Lê Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                var apiKey = Environment.GetEnvironmentVariable("Gemini_API_Key")
                             ?? _configuration["Gemini_API_Key"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    return BadRequest(new { error = "Chưa cấu hình API Key!" });
                }

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

                GenerateContentResponse response = null;

                try
                {
                    var client = new GenerativeModel(apiKey, "gemini-2.5-flash");
                    response = await client.GenerateContentAsync(prompt);
                }
                catch (Exception)
                {
                    var fallbackClient = new GenerativeModel(apiKey, "gemini-2.0-flash-lite");
                    response = await fallbackClient.GenerateContentAsync(prompt);
                }

                string rawText = response?.Text ?? "";
                var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);

                if (match.Success)
                {
                    string cleanJson = match.Value;
                    var resultObject = JsonConvert.DeserializeObject<AiResponse>(cleanJson);
                    return Ok(resultObject);
                }
                else
                {
                    return StatusCode(500, new { error = "AI trả về dữ liệu không hợp lệ." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI Error]: {ex.Message}");
                return StatusCode(500, new { error = "Hệ thống đang bận, Nhật Anh vui lòng thử lại sau!" });
            }
        }
    }
}