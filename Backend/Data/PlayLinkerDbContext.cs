using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;
using PlayLinker.Models.Entities;
namespace PlayLinker.Data;

public partial class PlayLinkerDbContext : DbContext
{
    public PlayLinkerDbContext()
    {
    }

    public PlayLinkerDbContext(DbContextOptions<PlayLinkerDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CloudSaveBackup> CloudSaveBackups { get; set; }

    public virtual DbSet<Developer> Developers { get; set; }

    public virtual DbSet<ExternalLink> ExternalLinks { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<GameCategory> GameCategories { get; set; }

    public virtual DbSet<GameDeveloper> GameDevelopers { get; set; }

    public virtual DbSet<GameExternalLink> GameExternalLinks { get; set; }

    public virtual DbSet<GameGenre> GameGenres { get; set; }

    public virtual DbSet<GameLanguage> GameLanguages { get; set; }

    public virtual DbSet<GameNews> GameNews { get; set; }

    public virtual DbSet<GamePlatform> GamePlatforms { get; set; }

    public virtual DbSet<GamePublisher> GamePublishers { get; set; }

    public virtual DbSet<GameRanking> GameRankings { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Language> Languages { get; set; }

    public virtual DbSet<LocalGameInstall> LocalGameInstalls { get; set; }

    public virtual DbSet<LocalMod> LocalMods { get; set; }

    public virtual DbSet<LocalSaveFile> LocalSaveFiles { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<NotificationCenter> NotificationCenters { get; set; }

    public virtual DbSet<ParentalAlertLog> ParentalAlertLogs { get; set; }

    public virtual DbSet<ParentalControlRelationship> ParentalControlRelationships { get; set; }

    public virtual DbSet<ParentalControlRule> ParentalControlRules { get; set; }

    public virtual DbSet<Platform> Platforms { get; set; }

    public virtual DbSet<PlayerPlatform> PlayerPlatforms { get; set; }

    public virtual DbSet<PreferenceGenre> PreferenceGenres { get; set; }

    public virtual DbSet<PriceAlertLog> PriceAlertLogs { get; set; }

    public virtual DbSet<PriceAlertSubscription> PriceAlertSubscriptions { get; set; }

    public virtual DbSet<PriceHistory> PriceHistories { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<Recommendation> Recommendations { get; set; }

    public virtual DbSet<RecommendationFeedback> RecommendationFeedbacks { get; set; }

    public virtual DbSet<ReportGenerationRecord> ReportGenerationRecords { get; set; }

    public virtual DbSet<ReportTemplate> ReportTemplates { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAchievement> UserAchievements { get; set; }

    public virtual DbSet<UserGameLibrary> UserGameLibraries { get; set; }

    public virtual DbSet<UserPlatformBinding> UserPlatformBindings { get; set; }

    public virtual DbSet<UserPlatformLibrary> UserPlatformLibraries { get; set; }

    public virtual DbSet<UserPreference> UserPreferences { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=114.55.115.211;database=playlinker_db;user=root;password=123456", Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.44-mysql"));

    // D模块：用户偏好与推荐
    public DbSet<UserPreference> UserPreferences { get; set; }
    public DbSet<PreferenceGenre> PreferenceGenres { get; set; }
    public DbSet<Recommendation> Recommendations { get; set; }
    public DbSet<RecommendationFeedback> RecommendationFeedbacks { get; set; }

    // D模块：价格监控与愿望单
    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<PriceAlertSubscription> PriceAlertSubscriptions { get; set; }
    public DbSet<PriceAlertLog> PriceAlertLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.AchievementId).HasName("PRIMARY");

            entity.ToTable("achievements", tb => tb.HasComment("成就表"));

            entity.Property(e => e.Hidden).HasComment("0=不隐藏，1=隐藏");
            entity.Property(e => e.IconLocked).HasComment("未解锁状态/灰色");
            entity.Property(e => e.IconUnlocked).HasComment("解锁状态");

            entity.HasOne(d => d.Game).WithMany(p => p.Achievements).HasConstraintName("achievements_ibfk_1");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PRIMARY");

            entity.ToTable("categories", tb => tb.HasComment("分类/标签词表"));
        });

        modelBuilder.Entity<CloudSaveBackup>(entity =>
        {
            entity.HasKey(e => e.CloudBackupId).HasName("PRIMARY");

            entity.ToTable("cloud_save_backup", tb => tb.HasComment("云端备份存档"));

            entity.Property(e => e.FileSize).HasComment("大小MB");
            entity.Property(e => e.UploadTime).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.CloudSaveBackups)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("cloud_save_backup_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.CloudSaveBackups)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("cloud_save_backup_ibfk_1");
        });

        modelBuilder.Entity<Developer>(entity =>
        {
            entity.HasKey(e => e.DeveloperId).HasName("PRIMARY");

            entity.ToTable("developers", tb => tb.HasComment("游戏开发商"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ExternalLink>(entity =>
        {
            entity.HasKey(e => e.LinkId).HasName("PRIMARY");

<<<<<<< HEAD
            entity.ToTable("external_links", tb => tb.HasComment("第三方攻略/外链源"));

            entity.Property(e => e.Source).HasComment("bilibili,youtube等");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.GameId).HasName("PRIMARY");

            entity.ToTable("games", tb => tb.HasComment("游戏主表"));

            entity.Property(e => e.IsFree).HasComment("1代表免费，0代表不免费");
            entity.Property(e => e.ReviewScoreDesc).HasDefaultValueSql("''");
        });

        modelBuilder.Entity<GameCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_categories", tb => tb.HasComment("游戏与分类关联表"));

            entity.HasOne(d => d.Category).WithMany(p => p.GameCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_categories_ibfk_2");

            entity.HasOne(d => d.Game).WithMany(p => p.GameCategories).HasConstraintName("game_categories_ibfk_1");
        });

        modelBuilder.Entity<GameDeveloper>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_developers", tb => tb.HasComment("游戏与开发商关联表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Developer).WithMany(p => p.GameDevelopers).HasConstraintName("game_developers_ibfk_2");

            entity.HasOne(d => d.Game).WithMany(p => p.GameDevelopers).HasConstraintName("game_developers_ibfk_1");
        });

        modelBuilder.Entity<GameExternalLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_external_links", tb => tb.HasComment("游戏与外链关联表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.GameExternalLinks).HasConstraintName("game_external_links_ibfk_1");

            entity.HasOne(d => d.Link).WithMany(p => p.GameExternalLinks)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_external_links_ibfk_2");
        });

        modelBuilder.Entity<GameGenre>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_genres", tb => tb.HasComment("游戏与题材关联表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.GameGenres).HasConstraintName("game_genres_ibfk_1");

            entity.HasOne(d => d.Genre).WithMany(p => p.GameGenres)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_genres_ibfk_2");
        });

        modelBuilder.Entity<GameLanguage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_languages", tb => tb.HasComment("游戏语言关联表"));

            entity.Property(e => e.Notes).HasComment("比如 是否有简体中文");

            entity.HasOne(d => d.Game).WithMany(p => p.GameLanguages).HasConstraintName("game_languages_ibfk_1");

            entity.HasOne(d => d.Language).WithMany(p => p.GameLanguages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_languages_ibfk_2");
        });

        modelBuilder.Entity<GameNews>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_news", tb => tb.HasComment("游戏与新闻关联表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.GameNews).HasConstraintName("game_news_ibfk_1");

            entity.HasOne(d => d.News).WithMany(p => p.GameNews).HasConstraintName("game_news_ibfk_2");
        });

        modelBuilder.Entity<GamePlatform>(entity =>
        {
            entity.HasKey(e => new { e.GameId, e.PlatformId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("game_platform", tb => tb.HasComment("游戏在平台的映射"));

            entity.Property(e => e.PlatformGameId).HasComment("平台内部标识");

            entity.HasOne(d => d.Game).WithMany(p => p.GamePlatforms).HasConstraintName("game_platform_ibfk_1");

            entity.HasOne(d => d.Platform).WithMany(p => p.GamePlatforms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_platform_ibfk_2");
        });

        modelBuilder.Entity<GamePublisher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("game_publishers", tb => tb.HasComment("游戏与发行商关联表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.GamePublishers).HasConstraintName("game_publishers_ibfk_1");

            entity.HasOne(d => d.Publisher).WithMany(p => p.GamePublishers).HasConstraintName("game_publishers_ibfk_2");
        });

        modelBuilder.Entity<GameRanking>(entity =>
        {
            entity.HasKey(e => e.RankId).HasName("PRIMARY");

            entity.ToTable("game_ranking", tb => tb.HasComment("游戏排行榜表"));

            entity.Property(e => e.CurrentRank).HasComment("排名");
            entity.Property(e => e.LastWeekRank).HasComment("上周排名");
            entity.Property(e => e.PeakPlayers)
                .HasDefaultValueSql("'0'")
                .HasComment("峰值在线人数");

            entity.HasOne(d => d.Game).WithOne(p => p.GameRanking)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("game_ranking_ibfk_1");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.GenreId).HasName("PRIMARY");

            entity.ToTable("genres", tb => tb.HasComment("游戏题材/风格词表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.LanguageId).HasName("PRIMARY");

            entity.ToTable("languages", tb => tb.HasComment("支持语言表"));
        });

        modelBuilder.Entity<LocalGameInstall>(entity =>
        {
            entity.HasKey(e => e.InstallId).HasName("PRIMARY");

            entity.ToTable("local_game_install", tb => tb.HasComment("本地安装信息"));

            entity.Property(e => e.DetectedTime).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.LocalGameInstalls)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("local_game_install_ibfk_3");

            entity.HasOne(d => d.Platform).WithMany(p => p.LocalGameInstalls).HasConstraintName("local_game_install_ibfk_1");

            entity.HasOne(d => d.User).WithMany(p => p.LocalGameInstalls)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("local_game_install_ibfk_2");
        });

        modelBuilder.Entity<LocalMod>(entity =>
        {
            entity.HasKey(e => e.ModId).HasName("PRIMARY");

            entity.ToTable("local_mod", tb => tb.HasComment("本地mod"));

            entity.Property(e => e.Enabled)
                .HasDefaultValueSql("'1'")
                .HasComment("0代表不启用，1代表启用");
            entity.Property(e => e.LastModified)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Install).WithMany(p => p.LocalMods).HasConstraintName("local_mod_ibfk_1");
        });

        modelBuilder.Entity<LocalSaveFile>(entity =>
        {
            entity.HasKey(e => e.SaveId).HasName("PRIMARY");

            entity.ToTable("local_save_file", tb => tb.HasComment("本地存档"));

            entity.Property(e => e.FileSize).HasComment("文件大小KB");
            entity.Property(e => e.IsBackupLocal).HasComment("0代表不备份，1代表备份");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Install).WithMany(p => p.LocalSaveFiles).HasConstraintName("local_save_file_ibfk_1");
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasKey(e => e.NewsId).HasName("PRIMARY");

            entity.ToTable("news", tb => tb.HasComment("新闻/公告源"));

            entity.Property(e => e.Date).HasComment("Unix时间戳");
        });

        modelBuilder.Entity<NotificationCenter>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PRIMARY");

            entity.ToTable("notification_center", tb => tb.HasComment("通知中心表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsRead).HasDefaultValueSql("'0'");
            entity.Property(e => e.NotificationType).HasDefaultValueSql("'info'");

            entity.HasOne(d => d.User).WithMany(p => p.NotificationCenters)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notification_center_ibfk_1");
        });

        modelBuilder.Entity<ParentalAlertLog>(entity =>
        {
            entity.HasKey(e => e.AlertId).HasName("PRIMARY");

            entity.ToTable("parental_alert_log", tb => tb.HasComment("家长监管报警日志表"));

            entity.Property(e => e.AlertTime).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.ChildUser).WithMany(p => p.ParentalAlertLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("parental_alert_log_ibfk_2");

            entity.HasOne(d => d.Notification).WithMany(p => p.ParentalAlertLogs).HasConstraintName("parental_alert_log_ibfk_3");

            entity.HasOne(d => d.Rule).WithMany(p => p.ParentalAlertLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("parental_alert_log_ibfk_1");
        });

        modelBuilder.Entity<ParentalControlRelationship>(entity =>
        {
            entity.HasKey(e => e.RelationshipId).HasName("PRIMARY");

            entity.ToTable("parental_control_relationship", tb => tb.HasComment("家长监管关系表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.ChildUser).WithOne(p => p.ParentalControlRelationshipChildUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("parental_control_relationship_ibfk_2");

            entity.HasOne(d => d.ParentUser).WithMany(p => p.ParentalControlRelationshipParentUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("parental_control_relationship_ibfk_1");
        });

        modelBuilder.Entity<ParentalControlRule>(entity =>
        {
            entity.HasKey(e => e.RuleId).HasName("PRIMARY");

            entity.ToTable("parental_control_rule", tb => tb.HasComment("家长监管规则表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValueSql("'1'");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.ChildUser).WithMany(p => p.ParentalControlRules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("parental_control_rule_ibfk_1");
        });

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.HasKey(e => e.PlatformId).HasName("PRIMARY");

            entity.ToTable("platforms", tb => tb.HasComment("游戏平台表"));

            entity.Property(e => e.Status)
                .HasDefaultValueSql("'1'")
                .HasComment("0不启用,1启用");
        });

        modelBuilder.Entity<PlayerPlatform>(entity =>
        {
            entity.HasKey(e => new { e.PlatformUserId, e.PlatformId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("player_platform", tb => tb.HasComment("玩家在某一平台的账号资料"));

            entity.Property(e => e.PlatformUserId).HasComment("平台侧用户标识（平台唯一）");
            entity.Property(e => e.PlatformId).HasComment("平台ID（标识Steam/Epic等）");

            entity.HasOne(d => d.Platform).WithMany(p => p.PlayerPlatforms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("player_platform_ibfk_1");
        });

        modelBuilder.Entity<PreferenceGenre>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("preference_genre", tb => tb.HasComment("用户偏好与游戏题材关联表"));

            entity.HasOne(d => d.Genre).WithMany(p => p.PreferenceGenres)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("preference_genre_ibfk_2");

            entity.HasOne(d => d.Preference).WithMany(p => p.PreferenceGenres).HasConstraintName("preference_genre_ibfk_1");
        });

        modelBuilder.Entity<PriceAlertLog>(entity =>
        {
            entity.HasKey(e => e.AlertId).HasName("PRIMARY");

            entity.ToTable("price_alert_log", tb => tb.HasComment("价格提醒日志表"));

            entity.Property(e => e.AlertTime).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Notification).WithMany(p => p.PriceAlertLogs).HasConstraintName("price_alert_log_ibfk_3");

            entity.HasOne(d => d.Price).WithMany(p => p.PriceAlertLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_alert_log_ibfk_2");

            entity.HasOne(d => d.Subscription).WithMany(p => p.PriceAlertLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_alert_log_ibfk_1");
        });

        modelBuilder.Entity<PriceAlertSubscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("PRIMARY");

            entity.ToTable("price_alert_subscription", tb => tb.HasComment("价格提醒订阅表(愿望单)"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValueSql("'1'");
            entity.Property(e => e.TargetDiscount).HasComment("NULL表示不启用");
            entity.Property(e => e.TargetPrice).HasComment("NULL表示不启用");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.PriceAlertSubscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_alert_subscription_ibfk_2");

            entity.HasOne(d => d.Platform).WithMany(p => p.PriceAlertSubscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_alert_subscription_ibfk_3");

            entity.HasOne(d => d.User).WithMany(p => p.PriceAlertSubscriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_alert_subscription_ibfk_1");
        });

        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.HasKey(e => e.PriceId).HasName("PRIMARY");

            entity.ToTable("price_history", tb => tb.HasComment("游戏价格历史表"));

            entity.Property(e => e.DiscountRate).HasComment("0-100");
            entity.Property(e => e.RecordDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Game).WithMany(p => p.PriceHistories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_history_ibfk_1");

            entity.HasOne(d => d.Platform).WithMany(p => p.PriceHistories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("price_history_ibfk_2");
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasKey(e => e.PublisherId).HasName("PRIMARY");

            entity.ToTable("publishers", tb => tb.HasComment("游戏发行商"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.HasKey(e => e.RecommendationId).HasName("PRIMARY");

            entity.ToTable("recommendation", tb => tb.HasComment("AI推荐结果表-个性化推荐记录"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ExpireTime).HasComment("默认7天");
            entity.Property(e => e.Reason).HasComment("AI生成解释短文");
            entity.Property(e => e.RecommendationType).HasComment("推荐类型");

            entity.HasOne(d => d.Game).WithMany(p => p.Recommendations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("recommendation_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.Recommendations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("recommendation_ibfk_1");
        });

        modelBuilder.Entity<RecommendationFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PRIMARY");

            entity.ToTable("recommendation_feedback", tb => tb.HasComment("推荐反馈表-AI算法优化反馈记录"));

            entity.Property(e => e.FeedbackResult).HasComment("1喜欢/2不喜欢");
            entity.Property(e => e.FeedbackTime).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Recommendation).WithOne(p => p.RecommendationFeedback)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("recommendation_feedback_ibfk_1");

            entity.HasOne(d => d.User).WithMany(p => p.RecommendationFeedbacks)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("recommendation_feedback_ibfk_2");
        });

        modelBuilder.Entity<ReportGenerationRecord>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PRIMARY");

            entity.ToTable("report_generation_record", tb => tb.HasComment("报表生成历史"));

            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasComment("0代表未生成，1代表生成");

            entity.HasOne(d => d.Template).WithMany(p => p.ReportGenerationRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("report_generation_record_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.ReportGenerationRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("report_generation_record_ibfk_1");
        });

        modelBuilder.Entity<ReportTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId).HasName("PRIMARY");

            entity.ToTable("report_template", tb => tb.HasComment("报表模板"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PRIMARY");

            entity.ToTable("role", tb => tb.HasComment("登录权限控制表"));
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("user", tb => tb.HasComment("核心登录与基础信息表"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Gender)
                .HasDefaultValueSql("'0'")
                .HasComment("1男/2女/0未知");
            entity.Property(e => e.HashedPassword).HasComment("AES-256加密");
            entity.Property(e => e.RoleId).HasDefaultValueSql("'1'");
            entity.Property(e => e.Status).HasDefaultValueSql("'inactive'");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_ibfk_1");
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => e.UserAchievementId).HasName("PRIMARY");

            entity.ToTable("user_achievements", tb => tb.HasComment("用户成就解锁记录"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UnlockTime).HasComment("null表示未解锁");
            entity.Property(e => e.Unlocked).HasComment("0未解锁/1已解锁");

            entity.HasOne(d => d.Achievement).WithMany(p => p.UserAchievements)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_achievements_ibfk_2");

            entity.HasOne(d => d.Platform).WithMany(p => p.UserAchievements)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_achievements_ibfk_3");

            entity.HasOne(d => d.User).WithMany(p => p.UserAchievements)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_achievements_ibfk_1");
        });

        modelBuilder.Entity<UserGameLibrary>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("user_game_library", tb => tb.HasComment("用户统一游戏库统计"));

            entity.Property(e => e.UserId).ValueGeneratedNever();

            entity.HasOne(d => d.User).WithOne(p => p.UserGameLibrary)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_game_library_ibfk_1");
        });

        modelBuilder.Entity<UserPlatformBinding>(entity =>
        {
            entity.HasKey(e => e.BindingId).HasName("PRIMARY");

            entity.ToTable("user_platform_binding", tb => tb.HasComment("跨平台账号OAuth绑定记录"));

            entity.Property(e => e.AccessToken).HasComment("AES-256加密存储");
            entity.Property(e => e.BindingStatus)
                .HasDefaultValueSql("'1'")
                .HasComment("1已绑定/0已解绑");
            entity.Property(e => e.BindingTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ExpireTime).HasComment("按平台API规则设置");
            entity.Property(e => e.PlatformUserId).HasComment("第三方平台用户ID（如SteamID）");
            entity.Property(e => e.RefreshToken).HasComment("AES-256加密存储");

            entity.HasOne(d => d.Platform).WithMany(p => p.UserPlatformBindings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_platform_binding_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.UserPlatformBindings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_platform_binding_ibfk_1");

            entity.HasOne(d => d.PlayerPlatform).WithMany(p => p.UserPlatformBindings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_platform_binding_ibfk_3");
        });

        modelBuilder.Entity<UserPlatformLibrary>(entity =>
        {
            entity.HasKey(e => new { e.PlatformUserId, e.PlatformId, e.GameId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.ToTable("user_platform_library", tb => tb.HasComment("用户在某平台的单款游戏记录"));

            entity.Property(e => e.PlatformUserId).HasComment("平台侧用户标识（平台唯一）");
            entity.Property(e => e.GameId).HasComment("若为单款记录");
            entity.Property(e => e.AchievementsTotal).HasComment("成就总数（平台/该游戏）");
            entity.Property(e => e.AchievementsUnlocked).HasComment("已解锁成就数（平台/该游戏）");
            entity.Property(e => e.PlaytimeMinutes).HasComment("累计游玩分钟数（该平台/该游戏）");

            entity.HasOne(d => d.Game).WithMany(p => p.UserPlatformLibraries)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_platform_library_ibfk_2");

            entity.HasOne(d => d.PlayerPlatform).WithMany(p => p.UserPlatformLibraries)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_platform_library_ibfk_1");
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.PreferenceId).HasName("PRIMARY");

            entity.ToTable("user_preference", tb => tb.HasComment("用户偏好表-AI推荐算法支撑数据"));

            entity.Property(e => e.PlaytimeRange).HasComment("偏好游玩时长区间（如\"1-3小时/天\"）");
            entity.Property(e => e.PriceSensitivity)
                .HasDefaultValueSql("'2'")
                .HasComment("价格敏感度（1高/2中/3低）");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.User).WithOne(p => p.UserPreference)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_preference_ibfk_1");
        });

        // D模块索引配置
        modelBuilder.Entity<UserAchievement>()
            .HasIndex(ua => new { ua.UserId, ua.AchievementId, ua.PlatformId })
            .IsUnique();

        modelBuilder.Entity<PriceAlertSubscription>()
            .HasIndex(p => new { p.UserId, p.GameId, p.PlatformId })
            .IsUnique();

        modelBuilder.Entity<PriceHistory>()
            .HasIndex(p => new { p.GameId, p.PlatformId });

        modelBuilder.Entity<PreferenceGenre>()
            .HasIndex(pg => new { pg.PreferenceId, pg.GenreId })
            .IsUnique();

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
