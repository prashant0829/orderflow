using Microsoft.AspNetCore.Identity;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Domain.Users;

namespace OrderFlow.Identity.Infrastructure.Security;

/// <summary>PBKDF2 with a random salt per password and a stored iteration count, from Microsoft.Extensions.Identity.Core.</summary>
public class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    // The framework ignores the user object for hashing; a placeholder avoids exposing it in the domain.
    private static readonly User Placeholder = User.Create(Guid.NewGuid(), "placeholder@orderflow.local", "x", "x", "x", DateTime.UtcNow);

    public string Hash(string password) => _hasher.HashPassword(Placeholder, password);

    public bool Verify(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(Placeholder, hashedPassword, providedPassword) != PasswordVerificationResult.Failed;
}
