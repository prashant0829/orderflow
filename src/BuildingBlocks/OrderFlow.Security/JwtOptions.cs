namespace OrderFlow.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = "orderflow-identity";
    public string Audience { get; set; } = "orderflow-api";

    /// <summary>Shared secret (HMAC-SHA256). Dev value lives in appsettings.Development.json; in real deployments set Jwt__SigningKey from a secret store.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
