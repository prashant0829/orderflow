using OrderFlow.Identity.Domain.Users;

namespace OrderFlow.Identity.Application.Auth.Dtos;

public record UserDto(Guid Id, string Email, string DisplayName, string Role);

public record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserDto User);

public static class UserMapping
{
    public static UserDto ToDto(this User user) => new(user.Id, user.Email, user.DisplayName, user.Role);
}
