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
                var student = await _context.Students.OrderBy(s => s.Id).FirstOrDefaultAsync();
                string name = student?.FullName ?? "Lê Nhật Anh";
                string major = student?.Major ?? "Robot & AI";

                var apiKey1 = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"];
                var apiOpenRouter = Environment.GetEnvironmentVariable("OpenRouter_API_Key") ?? _configuration["OpenRouter_API_Key"];

                string subjectList = (subjects != null && subjects.Count > 0) ? string.Join(", ", subjects) : "các môn chuyên ngành";

                // --- PROMPT SIÊU CHẶT CHẼ ---
                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. Hãy tư vấn ngắn gọn về {subjectList}. " +
                                "Yêu cầu trả về JSON THUẦN (không kèm text khác) với cấu trúc: " +
                                "{ " +
                                "\"advice\": \"Lời khuyên của bạn...\", " +
                                "\"closingQuestion\": \"Nếu không còn thắc mắc, hãy để trống ô nhập và bấm send để làm Quiz nhé!\", " +
                                "\"quiz\": [ { \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 } ] " +
                                "} " +
                                "Tạo đúng 5 câu hỏi trắc nghiệm.";

                string rawText = "";

                // 1. Thử Gemini
                try
                {
                    var client = new GenerativeModel(apiKey1, "gemini-2.5-flash");
                    var response = await client.GenerateContentAsync(prompt);
                    rawText = response?.Text ?? "";
                }
                catch { rawText = ""; }

                // 2. Dự phòng OpenRouter
                if (string.IsNullOrEmpty(rawText) || !rawText.Contains("{") || rawText.Contains("quota"))
                {
                    if (!string.IsNullOrEmpty(apiOpenRouter))
                    {
                        rawText = await CallOpenRouter(apiOpenRouter, prompt);
                    }
                }

                // Parse & Trả về
                var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    var resultObject = JsonConvert.DeserializeObject<AiResponse>(match.Value);
                    if (resultObject != null) return Ok(resultObject);
                }

                return StatusCode(500, new { error = "Hệ thống AI đang bảo trì, vui lòng thử lại sau!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

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