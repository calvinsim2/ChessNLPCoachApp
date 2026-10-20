using ChessNLPCoachApp.Interfaces;
using System.Text;
using System.Text.Json;

namespace ChessNLPCoachApp.Services
{
    public class LangflowClient : ILangflowClient
    {
        private readonly HttpClient _httpClient;

        private readonly string? _baseUrl;

        private readonly string? _flowId;

        private readonly string? _apiKey;

        public LangflowClient(HttpClient httpClient, string? baseUrl, string? flowId, string? apiKey)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl;
            _flowId = flowId;
            _apiKey = apiKey;
        }

        // =============================================================
        // LANGFLOW
        // =============================================================

        public async Task<string> AskLangFlowAsync(
            string engineAnalysis,
            CancellationToken cancellationToken = default)
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

            string url =
                $"{_baseUrl.TrimEnd('/')}/api/v1/run/{_flowId}";

            object payload = new
            {
                input_value = engineAnalysis,
                input_type = "chat",
                output_type = "chat"
            };

            string json = JsonSerializer.Serialize(payload);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                )
            };

            // Per-request header: the injected HttpClient is shared, so
            // DefaultRequestHeaders must not be mutated.
            request.Headers.Add("x-api-key", _apiKey);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            string? rawResponse =
                await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"LangFlow returned {(int)response.StatusCode}: {rawResponse}"
                );
            }

            using JsonDocument? document =
                JsonDocument.Parse(rawResponse);

            string? text = document
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
