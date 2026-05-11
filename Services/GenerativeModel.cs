using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // Đảm bảo ông đã cài gói NuGet Newtonsoft.Json nhé

namespace Google_GenerativeAI
{
    public class AiResponse
    {
        // Thêm dấu ? để xử lý cảnh báo CS8618 (Non-nullable property)
        public string? Text { get; set; }
    }

    public class GenerativeModel
    {
        private readonly string _apiKey;
        private readonly string _model;
        // Dùng static HttpClient để tránh lỗi "Socket Exhaustion" khi chạy trên server
        private static readonly HttpClient _client = new HttpClient();

        public GenerativeModel(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public async Task<AiResponse> GenerateContentAsync(string prompt)
        {
            try
            {
                // URL chuẩn của Google Gemini API
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

                // Payload theo đúng cấu trúc JSON mà Gemini yêu cầu
                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Gửi request POST
                var response = await _client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Trả về lỗi chi tiết để ông dễ debug trên Unity
                    return new AiResponse { Text = $"Lỗi API ({response.StatusCode}): {responseBody}" };
                }

                // Parse JSON để lấy nội dung phản hồi từ AI
                dynamic? result = JsonConvert.DeserializeObject(responseBody);
                string aiText = result?.candidates?[0]?.content?.parts?[0]?.text ?? "AI không có phản hồi.";

                return new AiResponse { Text = aiText };
            }
            catch (Exception ex)
            {
                return new AiResponse { Text = $"Lỗi kết nối: {ex.Message}" };
            }
        }
    }
}