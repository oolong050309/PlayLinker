using System.Threading.Tasks;

namespace PlayLinker.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
    Task SendWelcomeAsync(string to, string username);
    Task SendPasswordResetAsync(string to, string username, string resetLink, int expiresMinutes = 30);
    Task SendPasswordResetCodeAsync(string to, string username, string code, int expiresMinutes = 30);
}
