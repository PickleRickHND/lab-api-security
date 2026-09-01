var builder = WebApplication.CreateBuilder(args);

// Agregar servicios de controladores al contenedor DI
builder.Services.AddControllers();

// Opcional: Documentación Swagger/OpenAPI para pruebas en el laboratorio
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Habilitar Swagger únicamente en desarrollo o entornos de laboratorio
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Desactivar el encabezado 'Server' para no revelar información del entorno (Security Hardening)
app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    await next();
});

app.UseAuthorization();
app.MapControllers();

app.Run();