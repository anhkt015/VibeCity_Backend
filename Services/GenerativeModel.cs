using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // Phải có dòng này để hết lỗi CS0103

namespace Google_GenerativeAI
{
    public class AiResponse
    {
        public string? Text { get; set; } // Thêm dấu ? để hết cảnh báo CS8618
    }

    public class GenerativeModel
    {
        private readonly string _apiKey;
        private readonly string _model;

        public GenerativeModel(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public async Task<AiResponse> GenerateContentAsync(string prompt)
        {
            using (var client = new HttpClient())
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

                var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

                // Sửa lỗi Newtonsoft bằng cách gọi trực tiếp JsonConvert
                string json = JsonConvert.SerializeObject(payload);

                // Sửa lỗi CS1503: Cú pháp chuẩn cho StringContent
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new AiResponse { Text = $"Lỗi API: {response.StatusCode} - {responseBody}" };
                }

                dynamic? result = JsonConvert.DeserializeObject(responseBody);
                string aiText = result?.candidates[0]?.content?.parts[0]?.text ?? "AI không có phản hồi.";

                return new AiResponse { Text = aiText };
            }
        }
    }
}