using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PlayLinker.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var settings = _config.GetSection("EmailSettings");
        var host = settings["SmtpHost"] ?? "";
        var port = int.TryParse(settings["SmtpPort"], out var p) ? p : 587;
        var enableSsl = bool.TryParse(settings["EnableSsl"], out var ssl) ? ssl : true;
        var fromEmail = settings["FromEmail"] ?? "";
        var fromName = settings["FromName"] ?? "PlayLinker";
        var userName = settings["UserName"] ?? fromEmail;
        // 优先使用环境变量（更安全）
        var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD") ?? settings["Password"] ?? "";

        // 硬编码QQ邮箱备用（仅当未提供配置/环境变量时生效）
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(password))
        {
            host = string.IsNullOrWhiteSpace(host) ? "smtp.qq.com" : host;
            port = port == 0 ? 587 : port;
            enableSsl = true;
            fromEmail = string.IsNullOrWhiteSpace(fromEmail) ? "599850515@qq.com" : fromEmail;
            fromName = string.IsNullOrWhiteSpace(fromName) ? "PlayLinker" : fromName;
            userName = string.IsNullOrWhiteSpace(userName) ? fromEmail : userName;
            password = string.IsNullOrWhiteSpace(password) ? "ockeriknbsmabcdg" : password;
        }

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Email settings incomplete. host or fromEmail or password is missing.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(userName, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }

    public Task SendWelcomeAsync(string to, string username)
    {
        var subject = "欢迎使用 PlayLinker!";
        var body = $@"<h3>Hi {WebUtility.HtmlEncode(username)}, 欢迎加入 PlayLinker</h3>
<p>很高兴见到你！现在可以开始绑定平台、管理游戏库与设置家长监管规则。</p>
<p>祝你游戏愉快！</p>";
        return SendAsync(to, subject, body);
    }

    public Task SendPasswordResetAsync(string to, string username, string resetLink, int expiresMinutes = 30)
    {
        var subject = "PlayLinker 密码重置";
        var body = $@"<h3>Hi {WebUtility.HtmlEncode(username)},</h3>
<p>收到您的密码重置请求，请点击以下链接完成重置（{expiresMinutes}分钟内有效）：</p>
<p><a href=""{WebUtility.HtmlEncode(resetLink)}"">{WebUtility.HtmlEncode(resetLink)}</a></p>
<p>如果这不是您本人的操作，请忽略本邮件。</p>";
        return SendAsync(to, subject, body);
    }

    public Task SendPasswordResetCodeAsync(string to, string username, string code, int expiresMinutes = 30)
    {
        var subject = "PlayLinker 验证码";
        var body = $@"<h3>Hi {WebUtility.HtmlEncode(username)},</h3>
<p>您正在进行密码重置操作。请在 {expiresMinutes} 分钟内使用以下验证码：</p>
<p style=""font-size:20px;font-weight:bold;"">{WebUtility.HtmlEncode(code)}</p>
<p>如果这不是您本人的操作，请忽略本邮件。</p>";
        return SendAsync(to, subject, body);
    }

    public Task SendPriceAlertAsync(string to, string username, string gameName, string alertType, decimal currentPrice, decimal? originalPrice, int? discountRate, decimal? targetPrice, int? targetDiscount)
    {
        var subject = $"价格提醒：{WebUtility.HtmlEncode(gameName)}";
        
        string alertMessage = "";
        if (alertType == "target_price" && targetPrice.HasValue)
        {
            alertMessage = $"游戏价格已降至 ¥{currentPrice:F2}，低于您设置的目标价格 ¥{targetPrice.Value:F2}。";
        }
        else if (alertType == "target_discount" && targetDiscount.HasValue)
        {
            alertMessage = $"游戏折扣已达到 {discountRate}%，达到您设置的目标折扣 {targetDiscount.Value}%。";
        }
        else if (alertType == "price_drop")
        {
            alertMessage = $"游戏价格出现下降，当前价格为 ¥{currentPrice:F2}。";
        }

        var body = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;"">
    <div style=""background-color: white; border-radius: 8px; padding: 30px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
        <h2 style=""color: #8b5cf6; margin-top: 0;"">🎮 价格提醒</h2>
        <p style=""font-size: 16px; color: #333;"">Hi {WebUtility.HtmlEncode(username)},</p>
        <p style=""font-size: 16px; color: #333;"">{alertMessage}</p>
        
        <div style=""background-color: #f8f9fa; border-left: 4px solid #8b5cf6; padding: 15px; margin: 20px 0; border-radius: 4px;"">
            <h3 style=""margin-top: 0; color: #333;"">{WebUtility.HtmlEncode(gameName)}</h3>
            <p style=""margin: 5px 0; font-size: 18px; font-weight: bold; color: #8b5cf6;"">当前价格：¥{currentPrice:F2}</p>";
        
        if (originalPrice.HasValue && originalPrice.Value > currentPrice)
        {
            body += $@"
            <p style=""margin: 5px 0; color: #666; text-decoration: line-through;"">原价：¥{originalPrice.Value:F2}</p>
            <p style=""margin: 5px 0; color: #22c55e; font-weight: bold;"">节省：¥{(originalPrice.Value - currentPrice):F2}</p>";
        }
        
        if (discountRate.HasValue && discountRate.Value > 0)
        {
            body += $@"
            <p style=""margin: 5px 0; color: #ef4444; font-weight: bold;"">折扣：-{discountRate.Value}%</p>";
        }
        
        body += $@"
        </div>
        
        <p style=""font-size: 14px; color: #666; margin-top: 20px;"">您可以在 PlayLinker 应用中查看详细信息并管理您的价格提醒。</p>
        <p style=""font-size: 12px; color: #999; margin-top: 20px;"">此邮件由 PlayLinker 价格监控系统自动发送。</p>
    </div>
</div>";
        
        return SendAsync(to, subject, body);
    }
}

