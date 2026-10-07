using OrderFlow.Identity.Domain.Users;

namespace OrderFlow.Identity.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken);
}
