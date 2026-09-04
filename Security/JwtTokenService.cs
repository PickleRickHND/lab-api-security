using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SecureApi.Security;

public interface IJwtTokenService
{
    TokenResponse CreateToken(DemoUser user);
}

public sealed class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    public TokenResponse CreateToken(DemoUser user)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(options.ExpirationMinutes);
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(options.SigningKey));

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Role, "user")
        ];

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256));

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            checked(options.ExpirationMinutes * 60),
            expiresAt);
    }
}

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTime ExpiresAtUtc);

public sealed record DemoUser(
    string Id,
    string Username,
    string PasswordSalt,
    string PasswordHash);
