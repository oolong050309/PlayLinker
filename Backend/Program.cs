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

// 配置日志：清除默认的日志提供程序（包括EventLog），只使用Console和Debug
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 添加数据库上下文
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("数据库连接字符串未配置");

// 使用MySQL 8.0版本，避免AutoDetect尝试连接其他数据库
builder.Services.AddDbContext<PlayLinkerDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 21))
    ));

// 配置JWT认证
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

// 注册服务
builder.Services.AddScoped<ISteamService, SteamService>();
builder.Services.AddHttpClient<ISteamService, SteamService>();
builder.Services.AddScoped<IXboxService, XboxService>();
builder.Services.AddScoped<IPsnService, PsnService>();
builder.Services.AddScoped<IGogService, GogService>();
builder.Services.AddScoped<ReportGenerationService>();

// 注册认证和用户管理服务
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddMemoryCache();

// 添加控制器
builder.Services.AddControllers();

// 配置Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PlayLinker API - 统一游戏管理平台",
        Version = "v1",
        Description = @"PlayLinker 统一游戏管理平台完整API文档\n\n模块：认证、用户管理、平台绑定、通知中心、家长监管。",
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 配置CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 启动时初始化数据库与基础数据（角色等）
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PlayLinker.Data.PlayLinkerDbContext>();
        // 自动应用迁移（如无需迁移可移除此行）
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

// 配置HTTP请求管道
// 启用Swagger UI (在所有环境下都可用)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlayLinker API v1");
    c.RoutePrefix = "swagger"; // Swagger UI访问路径: http://localhost:5000/swagger
    c.DisplayRequestDuration(); // 显示请求耗时
    c.EnableDeepLinking(); // 启用深度链接
    c.EnableFilter(); // 启用过滤器
    c.EnableValidator(); // 启用验证器
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 输出启动信息
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("PlayLinker API 启动成功!");
logger.LogInformation("Swagger UI 访问地址: http://localhost:5000/swagger");
logger.LogInformation("API Base URL: http://localhost:5000/api/v1");

app.Run();

