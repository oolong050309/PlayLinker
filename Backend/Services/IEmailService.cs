using System.Threading.Tasks;

namespace PlayLinker.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
    Task SendWelcomeAsync(string to, string username);
    Task SendPasswordResetAsync(string to, string username, string resetLink, int expiresMinutes = 30);
    Task SendPasswordResetCodeAsync(string to, string username, string code, int expiresMinutes = 30);
    Task SendPriceAlertAsync(string to, string username, string gameName, string alertType, decimal currentPrice, decimal? originalPrice, int? discountRate, decimal? targetPrice, int? targetDiscount);
    Task SendParentalAlertAsync(string to, string username, string childUsername, string ruleType, Dictionary<string, object> violationDetails);
}
