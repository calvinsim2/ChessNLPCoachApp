using ChessNLPCoachApp.Interfaces;
using System.Text;
using System.Text.Json;

namespace ChessNLPCoachApp.Services
{
    public class LangflowClient : ILangflowClient
    {
        private readonly string? _baseUrl;

        private readonly string? _flowId;

        private readonly string? _apiKey;

        public LangflowClient(string? baseUrl, string? flowId, string? apiKey)
        {
            _baseUrl = baseUrl;
            _flowId = flowId;
            _apiKey = apiKey;
        }

        // =============================================================
        // LANGFLOW
        // =============================================================

        public async Task<string> AskLangFlowAsync(
            string engineAnalysis)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                throw new InvalidOperationException(
                    "LANGFLOW_BASE_URL has not been configured in the .env file.");
            }

            if (string.IsNullOrWhiteSpace(_flowId))
            {
                throw new InvalidOperationException(
                    "LANGFLOW_FLOW_ID has not been configured in the .env file.");
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException(
                    "LANGFLOW_API_KEY has not been configured in the .env file.");
            }

            using var httpClient = new HttpClient();

            httpClient.Timeout = TimeSpan.FromMinutes(5);

            httpClient.DefaultRequestHeaders.Add(
                "x-api-key",
                _apiKey
            );

            var url =
                $"{_baseUrl.TrimEnd('/')}/api/v1/run/{_flowId}";

            var payload = new
            {
                input_value = engineAnalysis,
                input_type = "chat",
                output_type = "chat"
            };

            var json = JsonSerializer.Serialize(payload);

            using var requestContent = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(
                url,
                requestContent
            );

            var rawResponse =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"LangFlow returned {(int)response.StatusCode}: {rawResponse}"
                );
            }

            using var document =
                JsonDocument.Parse(rawResponse);

            var text = document
                .RootElement
                .GetProperty("outputs")[0]
                .GetProperty("outputs")[0]
                .GetProperty("results")
                .GetProperty("message")
                .GetProperty("text")
                .GetString();

            return text ?? rawResponse;
        }

    }
}
