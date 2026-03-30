using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PersonalCloud.Services
{
    public interface ITurnstileService
    {
        Task<bool> VerifyTokenAsync(string? token, string? remoteIp = null);
    }

    public class TurnstileService : ITurnstileService
    {
        private readonly HttpClient _httpClient;
        private readonly string _secretKey;
        private readonly ILogger<TurnstileService> _logger;

        public TurnstileService(HttpClient httpClient, IConfiguration configuration, ILogger<TurnstileService> logger)
        {
            _httpClient = httpClient;
            _secretKey = configuration["Turnstile:SecretKey"] ?? string.Empty;
            _logger = logger;
        }

        public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var requestData = new List<KeyValuePair<string, string>>
            {
                new("secret", _secretKey),
                new("response", token)
            };

            if (!string.IsNullOrEmpty(remoteIp))
            {
                requestData.Add(new KeyValuePair<string, string>("remoteip", remoteIp));
            }

            var response = await _httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", new FormUrlEncodedContent(requestData));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Turnstile verification failed with status code {StatusCode}", response.StatusCode);
                return false;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TurnstileResponse>(jsonResponse);

            return result?.Success ?? false;
        }

        private class TurnstileResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("success")]
            public bool Success { get; set; }
        }
    }
}
