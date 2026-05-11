using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // Hãy chắc chắn ông đã cài Newtonsoft.Json qua NuGet

namespace Google_GenerativeAI
{
    public class AiResponse
    {
        // Sử dụng string? để giải quyết cảnh báo CS8618
        public string? Text { get; set; }
    }

    public class GenerativeModel
    {
        private readonly string _apiKey;
        private readonly string _model;

        // Sử dụng static HttpClient là chuẩn xác để chạy ổn định trên Render
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
                // Đảm bảo URL sử dụng v1beta hoặc v1 tùy theo key của ông
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

                // SỬA LỖI 400: Định nghĩa cấu trúc Object tường minh thay vì dùng kiểu ẩn danh lồng nhau phức tạp
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Gửi request POST đến Google
                var response = await _client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // In chi tiết "lời mắng" của Google ra console Render để ông dễ soi lỗi
                    Console.WriteLine($"❌ Gemini API Error Details: {responseBody}");
                    return new AiResponse { Text = $"Lỗi API {response.StatusCode}: {responseBody}" };
                }

                // Giải mã JSON kết quả
                dynamic? result = JsonConvert.DeserializeObject(responseBody);

                // Sử dụng toán tử điều kiện null (?) để tránh crash nếu cấu trúc trả về thay đổi
                string aiText = result?.candidates?[0]?.content?.parts?[0]?.text
                                ?? "AI đã nhận lệnh nhưng không có nội dung trả về.";

                return new AiResponse { Text = aiText };
            }
            catch (Exception ex)
            {
                // Bắt các lỗi kết nối mạng hoặc SSL
                Console.WriteLine($"❌ Connection Error: {ex.Message}");
                return new AiResponse { Text = $"Lỗi kết nối hệ thống: {ex.Message}" };
            }
        }
    }
}