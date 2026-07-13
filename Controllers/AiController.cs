/*using Microsoft.AspNetCore.Mvc;
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

        // --- DTOs ĐẦU VÀO ĐÃ FIX LỖI 400 (Dùng chung cho Unity & Test tool) ---
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

        // 1. ENDPOINT: TƯ VẤN VÀ TẠO QUIZ (ĐÃ FIX LỖI 400 & ĐỦ 3 TẦNG AI)
        [HttpPost("consult")]
        public async Task<IActionResult> GetAiAdvice([FromBody] AiConsultRequest request)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào gom cụm từ Body JSON
                if (request == null || string.IsNullOrWhiteSpace(request.StudentId) || request.Subjects == null || request.Subjects.Count == 0)
                    return BadRequest(new { error = "Dữ liệu đầu vào không hợp lệ." });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
                if (student == null) return BadRequest(new { error = "Không tìm thấy sinh viên." });

                string name = student.FullName;
                string major = student.Major;
                string subjectList = string.Join(", ", request.Subjects.Select(s => s.Trim()));

                // Đầy đủ cách lấy Key (Cả Môi trường Render lẫn appsettings.json của file cũ)
                var apiKey1 = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"] ?? "";
                var apiOpenRouter = Environment.GetEnvironmentVariable("OpenRouter_API_Key") ?? _configuration["OpenRouter_API_Key"];
                var groqKey = Environment.GetEnvironmentVariable("Groq_API_Key") ?? _configuration["Groq_API_Key"];

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. GPA: {student.Gpa:0.00}. " +
                                $"Hãy tư vấn ngắn gọn về môn: {subjectList}. " +
                                "Yêu cầu trả về JSON THUẦN (không kèm text khác) với cấu trúc: " +
                                "{ " +
                                "\"advice\": \"Lời khuyên chuyên sâu...\", " +
                                "\"closingQuestion\": \"Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!\", " +
                                "\"quiz\": [ { \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 } ] " +
                                "} " +
                                "Tạo đúng 5 câu hỏi trắc nghiệm liên quan đến nội dung trong lời khuyên.";

                string rawText = "";

                // --- TẦNG 1: GEMINI (Có bắt lỗi Quota / Tràn băng thông) ---
                try
                {
                    if (!string.IsNullOrEmpty(apiKey1))
                    {
                        var client = new GenerativeModel(apiKey1, "gemini-2.5-flash"); // Hoặc đổi sang gemini-2.5-flash tùy ông nhé
                        var res = await client.GenerateContentAsync(prompt);
                        rawText = res?.Text ?? "";

                        // GIA CỐ AN TOÀN từ file cũ của ông: Nếu dính lỗi quota hoặc text bậy bạ
                        if (rawText.Contains("error") || rawText.Contains("quota") || rawText.Contains("429") || !rawText.Contains("{"))
                        {
                            Console.WriteLine("⚠️ Gemini gặp sự cố Quota hoặc lỗi định dạng. Kích hoạt dự phòng...");
                            rawText = "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Gemini Error]: {ex.Message}");
                    rawText = "";
                }

                // --- TẦNG 2 & 3: DỰ PHÒNG TỰ ĐỘNG CHUYỂN AI (OpenRouter & Groq) ---
                if (string.IsNullOrWhiteSpace(rawText) || !rawText.Contains("{"))
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

                // --- XỬ LÝ TRÍCH XUẤT JSON ---
                if (!string.IsNullOrEmpty(rawText))
                {
                    var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        try
                        {
                            var resultObj = JsonConvert.DeserializeObject<AiResponse>(match.Value);
                            if (IsValidAiResponse(resultObj)) return Ok(resultObj);
                        }
                        catch { Console.WriteLine("❌ Lỗi Parse JSON từ AI."); }
                    }
                }

                // --- CỬA CHẶN CUỐI CÙNG: FALLBACK ĐÃ ĐƯỢC SỬA LỖI ĐỊNH DẠNG ---
                Console.WriteLine("🆘 Tất cả AI đều tạch. Trả về Quiz mặc định an toàn.");
                return Ok(BuildFallbackResponse(name, major, subjectList));
            }
            catch (Exception ex) { return StatusCode(500, new { error = "Lỗi hệ thống nghiêm trọng: " + ex.Message }); }
        }

        // 2. ENDPOINT: NỘP BÀI QUIZ
        // 2. ENDPOINT: NỘP BÀI QUIZ (Đã nâng cấp theo chuẩn phản hồi GPA)
        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmission res)
        {
            try
            {
                if (res == null || string.IsNullOrEmpty(res.StudentId))
                    return BadRequest(new { error = "Dữ liệu đầu vào trống!" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == res.StudentId);
                if (student == null) return BadRequest(new { error = "StudentId không hợp lệ!" });

                bool isFail = res.Score <= 2;
                double gpaDelta = 0.0;
                double oldGpa = student.Gpa;

                if (!isFail)
                {
                    gpaDelta = 0.05; // Theo quy định mới: +0.05 GPA khi pass quiz
                    student.Gpa = Math.Min(4.0, student.Gpa + gpaDelta); // Giới hạn tối đa là 4.0
                    await _context.SaveChangesAsync();
                }

                Console.WriteLine($"📝 [QUIZ] Sinh viên {student.StudentId} nộp bài: {res.Score}/5đ. GPA cũ: {oldGpa} -> GPA mới: {student.Gpa}");

                // Trả đầy đủ thông số về cho Unity cập nhật HUD giao diện
                return Ok(new
                {
                    success = true,
                    breakSafeZone = isFail,
                    finalScore = res.Score,
                    gpaDelta = gpaDelta,
                    newGpa = Math.Round(student.Gpa, 2), // Làm tròn 2 chữ số thập phân cho đẹp HUD
                    message = isFail ? "Quiz failed. GPA unchanged." : $"Quiz passed. GPA increased by {gpaDelta}."
                });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // 3. ENDPOINT: HỒI SINH ZOMBIE
        [HttpPost("zombie/respawn")]
        public async Task<IActionResult> RespawnZombie([FromBody] ZombieUpdate model)
        {
            try
            {
                // 1. Kiểm tra dữ liệu đầu vào cơ bản từ Unity gửi lên
                if (model == null)
                    return BadRequest(new { error = "Dữ liệu cập nhật Zombie bị thiếu!" });

                // 2. Tìm con Zombie trong Database để đè tọa độ mới lên
                var npc = await _context.Npcs.FirstOrDefaultAsync(n => n.Id == model.ZombieId);
                if (npc != null)
                {
                    npc.SpawnX = (double)model.NewX;
                    npc.SpawnY = (double)model.NewY;
                    npc.SpawnZ = (double)model.NewZ;

                    // Lưu trực tiếp xuống Supabase
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"🧟 [ZOMBIE] Zombie ID {model.ZombieId} hồi sinh thành công tại vị trí mới.");

                    // 3. CHỈ TRẢ VỀ ĐÚNG "SUCCESS: TRUE" THEO Ý ÔNG (Đỡ mệt đầu, nhẹ băng thông)
                    return Ok(new { success = true });
                }

                return NotFound(new { error = "Không tìm thấy Zombie với ID đã cho!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi hệ thống khi hồi sinh Zombie!", detail = ex.Message });
            }
        }

        // ── 🆕 THÊM ENDPOINT: XỬ LÝ KHI PLAYER BỊ ZOMBIE GIẾT ──────────────────
        public class PlayerDeathRequest
        {
            public string StudentId { get; set; } = string.Empty;
            public string Reason { get; set; } = "ZombieKilled";
        }

        [HttpPost("player-death")]
        public async Task<IActionResult> PlayerDeath([FromBody] PlayerDeathRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.StudentId))
                    return BadRequest(new { error = "Thiếu thông tin StudentId!" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
                if (student == null)
                    return BadRequest(new { error = "Không tìm thấy sinh viên trong hệ thống!" });

                double oldGpa = student.Gpa;
                double gpaDelta = -0.1; // Bị giết: Trừ bớt 0.1 GPA

                // Trừ điểm nhưng không được để GPA tụt xuống dưới 0
                student.Gpa = Math.Max(0.0, student.Gpa + gpaDelta);
                await _context.SaveChangesAsync();

                Console.WriteLine($"💀 [PLAYER DIED] Sinh viên {student.StudentId} bị hạ gục bởi {request.Reason}. GPA: {oldGpa} -> {student.Gpa}");

                return Ok(new
                {
                    success = true,
                    gpaDelta = gpaDelta,
                    newGpa = Math.Round(student.Gpa, 2),
                    message = $"Player died. GPA decreased by {Math.Abs(gpaDelta)}."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi hệ thống khi xử lý Player chết: " + ex.Message });
            }
        }

        private bool IsValidAiResponse(AiResponse? r)
        {
            return r != null && !string.IsNullOrEmpty(r.advice) && r.quiz != null && r.quiz.Count > 0;
        }

        // HÀM DỰ PHÒNG XỊN: Trả về JSON chuẩn cấu trúc, không lo crash game Unity
        private AiResponse BuildFallbackResponse(string name, string major, string subjectList)
        {
            string json = $@"
            {{
                ""advice"": ""Chào {name}, hệ thống AI hiện đang bận. Với sinh viên {major}, bạn nên tập trung nắm vững lý thuyết và thực hành nhiều bài tập về {subjectList}."",
                ""closingQuestion"": ""Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!"",
                ""quiz"": [
                    {{ ""question"": ""Cách tốt nhất để học {subjectList} là gì?"", ""options"": [""Chỉ xem video"", ""Thực hành code/bài tập"", ""Học thuộc lòng"", ""Bỏ qua""], ""answer"": 1 }},
                    {{ ""question"": ""Mục tiêu của việc làm Quiz?"", ""options"": [""Lấy điểm"", ""Củng cố kiến thức"", ""Cho vui"", ""Giết thời gian""], ""answer"": 1 }},
                    {{ ""question"": ""Khi gặp bài khó trong môn {subjectList} bạn nên làm gì?"", ""options"": [""Bỏ luôn"", ""Hỏi AI/Thầy cô"", ""Ngủ"", ""Xóa game""], ""answer"": 1 }},
                    {{ ""question"": ""Việc làm Quiz giúp ích gì cho bạn?"", ""options"": [""Ghi nhớ kiến thức"", ""Kiểm tra trình độ"", ""Tăng GPA"", ""Cả 3 phương án trên""], ""answer"": 3 }},
                    {{ ""question"": ""Bạn có nên học nhóm môn này không?"", ""options"": [""Không bao giờ"", ""Nên, để trao đổi kiến thức"", ""Chỉ để chơi"", ""Tùy hứng""], ""answer"": 1 }}
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
} */
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json.Linq;
using System.IO;

namespace VibeCity_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();
        // 🌟 BƯỚC 3: Thêm marker nhận diện và DTO bổ trợ
        private const string QuizJsonMarker = "<<<QUIZ_JSON>>>";

        public AiController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // --- DTOs ĐẦU VÀO ---
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

        public class PlayerDeathRequest
        {
            public string StudentId { get; set; } = string.Empty;
            public string Reason { get; set; } = "ZombieKilled";
        }

        // 1. ENDPOINT: TƯ VẤN VÀ TẠO QUIZ - Yêu cầu xác thực JWT
        [Authorize]
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

                var apiKey1 = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"] ?? "";
                var apiOpenRouter = Environment.GetEnvironmentVariable("OpenRouter_API_Key") ?? _configuration["OpenRouter_API_Key"];
                var groqKey = Environment.GetEnvironmentVariable("Groq_API_Key") ?? _configuration["Groq_API_Key"];

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. GPA: {student.Gpa:0.00}. " +
                                $"Hãy tư vấn ngắn gọn về môn: {subjectList}. " +
                                "Yêu cầu trả về JSON THUẦN (không kèm text khác) với cấu trúc: " +
                                "{ " +
                                "\"advice\": \"Lời khuyên chuyên sâu...\", " +
                                "\"closingQuestion\": \"Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!\", " +
                                "\"quiz\": [ { \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 } ] " +
                                "} " +
                                "Tạo đúng 5 câu hỏi trắc nghiệm liên quan đến nội dung trong lời khuyên.";

                string rawText = "";

                try
                {
                    if (!string.IsNullOrEmpty(apiKey1))
                    {
                        var client = new GenerativeModel(apiKey1, "gemini-2.5-flash");
                        var res = await client.GenerateContentAsync(prompt);
                        rawText = res?.Text ?? "";

                        if (rawText.Contains("error") || rawText.Contains("quota") || rawText.Contains("429") || !rawText.Contains("{"))
                        {
                            Console.WriteLine("⚠️ Gemini gặp sự cố Quota hoặc lỗi định dạng. Kích hoạt dự phòng...");
                            rawText = "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Gemini Error]: {ex.Message}");
                    rawText = "";
                }

                if (string.IsNullOrWhiteSpace(rawText) || !rawText.Contains("{"))
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

                if (!string.IsNullOrEmpty(rawText))
                {
                    var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        try
                        {
                            var resultObj = JsonConvert.DeserializeObject<AiResponse>(match.Value);
                            if (IsValidAiResponse(resultObj)) return Ok(resultObj);
                        }
                        catch { Console.WriteLine("❌ Lỗi Parse JSON từ AI."); }
                    }
                }

                Console.WriteLine("🆘 Tất cả AI đều tạch. Trả về Quiz mặc định an toàn.");
                return Ok(BuildFallbackResponse(name, major, subjectList));
            }
            catch (Exception ex) { return StatusCode(500, new { error = "Lỗi hệ thống nghiêm trọng: " + ex.Message }); }
        }
        private async Task WriteSseEventAsync(string eventName, object payload)
        {
            string json = JsonConvert.SerializeObject(payload);

            // Định dạng chuẩn cấu trúc SSE của hệ thống mạng
            await Response.WriteAsync($"event: {eventName}\n");
            await Response.WriteAsync($"data: {json}\n\n");

            // Flush lập tức đẩy dữ liệu ra đường truyền mạng, không cho hệ thống giữ lại bộ đệm
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        // 🌟 BƯỚC 5: Thêm bộ làm sạch ký tự lạ Text khuyên bảo (Unicode safe)
        private static string CleanAdviceText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            string normalized = input.Normalize(NormalizationForm.FormC);
            var output = new StringBuilder(normalized.Length);

            foreach (char character in normalized)
            {
                if (character == '\n' || character == '\r' || character == '\t')
                {
                    output.Append(character);
                    continue;
                }

                if (char.IsControl(character))
                    continue;

                if (character == '<' || character == '>')
                    continue; // Chống lỗi parse ký tự định dạng thẻ TextMeshPro

                output.Append(character);
            }

            return output.ToString();
        }

        // 🌟 BƯỚC 6: Thêm Endpoint Streaming Mới
        [Authorize]
        [HttpPost("consult-stream")]
        public async Task GetAiAdviceStream([FromBody] AiConsultRequest request)
        {
            // 1. Kiểm tra tính hợp lệ của dữ liệu đầu vào trước khi mở Stream
            if (request == null || string.IsNullOrWhiteSpace(request.StudentId) || request.Subjects == null || request.Subjects.Count == 0)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                Response.ContentType = "application/json; charset=utf-8";
                await Response.WriteAsync(JsonConvert.SerializeObject(new { error = "Dữ liệu đầu vào không hợp lệ." }));
                return;
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
            if (student == null)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                Response.ContentType = "application/json; charset=utf-8";
                await Response.WriteAsync(JsonConvert.SerializeObject(new { error = "Không tìm thấy sinh viên." }));
                return;
            }

            string subjectList = string.Join(", ", request.Subjects.Where(sub => !string.IsNullOrWhiteSpace(sub)).Select(sub => sub.Trim()));
            if (string.IsNullOrWhiteSpace(subjectList))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                Response.ContentType = "application/json; charset=utf-8";
                await Response.WriteAsync(JsonConvert.SerializeObject(new { error = "Chủ đề không hợp lệ." }));
                return;
            }

            string apiKey = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"] ?? string.Empty;

            // 2. Thiết lập Header mở cấu hình luồng dữ liệu Server-Sent Events (SSE)
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache, no-transform";
            Response.Headers["X-Accel-Buffering"] = "no";

            // Tắt bộ nhớ đệm IIS/Kestrel để dữ liệu đi thời gian thực
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            // Báo cho Unity biết Server đang xử lý
            await WriteSseEventAsync("status", new { status = "thinking" });

            var pendingText = new StringBuilder();
            var fullAdvice = new StringBuilder();
            var quizJsonText = new StringBuilder();
            bool quizSectionStarted = false;

            // Hàm cục bộ đẩy text tư vấn về Client
            async Task EmitAdviceAsync(string rawText)
            {
                string cleaned = CleanAdviceText(rawText);
                if (string.IsNullOrEmpty(cleaned)) return;

                fullAdvice.Append(cleaned);
                await WriteSseEventAsync("advice", new StreamAdviceEvent { text = cleaned });
            }

            // Hàm cục bộ bóc tách phân tích dữ liệu Chunk đổ về từ Gemini
            async Task ProcessGeminiChunkAsync(string newText, bool flushRemaining)
            {
                if (quizSectionStarted)
                {
                    if (!string.IsNullOrEmpty(newText))
                        quizJsonText.Append(newText);
                    return;
                }

                if (!string.IsNullOrEmpty(newText))
                    pendingText.Append(newText);

                string current = pendingText.ToString();
                int markerIndex = current.IndexOf(QuizJsonMarker, StringComparison.Ordinal);

                // Phát hiện marker chuyển đổi từ lời khuyên sang Quiz
                if (markerIndex >= 0)
                {
                    string advicePart = current.Substring(0, markerIndex);
                    await EmitAdviceAsync(advicePart);

                    string quizPart = current.Substring(markerIndex + QuizJsonMarker.Length);
                    quizJsonText.Append(quizPart);

                    pendingText.Clear();
                    quizSectionStarted = true;
                    return;
                }

                int keepCharacters = flushRemaining ? 0 : QuizJsonMarker.Length - 1;
                int safeCharacterCount = Math.Max(0, pendingText.Length - keepCharacters);

                if (safeCharacterCount <= 0) return;

                string safeAdvice = pendingText.ToString(0, safeCharacterCount);
                pendingText.Remove(0, safeCharacterCount);

                await EmitAdviceAsync(safeAdvice);
            }

            AiResponse? finalResult = null;

            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("Gemini API key is missing.");

                // Cấu hình Prompt đồng bộ hệ thống định dạng chặt chẽ
                string prompt = $"System Prompt và thông tin Sinh viên: {student.FullName}, ngành {student.Major}, GPA {student.Gpa:0.00}. " +
                                $"Hãy tư vấn và giải thích rõ ràng về chủ đề: {subjectList}. " +
                                $"\n\nSau khi viết xong lời khuyên, hãy ghi chính xác marker sau trên một dòng riêng: {QuizJsonMarker}\n" +
                                $"Ngay sau marker, chỉ trả về JSON thuần chứa đúng 5 câu hỏi Quiz cấu trúc: {{ \"closingQuestion\": \"...\", \"quiz\": [ {{ \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 }} ] }}";

                // Sử dụng endpoint alt=sse của Gemini 2.5 Flash
                string geminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse";

                var geminiBody = new
                {
                    contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                    generationConfig = new { temperature = 0.5, maxOutputTokens = 2500 }
                };

                using var geminiRequest = new HttpRequestMessage(HttpMethod.Post, geminiUrl);
                geminiRequest.Headers.Add("x-goog-api-key", apiKey);
                geminiRequest.Content = new StringContent(JsonConvert.SerializeObject(geminiBody), Encoding.UTF8, "application/json");

                using HttpResponseMessage geminiResponse = await _httpClient.SendAsync(geminiRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);

                if (!geminiResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Gemini trả về lỗi HTTP: {(int)geminiResponse.StatusCode}");

                await using Stream responseStream = await geminiResponse.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(responseStream, Encoding.UTF8);

                while (!reader.EndOfStream && !HttpContext.RequestAborted.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
                        continue;

                    string packetJson = line.Substring("data:".Length).Trim();
                    if (string.IsNullOrWhiteSpace(packetJson) || packetJson == "[DONE]")
                        continue;

                    try
                    {
                        JObject packet = JObject.Parse(packetJson);
                        string generatedText = string.Concat(packet.SelectTokens("candidates[0].content.parts[*].text").Select(t => t.Value<string>() ?? string.Empty));

                        if (!string.IsNullOrEmpty(generatedText))
                        {
                            await ProcessGeminiChunkAsync(generatedText, flushRemaining: false);
                        }
                    }
                    catch (JsonException)
                    {
                        // Bỏ qua lỗi phân tích cục bộ dòng trống
                    }
                }

                // Xử lý nốt ký tự cuối cùng
                await ProcessGeminiChunkAsync(string.Empty, flushRemaining: true);

                // 3. Tiến hành bốc tách khối JSON Quiz ở cuối luồng để gửi nguyên khối
                if (quizSectionStarted)
                {
                    Match jsonMatch = Regex.Match(quizJsonText.ToString(), @"\{.*\}", RegexOptions.Singleline);
                    if (jsonMatch.Success)
                    {
                        AiResponse? parsedResult = JsonConvert.DeserializeObject<AiResponse>(jsonMatch.Value);
                        if (parsedResult?.quiz != null && parsedResult.quiz.Count == 5)
                        {
                            parsedResult.advice = fullAdvice.ToString().Trim();
                            finalResult = parsedResult;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AI Stream] Người chơi ngắt kết nối mạng hoặc tắt UI.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AI Stream Error] {ex.Message}");
            }

            // Cơ chế dự phòng cứu nguy hệ thống (Fallback Mechanism) nếu lỗi cấu trúc JSON
            if (finalResult == null)
            {
                AiResponse fallback = BuildFallbackResponse(student.FullName, student.Major, subjectList);
                if (fullAdvice.Length == 0)
                {
                    await EmitAdviceAsync(fallback.advice ?? string.Empty);
                }
                fallback.advice = fullAdvice.Length > 0 ? fullAdvice.ToString().Trim() : fallback.advice;
                finalResult = fallback;
            }

            // Gửi cục data nguyên khối chứa cấu trúc thông tin Quiz
            await WriteSseEventAsync("result", finalResult);

            // Thông báo kết thúc kết nối stream
            await WriteSseEventAsync("done", new { success = true });
        }

        // Hàm sinh Quiz dự phòng khi lỗi mạng hoặc AI trả về sai định dạng
        private AiResponse BuildFallbackResponse(string name, string major, string subject)
        {
            return new AiResponse
            {
                advice = $"[Hệ thống] Xin chào {name}, hiện tại đường truyền học tập môn {subject} đang nghẽn nhẹ. Bạn hãy thực hiện bài ôn tập trắc nghiệm dưới đây nhé!",
                closingQuestion = "Bấm gửi để làm lại Quiz mới nhé!",
                quiz = new List<QuizItem>
                {
                    new QuizItem { question = $"Câu hỏi ôn tập kiến thức cơ bản về: {subject}?", options = new List<string>{"Đáp án A đúng","Đáp án B","Đáp án C","Đáp án D"}, answer = 0 },
                    new QuizItem { question = "Chọn khẳng định đúng trong thiết kế hệ thống?", options = new List<string>{"Sai","Đúng","Không rõ","Tùy trường hợp"}, answer = 1 },
                    new QuizItem { question = "Dữ liệu được lưu trữ an toàn ở đâu?", options = new List<string>{"Client","Không lưu","Database Server","Bộ nhớ tạm"}, answer = 2 },
                    new QuizItem { question = "Thuật toán xử lý tối ưu luồng mạng?", options = new List<string>{"Không tối ưu","Dùng bộ đệm","Xử lý Streaming SSE","Tải lại trang"}, answer = 2 },
                    new QuizItem { question = "Mục tiêu tối ưu trải nghiệm người chơi là gì?", options = new List<string>{"Tăng độ trễ","Game mượt mà hơn","Đồ họa nặng hơn","Tăng chi phí"}, answer = 1 }
                }
            };
        }
    

        // 2. ENDPOINT: NỘP BÀI QUIZ - Yêu cầu xác thực JWT
        [Authorize]
        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmission res)
        {
            try
            {
                if (res == null || string.IsNullOrEmpty(res.StudentId))
                    return BadRequest(new { error = "Dữ liệu đầu vào trống!" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == res.StudentId);
                if (student == null) return BadRequest(new { error = "StudentId không hợp lệ!" });

                bool isFail = res.Score <= 2;
                double gpaDelta = 0.0;
                double oldGpa = student.Gpa;

                if (!isFail)
                {
                    gpaDelta = 0.05;
                    student.Gpa = Math.Min(4.0, student.Gpa + gpaDelta);
                    await _context.SaveChangesAsync();
                }

                Console.WriteLine($"📝 [QUIZ] Sinh viên {student.StudentId} nộp bài: {res.Score}/5đ. GPA cũ: {oldGpa} -> GPA mới: {student.Gpa}");

                return Ok(new
                {
                    success = true,
                    breakSafeZone = isFail,
                    finalScore = res.Score,
                    gpaDelta = gpaDelta,
                    newGpa = Math.Round(student.Gpa, 2),
                    message = isFail ? "Quiz failed. GPA unchanged." : $"Quiz passed. GPA increased by {gpaDelta}."
                });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // 3. ENDPOINT: HỒI SINH ZOMBIE - Yêu cầu xác thực JWT
        [Authorize]
        [HttpPost("zombie/respawn")]
        public async Task<IActionResult> RespawnZombie([FromBody] ZombieUpdate model)
        {
            try
            {
                if (model == null)
                    return BadRequest(new { error = "Dữ liệu cập nhật Zombie bị thiếu!" });

                var npc = await _context.Npcs.FirstOrDefaultAsync(n => n.Id == model.ZombieId);
                if (npc != null)
                {
                    npc.SpawnX = (double)model.NewX;
                    npc.SpawnY = (double)model.NewY;
                    npc.SpawnZ = (double)model.NewZ;

                    await _context.SaveChangesAsync();
                    Console.WriteLine($"🧟 [ZOMBIE] Zombie ID {model.ZombieId} hồi sinh thành công tại vị trí mới.");

                    return Ok(new { success = true });
                }

                return NotFound(new { error = "Không tìm thấy Zombie với ID đã cho!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi hệ thống khi hồi sinh Zombie!", detail = ex.Message });
            }
        }

        // 4. ENDPOINT: XỬ LÝ KHI PLAYER BỊ ZOMBIE GIẾT - Yêu cầu xác thực JWT
        [Authorize]
        [HttpPost("player-death")]
        public async Task<IActionResult> PlayerDeath([FromBody] PlayerDeathRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.StudentId))
                    return BadRequest(new { error = "Thiếu thông tin StudentId!" });

                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
                if (student == null)
                    return BadRequest(new { error = "Không tìm thấy sinh viên trong hệ thống!" });

                double oldGpa = student.Gpa;
                double gpaDelta = -0.1;

                student.Gpa = Math.Max(0.0, student.Gpa + gpaDelta);
                await _context.SaveChangesAsync();

                Console.WriteLine($"💀 [PLAYER DIED] Sinh viên {student.StudentId} bị hạ gục bởi {request.Reason}. GPA: {oldGpa} -> {student.Gpa}");

                return Ok(new
                {
                    success = true,
                    gpaDelta = gpaDelta,
                    newGpa = Math.Round(student.Gpa, 2),
                    message = $"Player died. GPA decreased by {Math.Abs(gpaDelta)}."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi hệ thống khi xử lý Player chết: " + ex.Message });
            }
        }

        private bool IsValidAiResponse(AiResponse? r)
        {
            return r != null && !string.IsNullOrEmpty(r.advice) && r.quiz != null && r.quiz.Count > 0;
        }

        private AiResponse BuildFallbackResponse(string name, string major, string subjectList)
        {
            return new AiResponse
            {
                advice = $"Chào {name}, hệ thống AI hiện đang bận. Với sinh viên {major}, bạn nên tập trung nắm vững lý thuyết và thực hành nhiều bài tập về {subjectList}.",
                closingQuestion = "Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!",
                quiz = new List<QuizQuestion> // Đổi từ QuizItem thành QuizQuestion ở đây
        {
            new QuizQuestion { question = $"Cách tốt nhất để học tốt môn {subjectList} là gì?", options = new List<string>{"Chỉ xem video", "Thực hành bài tập", "Học thuộc lòng", "Bỏ qua"}, answer = 1 },
            new QuizQuestion { question = "Mục tiêu của việc làm Quiz là gì?", options = new List<string>{"Lấy điểm", "Củng cố kiến thức", "Cho vui", "Giết thời gian"}, answer = 1 },
            new QuizQuestion { question = $"Khi gặp bài khó trong môn {subjectList} bạn nên làm gì?", options = new List<string>{"Bỏ luôn", "Hỏi AI hoặc Thầy cô", "Ngủ", "Xóa game"}, answer = 1 },
            new QuizQuestion { question = "Việc làm Quiz giúp ích gì cho bạn?", options = new List<string>{"Ghi nhớ kiến thức", "Kiểm tra trình độ", "Tăng GPA", "Cả 3 phương án trên"}, answer = 3 },
            new QuizQuestion { question = "Bạn có nên học nhóm môn này không?", options = new List<string>{"Không bao giờ", "Nên, để trao đổi kiến thức", "Chỉ để chơi", "Tùy hứng"}, answer = 1 }
        }
            };
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