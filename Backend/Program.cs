using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PlayLinker.Data;
using PlayLinker.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// 添加控制器
builder.Services.AddControllers();

// 配置Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PlayLinker API - 开发者B模块",
        Version = "v1",
        Description = "统一游戏管理平台 - 游戏数据、元数据、游戏库、成就、Steam集成",
        Contact = new OpenApiContact
        {
            Name = "开发者B",
            Email = "developer@playlinker.com"
        }
    });

    // 添加JWT认证支持
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. 请先调用 POST /api/v1/auth/token 获取Token,然后在此处输入: Bearer {token}",
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

