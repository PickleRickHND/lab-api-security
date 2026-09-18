````markdown
# Rate Limiting

## Objetivo

Implementar una política de Rate Limiting para limitar el número de solicitudes que puede realizar un cliente y evitar el consumo excesivo de recursos de la API.

La política implementada utiliza la dirección IP del cliente como criterio de partición y rechaza las solicitudes que superan el límite establecido mediante HTTP `429 Too Many Requests`.

## Diseño

La API utiliza un limitador de ventana fija (`Fixed Window`) aplicado de forma global.

La política se configura con los siguientes parámetros:

- Límite: 10 solicitudes.
- Ventana: 10 segundos.
- Partición: dirección IP del cliente.
- Cola: 0 solicitudes.
- Reposición automática: habilitada.
- Código de rechazo: `429 Too Many Requests`.

Al utilizar una política global, el control se aplica a los endpoints de la API, incluido el endpoint de autenticación `/api/v1/auth/login`.

## Configuración

La política se registra mediante `AddRateLimiter` en `Program.cs`.

El limitador utiliza `PartitionedRateLimiter` para mantener un límite independiente por dirección IP:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext =>
        {
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: clientIp,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
````

El middleware se ejecuta antes de autenticación y autorización:

```csharp
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
```

Esto permite rechazar solicitudes excesivas antes de que continúen hacia los mecanismos de autenticación, autorización y los controladores.

## Desactivación temporal para pruebas de carga

La política puede desactivarse con la configuración `RateLimiting:Enabled`
(por defecto `true`). Con Docker Compose:

```bash
RATE_LIMITING_ENABLED=false docker compose up -d
```

La API registra una advertencia en el log al iniciar sin la defensa. Este modo
existe únicamente para el contraste del laboratorio de carga (`load-tests/`);
la suite de pruebas lo usa en el fixture compartido y verifica la política de
forma aislada en `RateLimitingTests`.

## Verificación manual

Para comprobar el límite configurado se pueden ejecutar 11 solicitudes consecutivas contra un endpoint protegido:

```bash
for i in {1..11}; do
  curl -s -o /dev/null -w "Solicitud $i : HTTP %{http_code}\n" \
    http://localhost:8080/api/v1/documents/whoami
done
```

Con el límite configurado, las primeras 10 solicitudes deben continuar hacia la aplicación y devolver `401 Unauthorized` al no incluir un token JWT.

La solicitud número 11 debe ser rechazada por el Rate Limiting y devolver:

```text
HTTP 429 Too Many Requests
```

Después de transcurridos los 10 segundos de la ventana, el límite se repone automáticamente y las solicitudes pueden volver a procesarse.

## Consideraciones de seguridad

* El Rate Limiting reduce el riesgo de abuso de recursos mediante solicitudes excesivas.
* Al utilizar una política global, el endpoint de login también queda protegido frente a solicitudes excesivas.
* La partición por dirección IP es adecuada para el escenario académico actual.
* La política utiliza una ventana fija, por lo que pueden producirse ráfagas de solicitudes alrededor del cambio de ventana.
* El endpoint de login continúa utilizando respuestas genéricas para credenciales inválidas.
* Esta configuración está orientada al laboratorio y debe adaptarse para escenarios productivos, especialmente cuando la API se encuentre detrás de proxies o balanceadores.

```
```
