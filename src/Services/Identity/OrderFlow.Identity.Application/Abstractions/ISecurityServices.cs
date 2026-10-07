using OrderFlow.Identity.Domain.Users;

namespace OrderFlow.Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hashedPassword, string providedPassword);
}

public record AccessToken(string Value, DateTime ExpiresAtUtc);

public record NewRefreshToken(string RawValue, string Hash);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    NewRefreshToken CreateRefreshToken();

    string HashRefreshToken(string rawToken);
}

public interface IUnitOfWork
{
    /// <exception cref="OrderFlow.SharedKernel.ConflictException">The email was registered by a parallel request.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
