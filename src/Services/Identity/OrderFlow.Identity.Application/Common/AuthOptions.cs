namespace OrderFlow.Identity.Application.Common;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 5;
    public int RefreshTokenDays { get; set; } = 7;
}
