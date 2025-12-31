using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayLinker.Models.DTOs;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;

namespace PlayLinker.Services;

public interface IAiService
{
    Task<AnalyzePreferenceResponseDto> AnalyzeUserPreferencesAsync(int userId, List<string> recentGames);
    Task<PricePredictionDto> PredictPriceAsync(long gameId, List<PriceHistoryDto> history);
    Task<List<AiRecommendationResult>> GetRecommendationsAsync(AiRecommendationContextDto context);
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

    private async Task<string?> CallAiProviderAsync(string systemPrompt, string userPrompt)
    {
        var apiKey = _configuration["AISettings:ApiKey"];
        var endpoint = _configuration["AISettings:Endpoint"]?.TrimEnd('/') ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["AISettings:Model"] ?? "qwen-plus";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("YOUR_KEY")) return null;

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.4
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var content = new StringContent(JsonSerializer.Serialize(requestBody, jsonOptions), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try 
        {
            var response = await _httpClient.PostAsync($"{endpoint}/chat/completions", content);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError($"AI Request Failed: {response.StatusCode} {err}");
                return null;
            }

            var resJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resJson);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                return CleanJsonString(choices[0].GetProperty("message").GetProperty("content").GetString() ?? "");
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Service Call Exception");
            return null;
        }
    }

    // [增强] 提取 JSON 数组核心部分
    private string CleanJsonString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "[]";
        
        // 找到第一个 [ 和最后一个 ]
        int start = raw.IndexOf('[');
        int end = raw.LastIndexOf(']');
        
        if (start != -1 && end != -1 && end > start)
        {
            return raw.Substring(start, end - start + 1);
        }
        
        // 兜底：简单移除 markdown
        return raw.Replace("```json", "").Replace("```", "").Trim();
    }

    public async Task<List<AiRecommendationResult>> GetRecommendationsAsync(AiRecommendationContextDto context)
    {
        // 1. 构建提示词
        var candidatesJson = JsonSerializer.Serialize(context.CandidateGames.Select(g => new { 
            id = g.GameId, 
            name = g.Name, 
            tags = g.Tags.Take(3),
            // score = g.ReviewScore // 暂时移除评分，以免误导 AI (因为现在很多是0)
        }));

        var sensitivityMap = new Dictionary<int, string> { { 1, "高(偏好打折)" }, { 2, "中等" }, { 3, "低(不敏感)" } };
        var userProfileStr = $"偏好时长: {context.PlaytimeRange}, 价格敏感度: {sensitivityMap.GetValueOrDefault(context.PriceSensitivity, "中等")}";
        var recentGamesStr = context.RecentGames.Any() ? string.Join(", ", context.RecentGames.Take(5)) : "暂无";

        var systemPrompt = @"你是一个游戏推荐引擎。根据用户画像和候选游戏列表，选出 3 款游戏。
        要求：
        1. 必须从【候选游戏池】中选择 ID。
        2. 返回 JSON 数组：[{""gameId"": 123, ""reason"": ""推荐理由(30字以内)""}]。
        3. 不要输出 Markdown。";

        var userPrompt = $@"
        【用户画像】：{userProfileStr}
        【最近在玩】：{recentGamesStr}
        【候选游戏池】：
        {candidatesJson}
        
        请输出推荐：";

        // 2. 调用 AI
        var jsonResponse = await CallAiProviderAsync(systemPrompt, userPrompt);

        // 3. 解析结果
        if (!string.IsNullOrEmpty(jsonResponse))
        {
            try
            {
                var results = JsonSerializer.Deserialize<List<AiRecommendationResult>>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (results != null && results.Count > 0) return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse AI response: {Json}", jsonResponse);
            }
        }

        // 4. AI 失败时，返回空列表，由 Controller 决定后续
        return new List<AiRecommendationResult>();
    }

    public async Task<AnalyzePreferenceResponseDto> AnalyzeUserPreferencesAsync(int userId, List<string> recentGames)
    {
        return await Task.FromResult(new AnalyzePreferenceResponseDto 
        { 
            AnalyzedGames = recentGames.Count, 
            Recommendations = new List<string> { "开发中" } 
        });
    }

    public async Task<PricePredictionDto> PredictPriceAsync(long gameId, List<PriceHistoryDto> history)
    {
        return await Task.FromResult(new PricePredictionDto { Probability = 0.5, Reasoning = "开发中" });
    }
}

// DTOs
public class AiRecommendationContextDto
{
    public string PlaytimeRange { get; set; } = "Unknown";
    public int PriceSensitivity { get; set; } = 2;
    public List<string> RecentGames { get; set; } = new();
    public List<GameCandidateDto> CandidateGames { get; set; } = new();
}

public class GameCandidateDto
{
    public long GameId { get; set; }
    public string Name { get; set; } = "";
    public int ReviewScore { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class AiRecommendationResult
{
    public long GameId { get; set; }
    public string Reason { get; set; } = "";
}