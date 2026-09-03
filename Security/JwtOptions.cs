namespace SecureApi.Security;

public sealed record JwtOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    int ExpirationMinutes)
{
    public const string SectionName = "Jwt";
}
