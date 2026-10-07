using FluentValidation;
using MediatR;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Application.Auth.Dtos;
using OrderFlow.Identity.Application.Common;
using OrderFlow.Identity.Domain.Users;
using OrderFlow.Security;
using OrderFlow.SharedKernel;

namespace OrderFlow.Identity.Application.Auth.Commands;

public record RegisterUserCommand(string Email, string Password, string DisplayName) : IRequest<AuthResult>;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 100;
    private const int MaxEmailLength = 200;
    private const int MaxDisplayNameLength = 100;

    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(MaxEmailLength);
        RuleFor(command => command.DisplayName).MaximumLength(MaxDisplayNameLength);
        RuleFor(command => command.Password).NotEmpty()
            .MinimumLength(MinPasswordLength).MaximumLength(MaxPasswordLength)
            .Matches("[A-Z]").WithMessage("Password needs an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password needs a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password needs a digit.");
    }
}

public class RegisterUserCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RegisterUserCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await users.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        // Anyone can register, but only as a customer. Admins are created by seeding or by an existing admin.
        var user = User.Create(Guid.Empty, email, passwordHasher.Hash(command.Password), command.DisplayName, Roles.Customer, timeProvider.GetUtcNow().UtcDateTime);
        await users.AddAsync(user, cancellationToken);

        var (result, _) = await tokenIssuer.IssueAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        IdentityTelemetry.RecordRegistration();
        return result;
    }
}
