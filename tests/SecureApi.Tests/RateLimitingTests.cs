using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SecureApi.Tests;

public sealed class RateLimitingTests
{
    // La política global permite 10 solicitudes por ventana de 10 segundos.
    private const int PermitLimit = 10;

    [Fact]
    public async Task PorDefectoLaSolicitudQueExcedeElLimiteRecibe429()
    {
        using var factory = CreateFactory(rateLimitingEnabled: null);
        using var client = factory.CreateClient();

        var statusCodes = await SendBurstAsync(client, PermitLimit + 1);

        Assert.All(statusCodes.Take(PermitLimit),
            status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[PermitLimit]);
    }

    [Fact]
    public async Task ConRateLimitingDesactivadoNuncaResponde429()
    {
        using var factory = CreateFactory(rateLimitingEnabled: "false");
        using var client = factory.CreateClient();

        var statusCodes = await SendBurstAsync(client, PermitLimit * 3);

        Assert.All(statusCodes, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
    }

    private static WebApplicationFactory<Program> CreateFactory(string? rateLimitingEnabled)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:Issuer", SecureApiFactory.Issuer);
            builder.UseSetting("Jwt:Audience", SecureApiFactory.Audience);
            builder.UseSetting("Jwt:SigningKey", SecureApiFactory.SigningKey);
            builder.UseSetting("Jwt:ExpirationMinutes", "30");

            if (rateLimitingEnabled is not null)
            {
                builder.UseSetting("RateLimiting:Enabled", rateLimitingEnabled);
            }
        });
    }

    // Sin JWT el endpoint responde 401; si el limitador actúa antes, responde 429.
    private static async Task<List<HttpStatusCode>> SendBurstAsync(HttpClient client, int count)
    {
        var statusCodes = new List<HttpStatusCode>(count);

        for (var i = 0; i < count; i++)
        {
            using var response = await client.GetAsync(
                "/api/v1/documents/whoami",
                TestContext.Current.CancellationToken);
            statusCodes.Add(response.StatusCode);
        }

        return statusCodes;
    }
}
