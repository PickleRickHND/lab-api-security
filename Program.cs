using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SecureApi.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Desactivar el encabezado generado por Kestrel para reducir el fingerprinting.
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Agregar servicios de controladores al contenedor DI
builder.Services.AddControllers();

// Opcional: Documentación Swagger/OpenAPI para pruebas en el laboratorio
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    const string bearerScheme = "Bearer";

    options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Ingrese solamente el access token, sin escribir la palabra Bearer."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(bearerScheme, document)] = []
    });
});

var jwtSection = builder.Configuration.GetRequiredSection(JwtOptions.SectionName);
var issuer = GetRequiredSetting(jwtSection, nameof(JwtOptions.Issuer));
var audience = GetRequiredSetting(jwtSection, nameof(JwtOptions.Audience));
var signingKey = GetRequiredSetting(jwtSection, nameof(JwtOptions.SigningKey));
var expirationMinutes = jwtSection.GetValue<int>(nameof(JwtOptions.ExpirationMinutes));

if (Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes.");
}

if (expirationMinutes <= 0)
{
    throw new InvalidOperationException("Jwt:ExpirationMinutes debe ser mayor que cero.");
}

var jwtOptions = new JwtOptions(issuer, audience, signingKey, expirationMinutes);

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Debe envolver Swagger, autenticación, rate limiting y endpoints para cubrir
// también las respuestas que finalizan el pipeline de forma anticipada.
app.UseMiddleware<SecureApi.Middleware.SecurityHeadersMiddleware>();

// Habilitar Swagger únicamente en desarrollo o entornos de laboratorio
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static string GetRequiredSetting(IConfiguration section, string name)
{
    var value = section[name];

    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"Configure Jwt:{name} antes de iniciar la API.")
        : value;
}

// Permite iniciar la aplicación mediante WebApplicationFactory en las pruebas.
public partial class Program { }
