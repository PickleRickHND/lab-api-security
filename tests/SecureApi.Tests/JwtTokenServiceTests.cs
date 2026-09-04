using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SecureApi.Security;
using Xunit;

namespace SecureApi.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateTokenIncluyeIdentidadFirmaYExpiracionConfiguradas()
    {
        var options = new JwtOptions(
            SecureApiFactory.Issuer,
            SecureApiFactory.Audience,
            SecureApiFactory.SigningKey,
            30);
        var service = new JwtTokenService(options);
        var user = new DemoUser("usr_alice", "alice", "salt", "hash");
        var beforeCreation = DateTime.UtcNow;

        var response = service.CreateToken(user);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(1_800, response.ExpiresIn);
        Assert.InRange(
            response.ExpiresAtUtc,
            beforeCreation.AddMinutes(30),
            DateTime.UtcNow.AddMinutes(30));
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(SecureApiFactory.Issuer, token.Issuer);
        Assert.Contains(SecureApiFactory.Audience, token.Audiences);
        Assert.Equal("usr_alice", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("alice", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal("user", token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.False(string.IsNullOrWhiteSpace(
            token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value));
    }
}
