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

        // --- DTOs ---
        public class AiConsultRequest
        {
            public string StudentId { get; set; } = string.Empty;
            public List<string> Subjects { get; set; } = new List<string>();
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

        public class QuizSubmission
        {
            public int Score { get; set; }
            public string? StudentId { get; set; }
        }

        public class ZombieUpdate
        {
            public int ZombieId { get; set; }
            public string? StudentId { get; set; }
            public float NewX { get; set; }
            public float NewY { get; set; }
            public float NewZ { get; set; }
        }

        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] AiConsultRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.StudentId) || request.Subjects == null || request.Subjects.Count == 0)
                    return BadRequest(new { error = "Dữ liệu đầu vào không hợp lệ." });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
                if (student == null) return BadRequest(new { error = "Không tìm thấy sinh viên." });

                string name = student.FullName;
                string major = student.Major;
                string subjectList = string.Join(", ", request.Subjects.Select(s => s.Trim()));

                var apiKey1 = _configuration["Gemini_API_Key"];
                var apiOpenRouter = _configuration["OpenRouter_API_Key"];
                var groqKey = _configuration["Groq_API_Key"];

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. GPA: {student.Gpa:0.00}. " +
                                $"Tư vấn ngắn gọn về môn: {subjectList}. Trả về JSON THUẦN: " +
                                "{ \"advice\": \"...\", \"closingQuestion\": \"...\", \"quiz\": [ { \"question\": \"...\", \"options\": [\"A\",\"B\",\"C\",\"D\"], \"answer\": 0 } ] } " +
                                "Tạo đúng 5 câu hỏi.";

                string rawText = "";

                // Tầng 1: Gemini
                try
                {
                    if (!string.IsNullOrEmpty(apiKey1))
                    {
                        var client = new GenerativeModel(apiKey1, "gemini-1.5-flash"); // Dùng bản ổn định 1.5
                        var res = await client.GenerateContentAsync(prompt);
                        rawText = res?.Text ?? "";
                    }
                }
                catch { rawText = ""; }

                // Tầng 2 & 3: Dự phòng
                if (string.IsNullOrWhiteSpace(rawText) || !rawText.Contains("{"))
                {
                    if (!string.IsNullOrEmpty(apiOpenRouter))
                        rawText = await CallChatApi("https://openrouter.ai/api/v1/chat/completions", apiOpenRouter, "google/gemini-2.0-flash-001", prompt);

                    if (string.IsNullOrWhiteSpace(rawText) && !string.IsNullOrEmpty(groqKey))
                        rawText = await CallChatApi("https://api.groq.com/openai/v1/chat/completions", groqKey, "llama-3.3-70b-versatile", prompt);
                }

                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        var resultObj = JsonConvert.DeserializeObject<AiResponse>(match.Value);
                        if (IsValidAiResponse(resultObj)) return Ok(resultObj);
                    }
                }

                return Ok(BuildFallbackResponse(name, major, subjectList));
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmission res)
        {
            try
            {
                if (res == null || string.IsNullOrEmpty(res.StudentId)) return BadRequest();
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == res.StudentId);
                if (student == null) return BadRequest();

                bool isFail = res.Score <= 2;
                if (!isFail)
                {
                    student.Gpa = Math.Min(4.0, student.Gpa + 0.01);
                    await _context.SaveChangesAsync();
                }
                return Ok(new { success = true, breakSafeZone = isFail, finalScore = res.Score });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost("zombie/respawn")]
        public async Task<IActionResult> RespawnZombie([FromBody] ZombieUpdate model)
        {
            try
            {
                var npc = await _context.Npcs.FirstOrDefaultAsync(n => n.Id == model.ZombieId);
                if (npc != null)
                {
                    npc.SpawnX = model.NewX; npc.SpawnY = model.NewY; npc.SpawnZ = model.NewZ;
                }
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == model.StudentId);
                if (student != null) student.Gpa = Math.Min(4.0, student.Gpa + 0.05);

                await _context.SaveChangesAsync();
                return Ok(new { message = "Success" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        private bool IsValidAiResponse(AiResponse? r)
        {
            return r != null && !string.IsNullOrEmpty(r.advice) && r.quiz != null && r.quiz.Count > 0;
        }

        private AiResponse BuildFallbackResponse(string name, string major, string subjectList)
        {
            // ĐÂY MỚI LÀ JSON ĐÚNG:
            string json = $@"
            {{
                ""advice"": ""Chào {name}, tôi có một vài lời khuyên về môn {subjectList} dành cho sinh viên {major}. Hãy tập trung vào bài tập thực hành nhé!"",
                ""closingQuestion"": ""Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!"",
                ""quiz"": [
                    {{ ""question"": ""Cách tốt nhất để học {subjectList} là?"", ""options"": [""Chỉ xem video"", ""Thực hành code/bài tập"", ""Học thuộc lòng"", ""Bỏ qua""], ""answer"": 1 }},
                    {{ ""question"": ""Mục tiêu của việc làm Quiz?"", ""options"": [""Lấy điểm"", ""Củng cố kiến thức"", ""Cho vui"", ""Giết thời gian""], ""answer"": 1 }},
                    {{ ""question"": ""Khi gặp bài khó bạn nên?"", ""options"": [""Bỏ luôn"", ""Hỏi AI/Thầy cô"", ""Ngủ"", ""Xóa game""], ""answer"": 1 }},
                    {{ ""question"": ""Môn {subjectList} giúp ích gì?"", ""options"": [""Không giúp gì"", ""Phát triển tư duy"", ""Tốn thời gian"", ""Chỉ để thi""], ""answer"": 1 }},
                    {{ ""question"": ""Cần bao nhiêu thời gian học?"", ""options"": [""1 phút"", ""Nên học mỗi ngày"", ""1 năm mới học 1 lần"", ""Không cần học""], ""answer"": 1 }}
                ]
            }}";
            return JsonConvert.DeserializeObject<AiResponse>(json)!;
        }

        private async Task<string> CallChatApi(string url, string key, string model, string prompt)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                var body = new { model, messages = new[] { new { role = "user", content = prompt } }, response_format = new { type = "json_object" } };
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
                dynamic? json = JsonConvert.DeserializeObject(result);
                return json?.choices[0].message.content ?? "";
            }
            catch { return ""; }
        }
    }
}