using PlayLinker.Models.Entities;

namespace PlayLinker.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user, out int expiresInSeconds);
    string GenerateRefreshToken(out DateTime expiresAtUtc);

    void StoreRefreshToken(int userId, string refreshToken, DateTime expiresAtUtc);
    bool ValidateRefreshToken(string refreshToken, out int userId);
    void RevokeRefreshToken(string refreshToken);
    void RevokeAllForUser(int userId);
}

