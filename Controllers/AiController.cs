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

        // 1. ENDPOINT: TƯ VẤN VÀ TẠO QUIZ
        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] List<string> subjects)
        {
            try
            {
                var student = await _context.Students.OrderBy(s => s.Id).FirstOrDefaultAsync();
                string name = student?.FullName ?? "Lê Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                var apiKey1 = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"] ?? "";
                var apiOpenRouter = Environment.GetEnvironmentVariable("OpenRouter_API_Key") ?? _configuration["OpenRouter_API_Key"];
                var groqKey = Environment.GetEnvironmentVariable("Groq_API_Key") ?? _configuration["Groq_API_Key"];

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

                // --- TẦNG 1: GEMINI 2.5 ---
                try
                {
                    var client = new GenerativeModel(apiKey1, "gemini-2.5-flash");
                    var response = await client.GenerateContentAsync(prompt);
                    rawText = response?.Text ?? "";

                    // KIỂM TRA GIA CỐ: Nếu Gemini nhả text nhưng là text báo lỗi Quota
                    if (rawText.Contains("error") || rawText.Contains("quota") || rawText.Contains("429") || !rawText.Contains("{"))
                    {
                        Console.WriteLine("⚠️ Gemini 2.5 trả về nội dung lỗi hoặc không có JSON. Đang kích hoạt dự phòng...");
                        rawText = "";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Gemini Error]: {ex.Message}");
                    rawText = "";
                }

                // --- TẦNG 2 & 3: DỰ PHÒNG (OPENROUTER & GROQ) ---
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    if (!string.IsNullOrEmpty(apiOpenRouter))
                    {
                        Console.WriteLine("🔄 Đang dùng OpenRouter cứu bồ...");
                        rawText = await CallChatApi("https://openrouter.ai/api/v1/chat/completions", apiOpenRouter, "google/gemini-2.0-flash-001", prompt);
                    }

                    if (string.IsNullOrWhiteSpace(rawText) && !string.IsNullOrEmpty(groqKey))
                    {
                        Console.WriteLine("🔄 OpenRouter tạch, đang dùng Groq...");
                        rawText = await CallChatApi("https://api.groq.com/openai/v1/chat/completions", groqKey, "llama-3.3-70b-versatile", prompt);
                    }
                }

                // --- XỬ LÝ KẾT QUẢ ---
                if (!string.IsNullOrEmpty(rawText))
                {
                    var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        try
                        {
                            var resultObject = JsonConvert.DeserializeObject<AiResponse>(match.Value);
                            if (resultObject != null) return Ok(resultObject);
                        }
                        catch { Console.WriteLine("❌ Lỗi Parse JSON từ AI."); }
                    }
                }

                // --- CỬA CHẶN CUỐI CÙNG: TRẢ VỀ DỮ LIỆU CỨNG ---
                // Tránh trả về lỗi 500 để Game không bị crash/đứng hình
                Console.WriteLine("🆘 Tất cả AI đều tạch. Trả về Quiz mặc định.");
                return Ok(new AiResponse
                {
                    advice = "Hệ thống AI đang bảo trì một chút, Nhật Anh hãy làm tạm bài tập này để ôn kiến thức nhé!",
                    closingQuestion = "Cố gắng đạt điểm tối đa để cộng GPA nha!",
                    quiz = new List<QuizQuestion>
                    {
                        new QuizQuestion {
                            question = "Trong kỹ thuật số, cổng nào thực hiện phép toán cộng logic?",
                            options = new List<string> { "AND", "OR", "NOT", "XOR" },
                            answer = 1
                        },
                        new QuizQuestion {
                            question = "Hệ thống số nhị phân sử dụng bao nhiêu ký số?",
                            options = new List<string> { "1", "2", "8", "10" },
                            answer = 1
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi hệ thống nghiêm trọng: " + ex.Message });
            }
        }

        // 2. ENDPOINT: NỘP BÀI QUIZ
        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmission res)
        {
            try
            {
                bool isFail = res.Score <= 2;
                if (!isFail)
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == res.StudentId);
                    if (student != null)
                    {
                        // FIX LỖI CS0019: Cộng trực tiếp vì double không bao giờ null (mặc định 0.0)
                        student.Gpa += 0.01;
                        await _context.SaveChangesAsync();
                    }
                }
                return Ok(new { success = true, breakSafeZone = isFail, finalScore = res.Score });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // 3. ENDPOINT: HỒI SINH ZOMBIE
        [HttpPost("zombie/respawn")]
        public async Task<IActionResult> RespawnZombie([FromBody] ZombieUpdate model)
        {
            try
            {
                var npc = await _context.Npcs.FirstOrDefaultAsync(n => n.Id == model.ZombieId);
                if (npc != null)
                {
                    // Ép kiểu chuẩn từ float về double cho Supabase
                    npc.SpawnX = (double)model.NewX;
                    npc.SpawnY = (double)model.NewY;
                    npc.SpawnZ = (double)model.NewZ;
                }

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == model.StudentId);
                if (student != null)
                {
                    // FIX LỖI CS0019: Cộng trực tiếp
                    student.Gpa += 0.05;
                    if (student.Gpa > 4.0) student.Gpa = 4.0;
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Zombie respawned and GPA increased!" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

       
        private async Task<string> CallChatApi(string url, string key, string model, string prompt)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

                var body = new
                {
                    model = model,
                    messages = new[] { new { role = "user", content = prompt } },
                    response_format = new { type = "json_object" } // Ép AI nhả JSON
                };

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