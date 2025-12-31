using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PlayLinker.Data;
using PlayLinker.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Text;
using PlayLinker.Models.DTOs;

var builder = WebApplication.CreateBuilder(args);

// 配置日志
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 1. 配置数据库上下文
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("数据库连接字符串未配置");

builder.Services.AddDbContext<PlayLinkerDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 21))
    ));

// 2. 配置 JWT 认证
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero
    };
});

// 3. 注册应用服务 (依赖注入)
// --- 令牌加密服务 (新增) ---
builder.Services.AddScoped<ITokenEncryptionService, TokenEncryptionService>();

// --- 游戏平台服务 ---
builder.Services.AddScoped<ISteamService, SteamService>();
builder.Services.AddHttpClient<ISteamService, SteamService>();
builder.Services.AddScoped<IXboxService, XboxService>();
builder.Services.AddScoped<IPsnService, PsnService>();
builder.Services.AddScoped<IGogService, GogService>();
builder.Services.AddScoped<IEpicService, EpicService>();
builder.Services.AddScoped<ReportGenerationService>();

// --- 认证和用户管理服务 (来自 HEAD) ---
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddMemoryCache();

// --- 阿里云 OSS 存储服务 ---
builder.Services.Configure<AliyunOssOptions>(builder.Configuration.GetSection("AliyunOss"));
builder.Services.AddScoped<IAliyunOssService, AliyunOssService>();

// --- AI 服务 (来自 Incoming) ---
builder.Services.AddHttpClient(); // 通用 HttpClient
builder.Services.AddScoped<IAiService, AiService>();

// --- 价格监控后台服务 ---
builder.Services.AddHttpClient<PriceMonitoringService>();
builder.Services.AddHostedService<PriceMonitoringService>();

// [新增] 注册排行榜监控服务
builder.Services.AddHostedService<PlayLinker.Services.RankingMonitoringService>();

// --- 家长监管监控后台服务 ---
// HostedService 只会以 IHostedService 注册，无法通过 GetRequiredService<ParentalMonitoringService>() 解析。
// 因此需要额外注册为可注入服务。
builder.Services.AddSingleton<ParentalMonitoringService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ParentalMonitoringService>());

// 4. 添加控制器
builder.Services.AddControllers();

// 5. 配置 Swagger 文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PlayLinker API - 统一游戏管理平台",
        Version = "v1",
        Description = @"PlayLinker 统一游戏管理平台完整API文档

📦 API模块列表：
• 开发者A：账号绑定与数据接入 (AuthController, SteamController, XboxController, PsnController, GogController)
• 开发者B：游戏数据与元数据 (GamesController, MetadataController, AchievementsController, LibraryController, WishlistController, NewsController, PreferencesController)
• 开发者C：本地游戏管理、存档管理、云存档、Mod管理、报表系统、数据分析 (LocalGamesController, SavesController, CloudController, ModsController, ReportsController, AnalyticsController)
• 开发者D：家长监管与社交功能 (待实现)

🔐 认证说明：
大部分API需要JWT认证，请先调用 POST /api/v1/auth/token 获取Token",
        Contact = new OpenApiContact
        {
            Name = "PlayLinker Team",
            Email = "developer@playlinker.com"
        }
    });

    // 启用注解与XML注释
    c.EnableAnnotations();
    var xmlFile = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // 添加JWT认证支持
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. 在此输入: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 6. 配置 CORS (允许跨域)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// 7. 启动时初始化数据库与基础数据 (来自 HEAD)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PlayLinker.Data.PlayLinkerDbContext>();
        // 自动应用迁移
        db.Database.Migrate();

        // 初始化默认角色
        if (!db.Roles.Any(r => r.RoleName == "user"))
        {
            db.Roles.Add(new PlayLinker.Models.Entities.Role { RoleName = "user", RoleDesc = "普通用户" });
        }
        if (!db.Roles.Any(r => r.RoleName == "parent"))
        {
            db.Roles.Add(new PlayLinker.Models.Entities.Role { RoleName = "parent", RoleDesc = "家长" });
        }
        if (!db.Roles.Any(r => r.RoleName == "admin"))
        {
            db.Roles.Add(new PlayLinker.Models.Entities.Role { RoleName = "admin", RoleDesc = "管理员" });
        }
        db.SaveChanges();
    }
    catch (Exception seedingEx)
    {
        var seedingLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        seedingLogger.LogError(seedingEx, "启动时种子数据初始化失败");
    }
}

// 配置 HTTP 请求管道
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlayLinker API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();