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

        public Task<AiResponse> GenerateContentAsync(string prompt)
        {
            var preview = prompt?.Length > 200 ? prompt.Substring(0, 200) + "..." : prompt;
            var text = $"[Simulated response from {_model}. Prompt preview: {preview}]";
            return Task.FromResult(new AiResponse { Text = text });
        }
    }
}
