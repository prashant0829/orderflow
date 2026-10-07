using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Application.Auth;
using OrderFlow.Identity.Application.Auth.Commands;
using OrderFlow.Identity.Application.Common;
using OrderFlow.Identity.Domain.Users;
using OrderFlow.Security;
using OrderFlow.SharedKernel;

namespace OrderFlow.Identity.UnitTests;

public class AuthHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IOptions<AuthOptions> _options = Options.Create(new AuthOptions());
    private int _issued;

    public AuthHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(call => "hash:" + call.Arg<string>());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) == "hash:" + call.ArgAt<string>(1));
        _tokens.CreateAccessToken(Arg.Any<User>()).Returns(new AccessToken("jwt", DateTime.UtcNow.AddMinutes(15)));
        _tokens.CreateRefreshToken().Returns(_ => { _issued++; return new NewRefreshToken($"raw{_issued}", $"hash{_issued}"); });
        _tokens.HashRefreshToken(Arg.Any<string>()).Returns(call => "hash" + call.Arg<string>()[3..]);
    }

    private ITokenIssuer Issuer => new TokenIssuer(_tokens, _refreshTokens, _options, _time);

    private User ExistingUser(string password = "Passw0rd") =>
        User.Create(Guid.NewGuid(), "bob@example.com", "hash:" + password, "Bob", Roles.Customer, _time.GetUtcNow().UtcDateTime);

    [Fact]
    public async Task Register_CreatesCustomerAndStoresHashedPasswordAndRefreshToken()
    {
        var result = await new RegisterUserCommandHandler(_users, _hasher, Issuer, _unitOfWork, _time)
            .Handle(new RegisterUserCommand("Bob@Example.com", "Passw0rd", "Bob"), CancellationToken.None);

        result.User.Role.Should().Be(Roles.Customer);
        await _users.Received(1).AddAsync(Arg.Is<User>(user => user.PasswordHash == "hash:Passw0rd" && user.Email == "bob@example.com"), Arg.Any<CancellationToken>());
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ExistingEmail_ThrowsConflict()
    {
        _users.GetByEmailAsync("bob@example.com", Arg.Any<CancellationToken>()).Returns(ExistingUser());

        var act = () => new RegisterUserCommandHandler(_users, _hasher, Issuer, _unitOfWork, _time)
            .Handle(new RegisterUserCommand("bob@example.com", "Passw0rd", "Bob"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void RegisterValidator_RejectsWeakPasswords(string password)
    {
        new RegisterUserCommandValidator().Validate(new RegisterUserCommand("a@b.com", password, "x")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WrongPassword_CountsFailureSavesAndThrows()
    {
        var user = ExistingUser();
        _users.GetByEmailAsync("bob@example.com", Arg.Any<CancellationToken>()).Returns(user);
        var handler = new LoginCommandHandler(_users, _hasher, Issuer, _unitOfWork, _options, _time);

        var act = () => handler.Handle(new LoginCommand("bob@example.com", "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        user.FailedLoginCount.Should().Be(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_FifthWrongPassword_LocksAccountEvenForTheRightPassword()
    {
        var user = ExistingUser();
        _users.GetByEmailAsync("bob@example.com", Arg.Any<CancellationToken>()).Returns(user);
        var handler = new LoginCommandHandler(_users, _hasher, Issuer, _unitOfWork, _options, _time);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await handler.Invoking(h => h.Handle(new LoginCommand("bob@example.com", "wrong"), CancellationToken.None)).Should().ThrowAsync<UnauthorizedException>();
        }

        var act = () => handler.Handle(new LoginCommand("bob@example.com", "Passw0rd"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*Too many*");
    }

    [Fact]
    public async Task Login_LockExpires_AllowsLoginAgain()
    {
        var user = ExistingUser();
        _users.GetByEmailAsync("bob@example.com", Arg.Any<CancellationToken>()).Returns(user);
        var handler = new LoginCommandHandler(_users, _hasher, Issuer, _unitOfWork, _options, _time);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await handler.Invoking(h => h.Handle(new LoginCommand("bob@example.com", "wrong"), CancellationToken.None)).Should().ThrowAsync<UnauthorizedException>();
        }

        _time.Advance(TimeSpan.FromMinutes(6));
        var result = await handler.Handle(new LoginCommand("bob@example.com", "Passw0rd"), CancellationToken.None);

        result.AccessToken.Should().Be("jwt");
    }

    [Fact]
    public async Task Login_UnknownEmail_StillHashesAndSaysInvalidCredentials()
    {
        var handler = new LoginCommandHandler(_users, _hasher, Issuer, _unitOfWork, _options, _time);

        var act = () => handler.Handle(new LoginCommand("nobody@example.com", "x"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid email or password.");
        _hasher.Received().Verify(Arg.Any<string>(), "x");
    }

    private RefreshAccessTokenCommandHandler RefreshHandler() =>
        new(_refreshTokens, _users, _tokens, Issuer, _unitOfWork, _time, NullLogger<RefreshAccessTokenCommandHandler>.Instance);

    [Fact]
    public async Task Refresh_ValidToken_RotatesIt()
    {
        var user = ExistingUser();
        var old = RefreshToken.Create(user.Id, "hash0", _time.GetUtcNow().UtcDateTime, TimeSpan.FromDays(7));
        _refreshTokens.GetByHashAsync("hash0", Arg.Any<CancellationToken>()).Returns(old);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await RefreshHandler().Handle(new RefreshAccessTokenCommand("raw0"), CancellationToken.None);

        result.RefreshToken.Should().Be("raw1");
        old.IsRevoked.Should().BeTrue();
        old.ReplacedByTokenHash.Should().Be("hash1");
    }

    [Fact]
    public async Task Refresh_AlreadyUsedToken_RevokesEverySessionAndThrows()
    {
        var userId = Guid.NewGuid();
        var now = _time.GetUtcNow().UtcDateTime;
        var used = RefreshToken.Create(userId, "hash0", now, TimeSpan.FromDays(7));
        used.Revoke(now, "hash1");
        var live = RefreshToken.Create(userId, "hash1", now, TimeSpan.FromDays(7));
        _refreshTokens.GetByHashAsync("hash0", Arg.Any<CancellationToken>()).Returns(used);
        _refreshTokens.ListActiveForUserAsync(userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([live]);

        var act = () => RefreshHandler().Handle(new RefreshAccessTokenCommand("raw0"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        live.IsRevoked.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Throws()
    {
        var old = RefreshToken.Create(Guid.NewGuid(), "hash0", _time.GetUtcNow().UtcDateTime, TimeSpan.FromDays(7));
        _refreshTokens.GetByHashAsync("hash0", Arg.Any<CancellationToken>()).Returns(old);
        _time.Advance(TimeSpan.FromDays(8));

        var act = () => RefreshHandler().Handle(new RefreshAccessTokenCommand("raw0"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Refresh_UnknownToken_Throws()
    {
        var act = () => RefreshHandler().Handle(new RefreshAccessTokenCommand("raw9"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Logout_UnknownToken_IsHarmless()
    {
        await new LogoutCommandHandler(_refreshTokens, _tokens, _unitOfWork, _time).Handle(new LogoutCommand("raw9"), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
