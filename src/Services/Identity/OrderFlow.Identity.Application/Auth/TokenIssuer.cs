using Microsoft.Extensions.Options;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Application.Auth.Dtos;
using OrderFlow.Identity.Application.Common;
using OrderFlow.Identity.Domain.Users;

namespace OrderFlow.Identity.Application.Auth;

public interface ITokenIssuer
{
    /// <summary>Creates an access token and a new stored refresh token for the user.</summary>
    Task<(AuthResult Result, string RefreshTokenHash)> IssueAsync(User user, CancellationToken cancellationToken);
}

public class TokenIssuer(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider) : ITokenIssuer
{
    public async Task<(AuthResult Result, string RefreshTokenHash)> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var access = tokenService.CreateAccessToken(user);
        var refresh = tokenService.CreateRefreshToken();

        var stored = RefreshToken.Create(user.Id, refresh.Hash, now, TimeSpan.FromDays(options.Value.RefreshTokenDays));
        await refreshTokens.AddAsync(stored, cancellationToken);

        var result = new AuthResult(access.Value, access.ExpiresAtUtc, refresh.RawValue, stored.ExpiresAtUtc, user.ToDto());
        return (result, refresh.Hash);
    }
}
