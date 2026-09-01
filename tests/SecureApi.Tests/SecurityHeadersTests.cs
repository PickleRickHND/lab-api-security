using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureApi.Middleware;
using Xunit;

namespace SecureApi.Tests;

public sealed class SecurityHeadersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private const string BrowserContentSecurityPolicy = "frame-ancestors 'none'";

    private const string PermissionsPolicy = "camera=(), geolocation=(), microphone=()";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SecurityHeadersTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/api/v1/documents/vulnerable/1", HttpStatusCode.OK)]
    [InlineData("/api/v1/documents/secure/2", HttpStatusCode.Forbidden)]
    [InlineData("/api/v1/documents/vulnerable/999", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/documents/vulnerable/no-es-entero", HttpStatusCode.BadRequest)]
    public async Task ApiIncluyeCabecerasDeSeguridadEnTodosLosResultados(
        string path,
        HttpStatusCode expectedStatus)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertGlobalSecurityHeaders(response);
        AssertHeader(response, "Content-Security-Policy", ApiContentSecurityPolicy);
        AssertHeader(response, "Cache-Control", "no-store");
    }

    [Theory]
    [InlineData("/ruta-inexistente", HttpStatusCode.NotFound)]
    [InlineData("/swagger/index.html", HttpStatusCode.OK)]
    [InlineData("/swagger/v1/swagger.json", HttpStatusCode.OK)]
    public async Task RespuestasNoApiIncluyenSoloCabecerasGlobales(
        string path,
        HttpStatusCode expectedStatus)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertGlobalSecurityHeaders(response);
        AssertHeader(
            response,
            "Content-Security-Policy",
            BrowserContentSecurityPolicy);
    }

    [Fact]
    public void KestrelNoPublicaLaCabeceraServer()
    {
        var options = _factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.False(options.AddServerHeader);
    }

    [Fact]
    public async Task ComportamientoIntencionalDelLaboratorioPermaneceIntacto()
    {
        using var response = await _client.GetAsync(
            "/api/v1/documents/vulnerable/1",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(document);
        Assert.Equal("Nomina_Confidencial.pdf", document.Title);
        Assert.Equal("usr_alice", document.OwnerId);
    }

    [Fact]
    public async Task CabecerasFuncionalesSeConservanEnRespuesta429()
    {
        var builder = new WebHostBuilder().Configure(app =>
        {
            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                context.Response.Headers["RateLimit-Remaining"] = "0";
                await context.Response.WriteAsync(
                    "Límite excedido",
                    TestContext.Current.CancellationToken);
            });
        });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();
        using var response = await client.GetAsync(
            "/api/recurso-limitado",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        AssertHeader(response, "Retry-After", "60");
        AssertHeader(response, "RateLimit-Remaining", "0");
        AssertGlobalSecurityHeaders(response);
        AssertHeader(response, "Content-Security-Policy", ApiContentSecurityPolicy);
        AssertHeader(response, "Cache-Control", "no-store");
    }

    private static void AssertGlobalSecurityHeaders(HttpResponseMessage response)
    {
        AssertHeaderAbsent(response, "Server");
        AssertHeaderAbsent(response, "X-Powered-By");
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Permissions-Policy", PermissionsPolicy);
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Falta la cabecera {name}.");
        Assert.Equal(expectedValue, Assert.Single(values));
    }

    private static void AssertHeaderAbsent(HttpResponseMessage response, string name)
    {
        Assert.False(response.Headers.Contains(name), $"La cabecera {name} no debe publicarse.");
    }

    private sealed record DocumentResponse(int Id, string OwnerId, string Title, string Content);
}
