namespace PlayLinker.Models.DTOs;

/// <summary>
/// 新闻列表响应DTO
/// </summary>
public class NewsListDto
{
    public List<NewsItemDto> Items { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
}

/// <summary>
/// 新闻项DTO
/// </summary>
public class NewsItemDto
{
    public long NewsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public long Date { get; set; }
    public string Contents { get; set; } = string.Empty;
    public string? NewsUrl { get; set; }
    public List<RelatedGameDto>? RelatedGames { get; set; }
}

/// <summary>
/// 关联游戏DTO
/// </summary>
public class RelatedGameDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string? HeaderImage { get; set; }
}

/// <summary>
/// 游戏新闻响应DTO
/// </summary>
public class GameNewsDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public List<NewsItemDto> News { get; set; } = new();
    public PaginationMeta Meta { get; set; } = new();
}

/// <summary>
/// 新闻详情响应DTO
/// </summary>
public class NewsDetailDto
{
    public long NewsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public long Date { get; set; }
    public string Contents { get; set; } = string.Empty;
    public string? NewsUrl { get; set; }
    public List<RelatedGameDto> RelatedGames { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int Views { get; set; }
}

/// <summary>
/// Steam新闻同步请求DTO（所有游戏）
/// </summary>
public class SteamNewsSyncAllRequestDto
{
    /// <summary>
    /// 每个游戏获取的新闻数量（默认20）
    /// </summary>
    public int? Count { get; set; } = 20;
}

/// <summary>
/// Steam新闻同步请求DTO（单个游戏）
/// </summary>
public class SteamNewsSyncRequestDto
{
    /// <summary>
    /// 游戏ID
    /// </summary>
    public long GameId { get; set; }

    /// <summary>
    /// 获取的新闻数量（默认20）
    /// </summary>
    public int? Count { get; set; } = 20;
}

/// <summary>
/// Steam新闻同步响应DTO（所有游戏）
/// </summary>
public class SteamNewsSyncResponseDto
{
    public int ProcessedGames { get; set; }
    public int TotalGames { get; set; }
    public int TotalNews { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Steam游戏新闻响应DTO
/// </summary>
public class SteamGameNewsResponseDto
{
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public int AppId { get; set; }
    public List<SteamNewsItemDto> News { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>
/// Steam新闻项DTO
/// </summary>
public class SteamNewsItemDto
{
    public string Gid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsExternalUrl { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Contents { get; set; } = string.Empty;
    public string FeedLabel { get; set; } = string.Empty;
    public long Date { get; set; }
    public string FeedName { get; set; } = string.Empty;
    public int FeedType { get; set; }
    public int AppId { get; set; }
}

