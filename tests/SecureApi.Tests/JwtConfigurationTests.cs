using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SecureApi.Tests;

public sealed class JwtConfigurationTests
{
    [Fact]
    public void InicioFallaSinSigningKey()
    {
        var exception = StartApplication(signingKey: string.Empty, expirationMinutes: "30");

        Assert.Contains("Jwt:SigningKey", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InicioFallaConSigningKeyCorta()
    {
        var exception = StartApplication(signingKey: "clave-corta", expirationMinutes: "30");

        Assert.Contains("al menos 32 bytes", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InicioFallaConExpiracionNoPositiva()
    {
        var exception = StartApplication(
            signingKey: SecureApiFactory.SigningKey,
            expirationMinutes: "0");

        Assert.Contains("mayor que cero", exception.ToString(), StringComparison.Ordinal);
    }

    private static Exception StartApplication(string signingKey, string expirationMinutes)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Jwt:Issuer", SecureApiFactory.Issuer);
            builder.UseSetting("Jwt:Audience", SecureApiFactory.Audience);
            builder.UseSetting("Jwt:SigningKey", signingKey);
            builder.UseSetting("Jwt:ExpirationMinutes", expirationMinutes);
        });

        return Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
