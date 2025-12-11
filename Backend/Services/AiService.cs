using Microsoft.Extensions.Configuration;
using PlayLinker.Models.DTOs;
using System.Text;
using System.Text.Json;

namespace PlayLinker.Services;

public interface IAiService
{
    Task<AnalyzePreferenceResponseDto> AnalyzeUserPreferencesAsync(int userId, List<string> recentGames);
    Task<PricePredictionDto> PredictPriceAsync(long gameId, List<PriceHistoryDto> history);
}

public class AiService : IAiService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiService> _logger;

    public AiService(IConfiguration configuration, HttpClient httpClient, ILogger<AiService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    // 这是一个通用的调用 AI 接口的辅助方法
    private async Task<string> CallAiProviderAsync(string prompt)
    {
        var apiKey = _configuration["AISettings:ApiKey"];
        var endpoint = _configuration["AISettings:Endpoint"] ?? "https://api.openai.com/v1";
        var model = _configuration["AISettings:Model"] ?? "gpt-3.5-turbo";

        // 如果没有配置 Key，返回模拟数据（方便测试）
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_AI_API_KEY_HERE")
        {
            _logger.LogWarning("AI API Key未配置，返回模拟数据。");
            return "MOCK_RESPONSE";
        }

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.7
        };

        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        try 
        {
            var response = await _httpClient.PostAsync($"{endpoint}/chat/completions", requestContent);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"AI Call Failed: {error}");
                return "ERROR";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Service Exception");
            return "ERROR";
        }
    }

    public async Task<AnalyzePreferenceResponseDto> AnalyzeUserPreferencesAsync(int userId, List<string> recentGames)
    {
        // 构建提示词
        var prompt = $"分析以下游戏列表用户的偏好: {string.Join(", ", recentGames)}。请返回JSON格式，包含topGenres(数组)和description。";
        
        var aiResponse = await CallAiProviderAsync(prompt);

        if (aiResponse == "MOCK_RESPONSE" || aiResponse == "ERROR")
        {
            return new AnalyzePreferenceResponseDto
            {
                AnalyzedGames = recentGames.Count,
                AnalyzedPeriod = "Last 6 Months",
                DetectedPreferences = new { Style = "Action/RPG", Tags = new[] { "Open World", "Story Rich" } },
                Recommendations = new List<string> { "基于您的历史，建议关注即将发售的3A大作。", "您似乎喜欢高难度的挑战。" }
            };
        }

        // 这里简化处理，实际应解析 aiResponse JSON
        return new AnalyzePreferenceResponseDto
        {
            AnalyzedGames = recentGames.Count,
            Recommendations = new List<string> { "AI分析完成: " + aiResponse.Substring(0, Math.Min(50, aiResponse.Length)) + "..." }
        };
    }

    public async Task<PricePredictionDto> PredictPriceAsync(long gameId, List<PriceHistoryDto> history)
    {
        var prompt = $"基于以下价格历史(日期:价格): {string.Join(", ", history.Select(h => $"{h.Date}:{h.CurrentPrice}"))}。预测下一次打折的时间和概率。";
        
        var aiResponse = await CallAiProviderAsync(prompt);

        if (aiResponse == "MOCK_RESPONSE" || aiResponse == "ERROR")
        {
            return new PricePredictionDto
            {
                Probability = 0.85,
                EstimatedDate = DateTime.Now.AddDays(15).ToString("yyyy-MM-dd"),
                Reasoning = "基于历史夏促和冬促的规律，预计近期会有折扣。"
            };
        }

        return new PricePredictionDto
        {
            Probability = 0.7,
            EstimatedDate = "Unknown",
            Reasoning = aiResponse
        };
    }
}