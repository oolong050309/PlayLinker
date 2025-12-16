using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlayLinker.Models.DTOs;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PlayLinker.Services;

/// <summary>
/// AI 服务接口定义
/// </summary>
public interface IAiService
{
    /// <summary>
    /// 分析用户游戏偏好
    /// </summary>
    Task<AnalyzePreferenceResponseDto> AnalyzeUserPreferencesAsync(int userId, List<string> recentGames);

    /// <summary>
    /// 预测游戏价格趋势
    /// </summary>
    Task<PricePredictionDto> PredictPriceAsync(long gameId, List<PriceHistoryDto> history);
}

/// <summary>
/// AI 服务实现类 (支持阿里云 DashScope / OpenAI 兼容接口)
/// </summary>
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

    /// <summary>
    /// 调用 AI 提供商的通用方法
    /// </summary>
    /// <returns>返回清洗后的 JSON 字符串，如果调用失败或配置无效则返回 null</returns>
    private async Task<string?> CallAiProviderAsync(string systemPrompt, string userPrompt)
    {
        var apiKey = _configuration["AISettings:ApiKey"];
        // 默认使用阿里云百炼兼容端点
        var endpoint = _configuration["AISettings:Endpoint"]?.TrimEnd('/') ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["AISettings:Model"] ?? "qwen-max";

        // 1. 检查 API Key 是否有效 (如果为空或包含占位符，直接返回 null 触发模拟数据)
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("YOUR_KEY") || apiKey.Length < 10)
        {
            _logger.LogWarning("AI API Key 未配置或无效，将使用模拟数据。");
            return null;
        }

        // 2. 构建请求体 (OpenAI Chat Completions 格式)
        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.5 // 控制随机性
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody, jsonOptions), Encoding.UTF8, "application/json");
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try 
        {
            // 3. 发送 HTTP POST 请求
            var response = await _httpClient.PostAsync($"{endpoint}/chat/completions", requestContent);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError($"AI 请求失败 Status: {response.StatusCode}, Body: {errorBody}");
                return null; // 调用失败返回 null
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            
            // 解析 content 内容
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            return CleanJsonString(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Service 调用异常，转为模拟模式");
            return null;
        }
    }

    /// <summary>
    /// 清洗 AI 返回的 JSON (去除 Markdown 代码块标记)
    /// </summary>
    private string CleanJsonString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        // 移除 ```json 和 ``` 标记
        return raw.Replace("```json", "").Replace("```", "").Trim();
    }

    /// <summary>
    /// 实现：分析用户偏好
    /// </summary>
    public async Task<AnalyzePreferenceResponseDto> AnalyzeUserPreferencesAsync(int userId, List<string> recentGames)
    {
        if (recentGames == null || !recentGames.Any())
        {
            return new AnalyzePreferenceResponseDto 
            { 
                AnalyzedGames = 0, 
                Recommendations = new List<string> { "暂无游戏数据，无法进行分析。" } 
            };
        }

        // 定义 Prompt，强制输出 JSON
        var systemPrompt = "你是一个专业的游戏数据分析师。请分析用户的游戏列表，并返回严格的 JSON 格式数据。不要输出 Markdown。返回格式示例：{\"topGenres\": [{\"genre\": \"RPG\", \"confidence\": 0.9}, {\"genre\": \"Action\", \"confidence\": 0.8}], \"analysisText\": \"用户偏好... \", \"recommendations\": [\"建议1\", \"建议2\"]}";
        var userPrompt = $"用户最近玩过的游戏列表：{string.Join(", ", recentGames)}。请分析其游戏偏好题材、风格，并给出3条购买建议。";

        // 尝试调用 AI
        var jsonResponse = await CallAiProviderAsync(systemPrompt, userPrompt);

        // 如果 AI 不可用，使用模拟数据
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return GetMockPreferenceAnalysis(recentGames.Count);
        }

        try
        {
            // 反序列化真实 AI 结果
            var result = JsonSerializer.Deserialize<AiPreferenceResult>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new AnalyzePreferenceResponseDto
            {
                AnalyzedGames = recentGames.Count,
                AnalyzedPeriod = "All Time (AI Powered)",
                DetectedPreferences = new 
                { 
                    TopGenres = result?.TopGenres ?? new(),
                    Analysis = result?.AnalysisText ?? "AI 分析生成中..."
                },
                Recommendations = result?.Recommendations ?? new List<string>(){"AI 分析生成中..."}
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 响应解析失败，使用模拟数据。");
            return GetMockPreferenceAnalysis(recentGames.Count);
        }
    }

    /// <summary>
    /// 获取偏好分析的模拟数据 (Fallback)
    /// </summary>
    private AnalyzePreferenceResponseDto GetMockPreferenceAnalysis(int count)
    {
        return new AnalyzePreferenceResponseDto
        {
            AnalyzedGames = count,
            AnalyzedPeriod = "近6个月 (模拟数据)",
            DetectedPreferences = new 
            { 
                TopGenres = new[] 
                { 
                    new { Genre = "Action", Confidence = 0.85 }, 
                    new { Genre = "RPG", Confidence = 0.75 } 
                },
                Analysis = "由于 AI 服务暂不可用，这是基于规则生成的模拟分析。用户似乎喜欢快节奏的动作游戏。"
            },
            Recommendations = new List<string> 
            { 
                "推荐尝试：黑神话：悟空 (热门动作)", 
                "推荐尝试：艾尔登法环 (开放世界RPG)",
                "推荐尝试：赛博朋克 2077"
            }
        };
    }

    /// <summary>
    /// 实现：预测价格
    /// </summary>
    public async Task<PricePredictionDto> PredictPriceAsync(long gameId, List<PriceHistoryDto> history)
    {
        if (history == null || history.Count < 2)
        {
            return new PricePredictionDto { Probability = 0.5, Reasoning = "数据不足，无法准确预测。" };
        }

        // 格式化历史数据供 AI 阅读
        var historyStr = string.Join("\n", history.OrderBy(h => h.Date).Select(h => $"{h.Date:yyyy-MM-dd}: ￥{h.CurrentPrice}"));
        
        var systemPrompt = "你是一个游戏市场价格预测专家。根据价格历史，预测下一次打折的时间和概率。返回严格 JSON，无 Markdown。示例：{\"probability\": 0.85, \"estimatedDate\": \"2024-12-25\", \"reasoning\": \"根据历史记录...\"}";
        var userPrompt = $"游戏ID: {gameId}。\n价格历史记录：\n{historyStr}\n\n请分析打折规律，预测下一次打折。";

        var jsonResponse = await CallAiProviderAsync(systemPrompt, userPrompt);

        if (string.IsNullOrEmpty(jsonResponse))
        {
            return GetMockPricePrediction();
        }

        try
        {
            var result = JsonSerializer.Deserialize<AiPriceResult>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new PricePredictionDto
            {
                Probability = result?.Probability ?? 0.5,
                EstimatedDate = result?.EstimatedDate ?? "未知",
                Reasoning = result?.Reasoning + " (AI 分析)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 价格预测响应解析失败，使用模拟数据。");
            return GetMockPricePrediction();
        }
    }

    /// <summary>
    /// 获取价格预测的模拟数据 (Fallback)
    /// </summary>
    private PricePredictionDto GetMockPricePrediction()
    {
        return new PricePredictionDto
        {
            Probability = 0.75,
            EstimatedDate = System.DateTime.Now.AddDays(15).ToString("yyyy-MM-dd"),
            Reasoning = "AI 服务未连接。根据历史通用规则预测，该游戏可能会在接下来的季节性促销中打折。"
        };
    }

    // --- 内部辅助类：用于反序列化 AI 返回的 JSON ---
    private class AiPreferenceResult
    {
        public List<AiGenreItem> TopGenres { get; set; }
        public string AnalysisText { get; set; }
        public List<string> Recommendations { get; set; }
    }
    private class AiGenreItem { public string Genre { get; set; } public double Confidence { get; set; } }
    private class AiPriceResult { public double Probability { get; set; } public string EstimatedDate { get; set; } public string Reasoning { get; set; } }
}