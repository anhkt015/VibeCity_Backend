using System;
using System.Threading.Tasks;

namespace Google_GenerativeAI
{
    public class AiResponse
    {
        public string Text { get; set; }
    }

    // Minimal local stub to satisfy compile-time references to GenerativeModel.
    // Replace with a real client integration when ready (official SDK or HTTP client).
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
            using (var client = new System.Net.Http.HttpClient())
            {
                // URL gọi API của Gemini
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

                // Cấu trúc dữ liệu gửi đi (JSON)
                var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Gửi request
                var response = await client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Trích xuất lấy đoạn text trả về từ Google
                dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
                string aiText = result.candidates[0].content.parts[0].text;

                return new AiResponse { Text = aiText };
            }
        }
    }
}
