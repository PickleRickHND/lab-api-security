using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SecureApi.Security;
using Xunit;

namespace SecureApi.Tests;

public sealed class JwtAuthenticationTests : IClassFixture<SecureApiFactory>
{
    private const string OtherSigningKey =
        "OTHER_TEST_SIGNING_KEY_64_BYTES_LONG_1234567890_ABCDEFGHIJKLM";

    private readonly SecureApiFactory _factory;
    private readonly HttpClient _client;

    public JwtAuthenticationTests(SecureApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateApiClient();
    }

    [Fact]
    public async Task LoginValidoEmiteAccessTokenConClaimsEsperados()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = "alice", password = "Alice123!" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokenResponse = Assert.IsType<TokenResponse>(
            await response.Content.ReadFromJsonAsync<TokenResponse>(
                TestContext.Current.CancellationToken));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.AccessToken);

        Assert.Equal("Bearer", tokenResponse.TokenType);
        Assert.Equal(1_800, tokenResponse.ExpiresIn);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(SecureApiFactory.Issuer, token.Issuer);
        Assert.Contains(SecureApiFactory.Audience, token.Audiences);
        Assert.Equal("usr_alice", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Theory]
    [InlineData("alice", "incorrecta")]
    [InlineData("usuario-inexistente", "incorrecta")]
    public async Task LoginConCredencialesInvalidasRespondeUnauthorized(
        string username,
        string password)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username, password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointProtegidoSinTokenRespondeUnauthorized()
    {
        using var response = await _client.GetAsync(
            "/api/v1/documents/secure/1",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task DocumentoSeguroAutorizaPropietarioYRechazaOtroUsuario()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        using var ownDocument = await client.GetAsync(
            "/api/v1/documents/secure/1",
            TestContext.Current.CancellationToken);
        using var otherDocument = await client.GetAsync(
            "/api/v1/documents/secure/2",
            TestContext.Current.CancellationToken);
        using var vulnerableDocument = await client.GetAsync(
            "/api/v1/documents/vulnerable/2",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ownDocument.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherDocument.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vulnerableDocument.StatusCode);
    }

    [Fact]
    public async Task TokenConFirmaIncorrectaRespondeUnauthorized()
    {
        var token = CreateToken(signingKey: OtherSigningKey);

        using var response = await SendAuthenticatedRequestAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenConIssuerIncorrectoRespondeUnauthorized()
    {
        var token = CreateToken(issuer: "Issuer.Incorrecto");

        using var response = await SendAuthenticatedRequestAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenConAudienceIncorrectaRespondeUnauthorized()
    {
        var token = CreateToken(audience: "Audience.Incorrecta");

        using var response = await SendAuthenticatedRequestAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenExpiradoRespondeUnauthorized()
    {
        var token = CreateToken(expiresAt: DateTime.UtcNow.AddMinutes(-2));

        using var response = await SendAuthenticatedRequestAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenSinSubjectRespondeForbiddenEnControlDePropietario()
    {
        var token = CreateToken(includeSubject: false);

        using var response = await SendAuthenticatedRequestAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedRequestAsync(string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/documents/secure/1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string CreateToken(
        string? issuer = null,
        string? audience = null,
        string? signingKey = null,
        DateTime? expiresAt = null,
        bool includeSubject = true)
    {
        var expiration = expiresAt ?? DateTime.UtcNow.AddMinutes(5);
        var claims = includeSubject
            ? new[] { new Claim(JwtRegisteredClaimNames.Sub, "usr_alice") }
            : [];
        var token = new JwtSecurityToken(
            issuer: issuer ?? SecureApiFactory.Issuer,
            audience: audience ?? SecureApiFactory.Audience,
            claims: claims,
            notBefore: expiration.AddMinutes(-5),
            expires: expiration,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey ?? SecureApiFactory.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
