using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlayLinker.Data;
using PlayLinker.Models.DTOs;

namespace PlayLinker.Services;

/// <summary>
/// Mod 浏览服务实现
/// 聚合多个 Mod 平台的 API
/// </summary>
public class ModExploreService : IModExploreService
{
    private readonly PlayLinkerDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModExploreService> _logger;
    private readonly IConfiguration _configuration;

    public ModExploreService(
        PlayLinkerDbContext context,
        HttpClient httpClient,
        ILogger<ModExploreService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ModExploreResponse> GetModsAsync(ModExploreRequest request)
    {
        // 获取游戏的 Mod 平台映射
        var mapping = await _context.Set<PlayLinker.Models.Entities.GameModSource>()
            .Include(x => x.Game)
            .FirstOrDefaultAsync(x => x.GameId == request.GameId && x.Source == request.Source);

        if (mapping == null)
        {
            return new ModExploreResponse
            {
                Mods = new List<ExploreModItemDto>(),
                Total = 0,
                Page = request.Page,
                PageSize = request.PageSize,
                Source = request.Source,
                GameName = "Unknown"
            };
        }

        return request.Source.ToLower() switch
        {
            "nexusmods" => await GetNexusModsAsync(mapping, request),
            "3dm" => await Get3DMModsAsync(mapping, request),
            "gamebanana" => await GetGameBananaModsAsync(mapping, request),
            _ => new ModExploreResponse { Source = request.Source }
        };
    }

    public async Task<ExploreModDetailDto?> GetModDetailAsync(string source, string modId, string? domain = null)
    {
        return source.ToLower() switch
        {
            "nexusmods" => await GetNexusModDetailAsync(modId, domain ?? ""),
            "3dm" => await Get3DMModDetailAsync(modId),
            _ => null
        };
    }

    public async Task<GameModSourceDto?> GetGameModSourcesAsync(long gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return null;

        var sources = await _context.Set<PlayLinker.Models.Entities.GameModSource>()
            .Where(x => x.GameId == gameId)
            .ToListAsync();

        return new GameModSourceDto
        {
            GameId = gameId,
            GameName = game.Name,
            Sources = sources.Select(s => new ModSourceInfo
            {
                Source = s.Source,
                ExternalGameId = s.ExternalGameId,
                ExternalDomain = s.ExternalDomain,
                DisplayName = GetSourceDisplayName(s.Source),
                IconUrl = GetSourceIconUrl(s.Source)
            }).ToList()
        };
    }

    public async Task<ModExploreResponse> SearchModsAsync(string source, string query, string? domain = null, int page = 1)
    {
        return source.ToLower() switch
        {
            "nexusmods" => await SearchNexusModsAsync(query, domain ?? "", page),
            "3dm" => await Search3DMModsAsync(query, page),
            _ => new ModExploreResponse { Source = source }
        };
    }

    #region NexusMods API

    private async Task<ModExploreResponse> GetNexusModsAsync(
        PlayLinker.Models.Entities.GameModSource mapping, 
        ModExploreRequest request)
    {
        try
        {
            var apiKey = _configuration["ModSources:NexusMods:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("NexusMods API key not configured");
                return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
            }

            var domain = mapping.ExternalDomain ?? "";
            var url = $"https://api.nexusmods.com/v1/games/{domain}/mods/latest_added.json";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", apiKey);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("NexusMods API error: {StatusCode}", response.StatusCode);
                return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
            }

            var json = await response.Content.ReadAsStringAsync();
            var mods = JsonSerializer.Deserialize<List<NexusModItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new ModExploreResponse
            {
                Mods = mods?.Select(m => new ExploreModItemDto
                {
                    ModId = m.ModId.ToString(),
                    Name = m.Name ?? "",
                    Summary = m.Summary ?? "",
                    Author = m.Author ?? "",
                    Version = m.Version ?? "",
                    ThumbnailUrl = m.PictureUrl ?? "",
                    ModPageUrl = $"https://www.nexusmods.com/{domain}/mods/{m.ModId}",
                    Downloads = m.ModDownloads,
                    Endorsements = m.EndorsementCount,
                    UpdatedAt = m.UpdatedTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(m.UpdatedTimestamp.Value).DateTime : null,
                    CreatedAt = m.CreatedTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(m.CreatedTimestamp.Value).DateTime : null,
                    Source = "NexusMods",
                    Category = m.CategoryId.ToString(),
                    AdultContent = m.ContainsAdultContent
                }).ToList() ?? new List<ExploreModItemDto>(),
                Total = mods?.Count ?? 0,
                Page = request.Page,
                PageSize = request.PageSize,
                Source = "NexusMods",
                GameName = mapping.Game?.Name ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NexusMods");
            return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
        }
    }

    private async Task<ExploreModDetailDto?> GetNexusModDetailAsync(string modId, string domain)
    {
        try
        {
            var apiKey = _configuration["ModSources:NexusMods:ApiKey"];
            if (string.IsNullOrEmpty(apiKey)) return null;

            var url = $"https://api.nexusmods.com/v1/games/{domain}/mods/{modId}.json";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", apiKey);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var mod = JsonSerializer.Deserialize<NexusModDetail>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mod == null) return null;

            return new ExploreModDetailDto
            {
                ModId = mod.ModId.ToString(),
                Name = mod.Name ?? "",
                Summary = mod.Summary ?? "",
                Description = mod.Description ?? "",
                Author = mod.Author ?? "",
                Version = mod.Version ?? "",
                ThumbnailUrl = mod.PictureUrl ?? "",
                ModPageUrl = $"https://www.nexusmods.com/{domain}/mods/{mod.ModId}",
                Downloads = mod.ModDownloads,
                Endorsements = mod.EndorsementCount,
                UpdatedAt = mod.UpdatedTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(mod.UpdatedTimestamp.Value).DateTime : null,
                Source = "NexusMods",
                AdultContent = mod.ContainsAdultContent
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NexusMods detail");
            return null;
        }
    }

    private async Task<ModExploreResponse> SearchNexusModsAsync(string query, string domain, int page)
    {
        // NexusMods 搜索需要使用不同的 API 端点
        // 这里简化处理，实际需要使用 GraphQL API
        return new ModExploreResponse
        {
            Source = "NexusMods",
            Mods = new List<ExploreModItemDto>(),
            Total = 0,
            Page = page
        };
    }

    #endregion

    #region 3DM API (v3)

    private async Task<ModExploreResponse> Get3DMModsAsync(
        PlayLinker.Models.Entities.GameModSource mapping, 
        ModExploreRequest request)
    {
        try
        {
            var apiKey = _configuration["ModSources:3DM:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("3DM API key not configured");
                return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
            }

            var gameId = mapping.ExternalGameId;
            // 3DM v3 API
            var url = $"https://mod.3dmgame.com/api/v3/mods?gameId={gameId}&page={request.Page}&pageSize={request.PageSize}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("authorization", apiKey);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("3DM API error: {StatusCode}", response.StatusCode);
                return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<_3DMV3ApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Success != true || result.Data?.Data == null)
            {
                return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
            }

            return new ModExploreResponse
            {
                Mods = result.Data.Data.Select(m => new ExploreModItemDto
                {
                    ModId = m.Id.ToString(),
                    Name = m.Mods_title ?? "",
                    Summary = m.Mods_desc ?? "",
                    Author = m.User_nickName ?? m.Mods_author ?? "",
                    Version = m.Mods_version ?? "",
                    ThumbnailUrl = !string.IsNullOrEmpty(m.Mods_image_url) 
                        ? (m.Mods_image_url.StartsWith("http") ? m.Mods_image_url : $"https://assets-mod.3dmgame.com{m.Mods_image_url}")
                        : "",
                    ModPageUrl = $"https://mod.3dmgame.com/mod/{m.Id}",
                    Downloads = m.Mods_download_cnt,
                    Endorsements = m.Mods_mark_cnt,
                    Source = "3DM",
                    Category = m.Mods_type_name ?? ""
                }).ToList(),
                Total = result.Data.Count,
                Page = request.Page,
                PageSize = request.PageSize,
                Source = "3DM",
                GameName = mapping.Game?.Name ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching 3DM mods");
            return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
        }
    }

    private async Task<ExploreModDetailDto?> Get3DMModDetailAsync(string modId)
    {
        try
        {
            var apiKey = _configuration["ModSources:3DM:ApiKey"];
            if (string.IsNullOrEmpty(apiKey)) return null;

            var url = $"https://mod.3dmgame.com/api/v3/mods/{modId}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("authorization", apiKey);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<_3DMV3ModDetailResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var mod = result?.Data;
            if (mod == null) return null;

            return new ExploreModDetailDto
            {
                ModId = mod.Id.ToString(),
                Name = mod.Mods_title ?? "",
                Summary = mod.Mods_desc ?? "",
                Description = mod.Mods_content ?? "",
                Author = mod.User_nickName ?? mod.Mods_author ?? "",
                Version = mod.Mods_version ?? "",
                ThumbnailUrl = !string.IsNullOrEmpty(mod.Mods_image_url)
                    ? (mod.Mods_image_url.StartsWith("http") ? mod.Mods_image_url : $"https://assets-mod.3dmgame.com{mod.Mods_image_url}")
                    : "",
                ModPageUrl = $"https://mod.3dmgame.com/mod/{mod.Id}",
                Downloads = mod.Mods_download_cnt,
                Source = "3DM"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching 3DM mod detail");
            return null;
        }
    }

    private async Task<ModExploreResponse> Search3DMModsAsync(string query, int page)
    {
        try
        {
            var apiKey = _configuration["ModSources:3DM:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return new ModExploreResponse { Source = "3DM" };
            }

            var url = $"https://mod.3dmgame.com/api/v3/mods?search={Uri.EscapeDataString(query)}&page={page}&pageSize=20";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("authorization", apiKey);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new ModExploreResponse { Source = "3DM" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<_3DMV3ApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Success != true || result.Data?.Data == null)
            {
                return new ModExploreResponse { Source = "3DM" };
            }

            return new ModExploreResponse
            {
                Mods = result.Data.Data.Select(m => new ExploreModItemDto
                {
                    ModId = m.Id.ToString(),
                    Name = m.Mods_title ?? "",
                    Summary = m.Mods_desc ?? "",
                    Author = m.User_nickName ?? m.Mods_author ?? "",
                    ThumbnailUrl = !string.IsNullOrEmpty(m.Mods_image_url)
                        ? (m.Mods_image_url.StartsWith("http") ? m.Mods_image_url : $"https://assets-mod.3dmgame.com{m.Mods_image_url}")
                        : "",
                    ModPageUrl = $"https://mod.3dmgame.com/mod/{m.Id}",
                    Downloads = m.Mods_download_cnt,
                    Source = "3DM"
                }).ToList(),
                Total = result.Data.Count,
                Page = page,
                Source = "3DM"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching 3DM mods");
            return new ModExploreResponse { Source = "3DM" };
        }
    }

    #endregion

    #region GameBanana API

    private async Task<ModExploreResponse> GetGameBananaModsAsync(
        PlayLinker.Models.Entities.GameModSource mapping, 
        ModExploreRequest request)
    {
        try
        {
            var gameId = mapping.ExternalGameId;
            var url = $"https://gamebanana.com/apiv11/Game/{gameId}/Subfeed?_nPage={request.Page}&_nPerpage={request.PageSize}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GameBananaResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new ModExploreResponse
            {
                Mods = result?.Records?.Select(m => new ExploreModItemDto
                {
                    ModId = m.IdRow.ToString(),
                    Name = m.Name ?? "",
                    Summary = "",
                    Author = m.Submitter?.Name ?? "",
                    ThumbnailUrl = m.PreviewMedia?.Images?.FirstOrDefault()?.BaseUrl ?? "",
                    ModPageUrl = m.ProfileUrl ?? "",
                    Downloads = m.DownloadCount,
                    Endorsements = m.LikeCount,
                    Source = "GameBanana"
                }).ToList() ?? new List<ExploreModItemDto>(),
                Total = result?.Metadata?.RecordCount ?? 0,
                Page = request.Page,
                PageSize = request.PageSize,
                Source = "GameBanana",
                GameName = mapping.Game?.Name ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GameBanana mods");
            return CreateEmptyResponse(request, mapping.Game?.Name ?? "");
        }
    }

    #endregion

    #region Helper Methods

    private static ModExploreResponse CreateEmptyResponse(ModExploreRequest request, string gameName)
    {
        return new ModExploreResponse
        {
            Mods = new List<ExploreModItemDto>(),
            Total = 0,
            Page = request.Page,
            PageSize = request.PageSize,
            Source = request.Source,
            GameName = gameName
        };
    }

    private static string GetSourceDisplayName(string source)
    {
        return source.ToLower() switch
        {
            "nexusmods" => "Nexus Mods",
            "3dm" => "3DM Mod站",
            "gamebanana" => "GameBanana",
            "steam" => "Steam 创意工坊",
            _ => source
        };
    }

    private static string GetSourceIconUrl(string source)
    {
        return source.ToLower() switch
        {
            "nexusmods" => "https://www.nexusmods.com/favicon.ico",
            "3dm" => "https://mod.3dmgame.com/favicon.ico",
            "gamebanana" => "https://gamebanana.com/favicon.ico",
            "steam" => "https://store.steampowered.com/favicon.ico",
            _ => ""
        };
    }

    #endregion
}

#region API Response Models

// NexusMods - 使用 JsonPropertyName 确保正确映射
public class NexusModItem
{
    [System.Text.Json.Serialization.JsonPropertyName("mod_id")]
    public int ModId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("author")]
    public string? Author { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public string? Version { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("picture_url")]
    public string? PictureUrl { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("mod_downloads")]
    public int ModDownloads { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("endorsement_count")]
    public int EndorsementCount { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("category_id")]
    public int CategoryId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("domain_name")]
    public string? DomainName { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("updated_timestamp")]
    public long? UpdatedTimestamp { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("created_timestamp")]
    public long? CreatedTimestamp { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("contains_adult_content")]
    public bool ContainsAdultContent { get; set; }
}

public class NexusModDetail : NexusModItem
{
    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; set; }
}

// 3DM v3 API
public class _3DMV3ApiResponse
{
    public bool Success { get; set; }
    public _3DMV3DataWrapper? Data { get; set; }
}

public class _3DMV3DataWrapper
{
    public List<_3DMModItem>? Data { get; set; }
    public int Count { get; set; }
}

public class _3DMV3ModDetailResponse
{
    public bool Success { get; set; }
    public _3DMModItem? Data { get; set; }
}

// 3DM (保留兼容)
public class _3DMApiResponse
{
    public List<_3DMModItem>? Data { get; set; }
    public int Total { get; set; }
}

public class _3DMModDetailResponse
{
    public _3DMModItem? Data { get; set; }
}

public class _3DMModItem
{
    public int Id { get; set; }
    
    // 标题和描述
    public string? Mods_title { get; set; }
    public string? Mods_desc { get; set; }
    public string? Mods_content { get; set; }
    
    // 图片
    public string? Mods_image_url { get; set; }
    
    // 版本和作者
    public string? Mods_version { get; set; }
    public string? User_nickName { get; set; }
    public string? Mods_author { get; set; }
    
    // 分类
    public string? Mods_type_name { get; set; }
    public int? Mods_type_id { get; set; }
    
    // 统计数据
    public int Mods_download_cnt { get; set; }
    public int Mods_mark_cnt { get; set; }
    public int Mods_click_cnt { get; set; }
    public int Mods_collection_cnt { get; set; }
    
    // 时间
    public string? Mods_createTime { get; set; }
    public string? Mods_updateTime { get; set; }
    
    // 兼容旧字段名（以防 API 返回不同格式）
    public string? ModsTitle { get => Mods_title; set => Mods_title = value; }
    public string? ModsDesc { get => Mods_desc; set => Mods_desc = value; }
    public string? ModsContent { get => Mods_content; set => Mods_content = value; }
    public string? ModsImageUrl { get => Mods_image_url; set => Mods_image_url = value; }
    public string? ModsVersion { get => Mods_version; set => Mods_version = value; }
    public string? UserNickName { get => User_nickName; set => User_nickName = value; }
    public string? ModsTypeName { get => Mods_type_name; set => Mods_type_name = value; }
    public int ModsDownloadCnt { get => Mods_download_cnt; set => Mods_download_cnt = value; }
    public int ModsMarkCnt { get => Mods_mark_cnt; set => Mods_mark_cnt = value; }
}

// GameBanana
public class GameBananaResponse
{
    public List<GameBananaRecord>? Records { get; set; }
    public GameBananaMetadata? Metadata { get; set; }
}

public class GameBananaMetadata
{
    public int RecordCount { get; set; }
}

public class GameBananaRecord
{
    public int IdRow { get; set; }
    public string? Name { get; set; }
    public string? ProfileUrl { get; set; }
    public int DownloadCount { get; set; }
    public int LikeCount { get; set; }
    public GameBananaSubmitter? Submitter { get; set; }
    public GameBananaPreviewMedia? PreviewMedia { get; set; }
}

public class GameBananaSubmitter
{
    public string? Name { get; set; }
}

public class GameBananaPreviewMedia
{
    public List<GameBananaImage>? Images { get; set; }
}

public class GameBananaImage
{
    public string? BaseUrl { get; set; }
    public string? File { get; set; }
}

#endregion
