using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Application.Auth.Dtos;
using OrderFlow.Identity.Application.Common;
using OrderFlow.SharedKernel;

namespace OrderFlow.Identity.Application.Auth.Commands;

public record RefreshAccessTokenCommand(string RefreshToken) : IRequest<AuthResult>;

public record LogoutCommand(string RefreshToken) : IRequest;

public class RefreshAccessTokenCommandValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenCommandValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}

/// <summary>
/// Rotation: every refresh token works once and is replaced. If a token that was already used shows up again,
/// someone copied it, so every session of that user is ended.
/// </summary>
public class RefreshAccessTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    ITokenService tokenService,
    ITokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RefreshAccessTokenCommandHandler> logger) : IRequestHandler<RefreshAccessTokenCommand, AuthResult>
{
    private const string InvalidTokenMessage = "The refresh token is invalid or expired.";

    public async Task<AuthResult> Handle(RefreshAccessTokenCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stored = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(command.RefreshToken), cancellationToken)
            ?? throw new UnauthorizedException(InvalidTokenMessage);

        if (stored.IsRevoked)
        {
            IdentityTelemetry.RecordTokenReuse();
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions", stored.UserId);
            foreach (var active in await refreshTokens.ListActiveForUserAsync(stored.UserId, now, cancellationToken))
            {
                active.Revoke(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(InvalidTokenMessage);
        }

        if (stored.IsExpired(now))
        {
            throw new UnauthorizedException(InvalidTokenMessage);
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new UnauthorizedException(InvalidTokenMessage);

        var (result, newHash) = await tokenIssuer.IssueAsync(user, cancellationToken);
        stored.Revoke(now, newHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }
}

public class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var stored = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (stored is null)
        {
            return;
        }

        stored.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
