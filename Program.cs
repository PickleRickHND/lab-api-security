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
builder.Services.AddSwaggerGen();

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

app.UseAuthorization();
app.MapControllers();

app.Run();

// Permite iniciar la aplicación mediante WebApplicationFactory en las pruebas.
public partial class Program { }
