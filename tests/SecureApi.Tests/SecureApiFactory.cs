using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureApi.Security;

namespace SecureApi.Tests;

public sealed class SecureApiFactory : WebApplicationFactory<Program>
{
    internal const string Issuer = "SecureApi.Tests";
    internal const string Audience = "SecureApi.Tests.Client";
    internal const string SigningKey =
        "TEST_ONLY_SIGNING_KEY_64_BYTES_LONG_1234567890_ABCDEFGHIJKLMN";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:ExpirationMinutes", "30");

        // En TestServer todas las solicitudes comparten la partición "unknown";
        // el limitador se prueba de forma aislada en RateLimitingTests.
        builder.UseSetting("RateLimiting:Enabled", "false");
    }

    internal HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    internal async Task<HttpClient> CreateAuthenticatedClientAsync(
        string username = "alice",
        string password = "Alice123!",
        CancellationToken cancellationToken = default)
    {
        var client = CreateApiClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username, password },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (token is null)
        {
            client.Dispose();
            throw new InvalidOperationException("El login de pruebas no devolvió un token.");
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.AccessToken);

        return client;
    }
}
