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
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;

namespace VibeCity_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();

        public AiController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // --- CÁC CLASS DỮ LIỆU (DTOs) ---
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

        public class QuizSubmission
        {
            public int Score { get; set; }
            public string StudentId { get; set; }
        }

        public class ZombieUpdate
        {
            public int ZombieId { get; set; }
            public string StudentId { get; set; }
            public float NewX { get; set; }
            public float NewY { get; set; }
            public float NewZ { get; set; }
        }

        // 1. ENDPOINT: TƯ VẤN VÀ TẠO QUIZ
        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] List<string> subjects)
        {
            try
            {
                var student = await _context.Students.OrderBy(s => s.Id).FirstOrDefaultAsync();
                string name = student?.FullName ?? "Lê Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                var apiKey1 = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"];
                var apiOpenRouter = Environment.GetEnvironmentVariable("OpenRouter_API_Key") ?? _configuration["OpenRouter_API_Key"];

                string subjectList = (subjects != null && subjects.Count > 0) ? string.Join(", ", subjects) : "các môn chuyên ngành";

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. Hãy tư vấn ngắn gọn về {subjectList}. " +
                                "Yêu cầu trả về JSON THUẦN (không kèm text khác) với cấu trúc: " +
                                "{ " +
                                "\"advice\": \"Lời khuyên chuyên sâu...\", " +
                                "\"closingQuestion\": \"Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!\", " +
                                "\"quiz\": [ { \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 } ] " +
                                "} " +
                                "Tạo đúng 5 câu hỏi trắc nghiệm liên quan.";

                string rawText = "";
                try
                {
                    var client = new GenerativeModel(apiKey1, "gemini-1.5-flash");
                    var response = await client.GenerateContentAsync(prompt);
                    rawText = response?.Text ?? "";
                }
                catch { rawText = ""; }

                if (string.IsNullOrEmpty(rawText) || !rawText.Contains("{"))
                {
                    if (!string.IsNullOrEmpty(apiOpenRouter)) rawText = await CallOpenRouter(apiOpenRouter, prompt);
                }

                var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    var resultObject = JsonConvert.DeserializeObject<AiResponse>(match.Value);
                    if (resultObject != null) return Ok(resultObject);
                }
                return StatusCode(500, new { error = "AI tạm thời không phản hồi." });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // 2. ENDPOINT: NỘP BÀI QUIZ
        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmission res)
        {
            try
            {
                bool isFail = res.Score <= 2;
                string message = isFail
                    ? "Kiến thức của bạn quá yếu! Lũ Zombie đã đánh sập cổng trường HCMUTE rồi! CHẠY NGAY ĐI!"
                    : "Tuyệt vời! Kiến thức vững vàng đã giúp bảo vệ vòng vây an toàn của trường.";

                if (!isFail)
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == res.StudentId);
                    if (student != null)
                    {
                        student.Gpa = (student.Gpa ?? 0) + 0.01f;
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true, breakSafeZone = isFail, advice = message, finalScore = res.Score });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // 3. ENDPOINT: HỒI SINH ZOMBIE (ÉP KIỂU DECIMAL CHUẨN)
        [HttpPost("zombie/respawn")]
        public async Task<IActionResult> RespawnZombie([FromBody] ZombieUpdate model)
        {
            try
            {
                var npc = await _context.Npcs.FirstOrDefaultAsync(n => n.Id == model.ZombieId);
                if (npc != null)
                {
                    // Ép kiểu float sang decimal để tương thích với kiểu dữ liệu SQL Server (nếu có)
                    npc.PositionX = Convert.ToDecimal(model.NewX);
                    npc.PositionY = Convert.ToDecimal(model.NewY);
                    npc.PositionZ = Convert.ToDecimal(model.NewZ);
                }

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == model.StudentId);
                if (student != null)
                {
                    student.Gpa = (student.Gpa ?? 0) + 0.05f;
                    if (student.Gpa > 4.0f) student.Gpa = 4.0f;
                }

                await _context.SaveChangesAsync();
                return Ok(new
                {
                    newGpa = student?.Gpa,
                    message = "GPA đã tăng! Zombie đã hồi sinh tại vị trí mới."
                });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // --- HÀM HỖ TRỢ AI ---
        private async Task<string> CallOpenRouter(string apiKey, string prompt)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var body = new
                {
                    model = "meta-llama/llama-3-8b-instruct:free",
                    messages = new[] { new { role = "user", content = prompt } }
                };
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
                dynamic json = JsonConvert.DeserializeObject(result);
                return json.choices[0].message.content;
            }
            catch { return ""; }
        }
    }
}