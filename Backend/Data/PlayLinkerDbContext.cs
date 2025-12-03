using Microsoft.EntityFrameworkCore;
using PlayLinker.Models.Entities;

namespace PlayLinker.Data;

/// <summary>
/// PlayLinker数据库上下文
/// 管理所有数据库实体和数据访问逻辑
/// </summary>
public class PlayLinkerDbContext : DbContext
{
    public PlayLinkerDbContext(DbContextOptions<PlayLinkerDbContext> options) : base(options)
    {
    }

    // 游戏相关表
    public DbSet<Game> Games { get; set; }
    public DbSet<GameRanking> GameRankings { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<GameGenre> GameGenres { get; set; }
    public DbSet<Developer> Developers { get; set; }
    public DbSet<GameDeveloper> GameDevelopers { get; set; }
    public DbSet<Publisher> Publishers { get; set; }
    public DbSet<GamePublisher> GamePublishers { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<GameCategory> GameCategories { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<GameLanguage> GameLanguages { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<GamePlatform> GamePlatforms { get; set; }

    // 成就相关表
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }

    // 新闻相关表
    public DbSet<News> News { get; set; }
    public DbSet<GameNews> GameNews { get; set; }

    // 用户游戏库相关表
    public DbSet<UserGameLibrary> UserGameLibraries { get; set; }
    public DbSet<UserPlatformLibrary> UserPlatformLibraries { get; set; }
    public DbSet<PlayerPlatform> PlayerPlatforms { get; set; }
    public DbSet<UserPlatformBinding> UserPlatformBindings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置UserPlatformLibrary复合主键
        modelBuilder.Entity<UserPlatformLibrary>()
            .HasKey(upl => new { upl.PlatformUserId, upl.PlatformId, upl.GameId });

        // 配置PlayerPlatform复合主键
        modelBuilder.Entity<PlayerPlatform>()
            .HasKey(pp => new { pp.PlatformUserId, pp.PlatformId });

        // 配置GamePlatform复合主键
        modelBuilder.Entity<GamePlatform>()
            .HasKey(gp => new { gp.GameId, gp.PlatformId });

        // 配置UserGameLibrary主键（非自增）
        modelBuilder.Entity<UserGameLibrary>()
            .HasKey(ugl => ugl.UserId);
        
        modelBuilder.Entity<UserGameLibrary>()
            .Property(ugl => ugl.UserId)
            .ValueGeneratedNever(); // 明确指定不是自增字段

        // 配置索引
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.Name);

        modelBuilder.Entity<Game>()
            .HasIndex(g => g.ReleaseDate);

        modelBuilder.Entity<GameRanking>()
            .HasIndex(gr => gr.CurrentRank);

        modelBuilder.Entity<News>()
            .HasIndex(n => n.Date);

        modelBuilder.Entity<UserPlatformLibrary>()
            .HasIndex(upl => upl.LastPlayed);

        // 配置唯一约束
        modelBuilder.Entity<GameGenre>()
            .HasIndex(gg => new { gg.GameId, gg.GenreId })
            .IsUnique();

        modelBuilder.Entity<GameDeveloper>()
            .HasIndex(gd => new { gd.GameId, gd.DeveloperId })
            .IsUnique();

        modelBuilder.Entity<GamePublisher>()
            .HasIndex(gp => new { gp.GameId, gp.PublisherId })
            .IsUnique();

        modelBuilder.Entity<GameCategory>()
            .HasIndex(gc => new { gc.GameId, gc.CategoryId })
            .IsUnique();

        modelBuilder.Entity<GameLanguage>()
            .HasIndex(gl => new { gl.GameId, gl.LanguageId })
            .IsUnique();

        modelBuilder.Entity<GameNews>()
            .HasIndex(gn => new { gn.GameId, gn.NewsId })
            .IsUnique();

        modelBuilder.Entity<UserAchievement>()
            .HasIndex(ua => new { ua.UserId, ua.AchievementId, ua.PlatformId })
            .IsUnique();
    }
}

