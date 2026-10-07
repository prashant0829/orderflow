using OrderFlow.SharedKernel;

namespace OrderFlow.Identity.Domain.Users;

public class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public int FailedLoginCount { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static User Create(Guid id, string email, string passwordHash, string displayName, string role, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        return new User
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Trim() : displayName.Trim(),
            Role = role,
            CreatedAtUtc = nowUtc
        };
    }

    public bool IsLockedOut(DateTime nowUtc) => LockedUntilUtc is { } lockedUntil && lockedUntil > nowUtc;

    /// <summary>Counts a wrong password. After too many in a row the account locks for a while, which slows down password guessing.</summary>
    public void RegisterFailedLogin(DateTime nowUtc, int maxAttempts, TimeSpan lockDuration)
    {
        FailedLoginCount++;

        if (FailedLoginCount >= maxAttempts)
        {
            LockedUntilUtc = nowUtc.Add(lockDuration);
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntilUtc = null;
    }
}
