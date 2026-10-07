using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OrderFlow.Identity.Domain.Users;
using OrderFlow.Security;

namespace OrderFlow.Identity.UnitTests;

public class UserAndTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(5);

    private static User NewUser() => User.Create(Guid.Empty, "  Bob@Example.com ", "hash", "Bob", Roles.Customer, Now);

    [Fact]
    public void Create_NormalizesEmail()
    {
        NewUser().Email.Should().Be("bob@example.com");
    }

    [Fact]
    public void FailedLogins_LockTheAccountAfterTheLimit()
    {
        var user = NewUser();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            user.RegisterFailedLogin(Now, 5, LockFor);
        }

        user.IsLockedOut(Now).Should().BeFalse();

        user.RegisterFailedLogin(Now, 5, LockFor);

        user.IsLockedOut(Now).Should().BeTrue();
        user.IsLockedOut(Now.Add(LockFor).AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void SuccessfulLogin_ClearsFailuresAndLock()
    {
        var user = NewUser();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(Now, 5, LockFor);
        }

        user.RegisterSuccessfulLogin();

        user.IsLockedOut(Now).Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public void RefreshToken_IsActiveUntilExpiredOrRevoked()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(7));

        token.IsActive(Now.AddDays(6)).Should().BeTrue();
        token.IsActive(Now.AddDays(8)).Should().BeFalse();

        token.Revoke(Now, "next-hash");

        token.IsActive(Now).Should().BeFalse();
        token.ReplacedByTokenHash.Should().Be("next-hash");
    }

    [Fact]
    public void Revoke_Twice_KeepsTheFirstReplacement()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Now, TimeSpan.FromDays(7));
        token.Revoke(Now, "first");

        token.Revoke(Now.AddMinutes(1), "second");

        token.ReplacedByTokenHash.Should().Be("first");
    }

    [Fact]
    public void JwtOptions_ShortSigningKey_FailsFast()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "too-short" }).Build();

        var act = () => configuration.GetJwtOptions();

        act.Should().Throw<InvalidOperationException>().WithMessage("*at least 32*");
    }

    [Fact]
    public void CanAccessCustomer_OwnerAndAdminOnly()
    {
        var ownerId = Guid.NewGuid();
        var owner = Principal(ownerId, Roles.Customer);

        owner.CanAccessCustomer(ownerId).Should().BeTrue();
        owner.CanAccessCustomer(Guid.NewGuid()).Should().BeFalse();
        Principal(Guid.NewGuid(), Roles.Admin).CanAccessCustomer(ownerId).Should().BeTrue();
    }

    private static System.Security.Claims.ClaimsPrincipal Principal(Guid id, string role) => new(new System.Security.Claims.ClaimsIdentity(
        [new System.Security.Claims.Claim("sub", id.ToString()), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)],
        "test", "name", System.Security.Claims.ClaimTypes.Role));
}
