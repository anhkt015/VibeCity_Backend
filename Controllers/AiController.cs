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
        private const string QuizJsonMarker = "<<<QUIZ_JSON>>>";

        public AiController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

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

        public class StreamAdviceEvent
        {
            public string text { get; set; } = string.Empty;
        }

        // =========================================================================
        // CHẾ ĐỘ 2: HỌC NHANH (Dùng Consult thường - Trả về 1 cục Object ăn liền)
        // =========================================================================
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

                var apiKey = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"] ?? "";

                string prompt = $"Tôi là {name}, sinh viên {major} tại HCMUTE. GPA: {student.Gpa:0.00}. " +
                                $"Hãy tư vấn thật ngắn gọn súc tích về môn: {subjectList}. " +
                                "Yêu cầu bắt buộc trả về cấu trúc JSON sau: " +
                                "{ " +
                                "\"advice\": \"Lời khuyên siêu ngắn gọn...\", " +
                                "\"closingQuestion\": \"Quiz đã sẵn sàng. Hãy bấm Bắt đầu trắc nghiệm.\", " +
                                "\"quiz\": [ { \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 } ] " +
                                "} " +
                                "Tạo đúng 5 câu hỏi trắc nghiệm liên quan trực tiếp đến nội dung. Mỗi câu đúng 4 lựa chọn. Answer từ 0 đến 3.";

                string rawText = "";
                if (!string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        var client = new GenerativeModel(apiKey, "gemini-2.5-flash");
                        var res = await client.GenerateContentAsync(prompt);
                        rawText = res?.Text ?? "";
                    }
                    catch (Exception ex) { Console.WriteLine($"[Gemini Consult Error]: {ex.Message}"); }
                }

                if (!string.IsNullOrEmpty(rawText))
                {
                    var match = Regex.Match(rawText, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        try
                        {
                            // Làm sạch chuỗi JSON thô trước khi ép kiểu để chống lỗi ký tự ẩn phá cấu trúc
                            string cleanJson = CleanRawJson(match.Value);
                            var resultObj = JsonConvert.DeserializeObject<AiResponse>(cleanJson);

                            // ĐẢM BẢO ĐỦ 5 CÂU HỎI: Nếu AI trả về thiếu hoặc lỗi danh sách câu hỏi
                            if (resultObj != null)
                            {
                                if (resultObj.quiz == null || resultObj.quiz.Count < 5)
                                {
                                    resultObj.quiz = FillMissingQuestions(resultObj.quiz, subjectList);
                                }
                                return Ok(resultObj);
                            }
                        }
                        catch { }
                    }
                }

                return Ok(BuildFallbackResponse(name, major, subjectList));
            }
            catch (Exception ex) { return StatusCode(500, new { error = "Lỗi hệ thống: " + ex.Message }); }
        }

        // =========================================================================
        // CHẾ ĐỘ 1: HỌC TẬP NGHIÊM TÚC (Dùng Stream chữ chạy - Cập nhật PROMPT MỚI)
        // =========================================================================
        [Authorize]
        [HttpPost("consult-stream")]
        public async Task GetAiAdviceStream([FromBody] AiConsultRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.StudentId) || request.Subjects == null || request.Subjects.Count == 0)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsync(JsonConvert.SerializeObject(new { error = "Đầu vào lỗi." }));
                return;
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == request.StudentId);
            if (student == null) { Response.StatusCode = 400; return; }

            string subjectList = string.Join(", ", request.Subjects.Select(s => s.Trim()));
            string apiKey = Environment.GetEnvironmentVariable("Gemini_API_Key") ?? _configuration["Gemini_API_Key"] ?? string.Empty;

            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache, no-transform";
            Response.Headers["X-Accel-Buffering"] = "no";
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            await WriteSseEventAsync("status", new { status = "thinking" });

            var pendingText = new StringBuilder();
            var fullAdvice = new StringBuilder();
            var quizJsonText = new StringBuilder();
            bool quizSectionStarted = false;

            // Đang ghép tiếp phần Prompt định hướng chuyên sâu và quy chuẩn định dạng nghiêm ngặt của ông
            string prompt = $"System Prompt: Tôi là {student.FullName}, sinh viên {student.Major} tại HCMUTE. GPA {student.Gpa:0.00}.\n" +
                            $"Hãy tư vấn chuyên sâu về chủ đề: {subjectList}. " +
                            $"Ưu tiên trình bày khái niệm cốt lõi, hai điểm cần nhớ và một bước thực hành tiếp theo. " +
                            $"Không lặp lại cùng một ý.\n\n" +
                            $"[YÊU CẦU ĐỊNH DẠNG PHẦN LỜI KHUYÊN]\n" +
                            $"Trả lời bằng ngôn ngữ phù hợp với câu hỏi của người học. " +
                            $"Chỉ dùng văn bản Unicode thuần. " +
                            $"Không dùng Markdown, HTML, XML, thẻ TextMeshPro, khung code, bảng biểu, emoji hoặc ký tự trang trí. " +
                            $"Được phép dùng chữ viết của mọi ngôn ngữ, chữ số, dấu câu và ký hiệu học thuật thông dụng. " +
                            $"Không đưa câu hỏi, lựa chọn hoặc đáp án Quiz vào phần lời khuyên.\n\n" +
                            $"Sau khi hoàn thành lời khuyên, ghi chính xác marker:\n{QuizJsonMarker}\n" +
                            $"Ngay sau marker, chỉ trả về JSON thuần theo cấu trúc: " +
                            $"{{" +
                            $"\"closingQuestion\": \"Quiz đã sẵn sàng. Hãy bấm Bắt đầu trắc nghiệm.\", " +
                            $"\"quiz\": [ {{ \"question\": \"...\", \"options\": [\"A\", \"B\", \"C\", \"D\"], \"answer\": 0 }} ]" +
                            $"}}\n" +
                            $"Tạo đúng 5 câu hỏi liên quan trực tiếp đến nội dung vừa giải thích. Mỗi câu có đúng 4 lựa chọn. " +
                            $"Answer là chỉ số từ 0 đến 3. Không thêm nội dung nào sau JSON.";

            try
            {
                string geminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse";
                var geminiBody = new { contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } } };

                using var geminiRequest = new HttpRequestMessage(HttpMethod.Post, geminiUrl);
                geminiRequest.Headers.Add("x-goog-api-key", apiKey);
                geminiRequest.Content = new StringContent(JsonConvert.SerializeObject(geminiBody), Encoding.UTF8, "application/json");

                using HttpResponseMessage geminiResponse = await _httpClient.SendAsync(geminiRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                await using Stream responseStream = await geminiResponse.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(responseStream, Encoding.UTF8);

                while (!reader.EndOfStream && !HttpContext.RequestAborted.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal)) continue;

                    string packetJson = line.Substring("data:".Length).Trim();
                    if (packetJson == "[DONE]") continue;

                    try
                    {
                        JObject packet = JObject.Parse(packetJson);
                        string generatedText = string.Concat(packet.SelectTokens("candidates[0].content.parts[*].text").Select(t => t.Value<string>() ?? string.Empty));

                        if (!string.IsNullOrEmpty(generatedText))
                        {
                            if (quizSectionStarted)
                            {
                                quizJsonText.Append(generatedText);
                            }
                            else
                            {
                                pendingText.Append(generatedText);
                                string current = pendingText.ToString();
                                int markerIndex = current.IndexOf(QuizJsonMarker, StringComparison.Ordinal);

                                if (markerIndex >= 0)
                                {
                                    string advicePart = current.Substring(0, markerIndex);
                                    string cleanedAdvice = CleanAdviceText(advicePart);
                                    fullAdvice.Append(cleanedAdvice);
                                    await WriteSseEventAsync("advice", new { text = cleanedAdvice });

                                    quizJsonText.Append(current.Substring(markerIndex + QuizJsonMarker.Length));
                                    pendingText.Clear();
                                    quizSectionStarted = true;
                                }
                                else if (pendingText.Length > 100)
                                {
                                    string emitChunk = pendingText.ToString(0, pendingText.Length - 50);
                                    pendingText.Remove(0, pendingText.Length - 50);
                                    string cleanedEmit = CleanAdviceText(emitChunk);
                                    fullAdvice.Append(cleanedEmit);
                                    await WriteSseEventAsync("advice", new { text = cleanedEmit });
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (pendingText.Length > 0 && !quizSectionStarted)
                {
                    string finalAdvice = CleanAdviceText(pendingText.ToString());
                    fullAdvice.Append(finalAdvice);
                    await WriteSseEventAsync("advice", new { text = finalAdvice });
                }

                AiResponse? finalResult = null;
                string rawJson = quizJsonText.ToString();
                var match = Regex.Match(rawJson, @"\{.*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    try
                    {
                        string cleanJson = CleanRawJson(match.Value);
                        finalResult = JsonConvert.DeserializeObject<AiResponse>(cleanJson);
                    }
                    catch { }
                }

                if (finalResult == null)
                {
                    finalResult = new AiResponse { closingQuestion = "Quiz đã sẵn sàng. Hãy bấm Bắt đầu trắc nghiệm." };
                }
                if (finalResult.quiz == null || finalResult.quiz.Count < 5)
                {
                    finalResult.quiz = FillMissingQuestions(finalResult.quiz, subjectList);
                }

                // Đồng bộ hóa chuẩn endpoint Stream theo phản hồi của Unity
                finalResult.advice = fullAdvice.ToString().Trim();

                await WriteSseEventAsync("result", finalResult);
                await WriteSseEventAsync("done", new { success = true });
                await Response.CompleteAsync();
            }
            catch (Exception)
            {
                // Đồng bộ hóa phần catch ném lỗi trả về đúng cấu trúc "result" + "done"
                var fb = BuildFallbackResponse(student.FullName, student.Major, subjectList);
                fb.advice = fullAdvice.Length > 0 ? fullAdvice.ToString().Trim() : fb.advice;

                await WriteSseEventAsync("result", fb);
                await WriteSseEventAsync("done", new { success = true });
                await Response.CompleteAsync();
            }
        }

        // --- CÁC HÀM TRỢ GIÚP (HELPERS) ---
        private static string CleanRawJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "{}";
            return Regex.Replace(json, @"[\x00-\x1F\x7F]", "");
        }

        private List<QuizQuestion> FillMissingQuestions(List<QuizQuestion>? existingQuiz, string subjectList)
        {
            var targetList = existingQuiz ?? new List<QuizQuestion>();
            var pool = new List<QuizQuestion>
            {
                new QuizQuestion { question = $"Kiến thức cốt lõi nhất cần nắm vững khi bắt đầu học tập môn {subjectList} là gì?", options = new List<string>{ "Hiểu rõ các khái niệm lý thuyết nền tảng", "Học thuộc lòng toàn bộ slide", "Bỏ qua các buổi thực hành bài tập", "Chỉ học vẹt trước khi thi một ngày" }, answer = 0 },
                new QuizQuestion { question = $"Điểm cần lưu ý quan trọng để cải thiện hiệu quả tiếp thu kiến thức môn {subjectList} là gì?", options = new List<string>{ "Chờ đợi giảng viên nhắc nhở", "Chủ động kết hợp lý thuyết với các bước thực hành", "Sao chép mã nguồn/bài làm của bạn bè", "Chỉ học phần dễ, bỏ qua phần khó" }, answer = 1 },
                new QuizQuestion { question = $"Khi gặp một khái niệm hoặc bài tập phức tạp chưa rõ trong {subjectList}, hành động nào đúng đắn nhất?", options = new List<string>{ "Bỏ qua và chuyển sang học môn khác", "Tra cứu tài liệu chuyên ngành hoặc thảo luận cùng giảng viên", "Đợi đến khi kiểm tra rồi tính", "Xóa ứng dụng học tập" }, answer = 1 },
                new QuizQuestion { question = $"Để cải thiện chỉ số đánh giá năng lực (GPA) tổng thể đối với môn {subjectList}, sinh viên nên làm gì?", options = new List<string>{ "Chỉ cần tham gia điểm danh đầy đủ", "Hoàn thành tốt tất cả các bài kiểm tra đánh giá định kỳ", "Học tùy hứng không có kế hoạch", "Không cần làm bài tập về nhà" }, answer = 1 },
                new QuizQuestion { question = $"Lợi ích thực tế lớn nhất của việc giải quyết các câu hỏi trắc nghiệm (Quiz) về {subjectList} là gì?", options = new List<string>{ "Giết thời gian rảnh rỗi", "Đánh giá lại lỗ hổng kiến thức để kịp thời bổ sung", "Tăng tương tác ảo trên hệ thống", "Tích điểm thưởng game đơn thuần" }, answer = 1 }
            };

            int poolIndex = 0;
            while (targetList.Count < 5 && poolIndex < pool.Count)
            {
                var q = pool[poolIndex];
                targetList.Add(q);
                poolIndex++;
            }

            foreach (var item in targetList)
            {
                if (item.options == null || item.options.Count != 4)
                {
                    item.options = new List<string> { "Phương án A", "Phương án B", "Phương án C", "Phương án D" };
                }
                if (item.answer < 0 || item.answer > 3) item.answer = 0;
            }

            return targetList.Take(5).ToList();
        }

        private AiResponse BuildFallbackResponse(string name, string major, string subjectList)
        {
            var fallback = new AiResponse
            {
                advice = $"Chào {name}, sinh viên {major} tại HCMUTE. Hãy tập trung ôn tập lý thuyết và làm bài tập thực hành môn {subjectList}.",
                closingQuestion = "Quiz đã sẵn sàng. Hãy bấm Bắt đầu trắc nghiệm."
            };
            fallback.quiz = FillMissingQuestions(null, subjectList);
            return fallback;
        }

        private async Task WriteSseEventAsync(string eventName, object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            await Response.WriteAsync($"event: {eventName}\n");
            await Response.WriteAsync($"data: {json}\n\n");
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }

        private static string CleanAdviceText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string normalized = input.Normalize(NormalizationForm.FormC);
            var output = new StringBuilder(normalized.Length);
            foreach (char character in normalized)
            {
                if (character == '\n' || character == '\r' || character == '\t') { output.Append(character); continue; }
                if (char.IsControl(character)) continue; // Giữ lại '<' và '>' để Unity hiển thị bình thường
                output.Append(character);
            }
            return output.ToString();
        }

        private bool IsValidAiResponse(AiResponse? r)
        {
            return r != null && !string.IsNullOrEmpty(r.advice) && r.quiz != null && r.quiz.Count > 0;
        }

        // =========================================================================
        // CÁC ENDPOINT QUẢN LÝ GAME KHÁC (GIỮ NGUYÊN)
        // =========================================================================
        [Authorize]
        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmission res)
        {
            try
            {
                if (res == null || string.IsNullOrEmpty(res.StudentId)) return BadRequest();
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentId == res.StudentId);
                if (student == null) return BadRequest();
                if (res.Score > 2) { student.Gpa = Math.Min(4.0, student.Gpa + 0.05); await _context.SaveChangesAsync(); }
                return Ok(new { success = true, finalScore = res.Score, newGpa = Math.Round(student.Gpa, 2) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
    }
}