using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Application.Auth.Dtos;
using OrderFlow.Identity.Application.Common;
using OrderFlow.SharedKernel;

namespace OrderFlow.Identity.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider) : IRequestHandler<LoginCommand, AuthResult>
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    // A real hash of a throwaway password, so an unknown email costs the same time as a wrong password.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash(Guid.NewGuid().ToString("N")));

    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await users.GetByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(_dummyHash.Value, command.Password);
            IdentityTelemetry.RecordLogin(IdentityTelemetry.InvalidCredentials);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (user.IsLockedOut(now))
        {
            IdentityTelemetry.RecordLogin(IdentityTelemetry.Locked);
            throw new UnauthorizedException("Too many failed attempts. Try again later.");
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            user.RegisterFailedLogin(now, options.Value.MaxFailedLoginAttempts, TimeSpan.FromMinutes(options.Value.LockoutMinutes));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            IdentityTelemetry.RecordLogin(IdentityTelemetry.InvalidCredentials);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        user.RegisterSuccessfulLogin();
        var (result, _) = await tokenIssuer.IssueAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        IdentityTelemetry.RecordLogin(IdentityTelemetry.Success);
        return result;
    }
}
