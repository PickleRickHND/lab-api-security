# Laboratorio de seguridad defensiva para APIs

Proyecto académico de la Universidad Tecnológica Centroamericana (UNITEC) para
la asignatura **Arquitectura de Sistemas Informáticos**. El laboratorio aplica
controles de seguridad defensiva sobre una API ASP.NET Core y demuestra cómo
reducir exposición de información, abuso de recursos y accesos no autorizados.

## Objetivos del laboratorio

El ejercicio está dividido en tres áreas principales:

1. **Hardening de cabeceras HTTP:** reducir el fingerprinting del servidor y
   aplicar cabeceras defensivas a respuestas exitosas y de error.
2. **Rate Limiting:** limitar solicitudes para proteger los recursos del
   servidor y responder con `429 Too Many Requests` cuando corresponda.
3. **OAuth 2.0 y JWT:** autenticar usuarios y proteger los endpoints expuestos.

## Estado actual

| Área | Estado | Observación |
| --- | --- | --- |
| Cabeceras HTTP | Implementado | Incluye pruebas de API, Swagger, errores y contrato `429` |
| Docker | Implementado | La imagen se compila desde el código fuente actual |
| Rate Limiting | Pendiente de integración | Debe aportar la respuesta `429` real |
| OAuth 2.0/JWT | Pendiente de integración | La identidad de usuario todavía es simulada |
| Demostración BOLA/IDOR | Incluida | El endpoint vulnerable existe intencionalmente |

## Tecnologías

- .NET 8 y ASP.NET Core Web API.
- Swagger/OpenAPI mediante Swashbuckle.
- Docker y Docker Compose.
- xUnit v3 y `Microsoft.AspNetCore.Mvc.Testing`.
- Pruebas E2E con Bash y cURL.

## Estructura principal

```text
Controllers/                       Endpoints de demostración
Middleware/                        Hardening de cabeceras HTTP
tests/SecureApi.Tests/             Pruebas automatizadas de integración
tests/e2e/                         Verificación sobre Kestrel real
docs/security-headers.md           Documento técnico del hardening
Program.cs                         Configuración y pipeline de la API
Dockerfile                         Construcción multietapa de la imagen
docker-compose.yml                 Ejecución local del contenedor
```

## Endpoints disponibles

| Método | Ruta | Propósito |
| --- | --- | --- |
| `GET` | `/api/v1/documents/vulnerable/{id}` | Demuestra una vulnerabilidad BOLA/IDOR |
| `GET` | `/api/v1/documents/secure/{id}` | Demuestra validación de propietario con identidad simulada |
| `GET` | `/swagger/index.html` | Interfaz Swagger para explorar la API |

> [!WARNING]
> El endpoint `vulnerable` expone documentos sin validar propietario de forma
> intencional. Solo debe utilizarse con fines académicos y datos ficticios.

## Cabeceras implementadas

La API elimina `Server` y `X-Powered-By` y aplica, según el tipo de respuesta:

- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: no-referrer`
- `X-Frame-Options: DENY`
- `Permissions-Policy`
- `Content-Security-Policy`
- `Cache-Control: no-store` para rutas `/api`

La explicación completa está en
[docs/security-headers.md](docs/security-headers.md).

## Requisitos

Se puede ejecutar el laboratorio con cualquiera de estas opciones:

- Docker Desktop con Docker Compose, recomendada para reproducir .NET 8.
- SDK .NET 8 o posterior para compilación local.
- cURL para las comprobaciones E2E.

## Ejecución con Docker

```bash
docker compose up --detach --build
```

La API queda disponible en:

- Swagger: <http://localhost:8080/swagger/index.html>
- API: <http://localhost:8080/api/v1/documents/vulnerable/1>

Si el puerto 8080 está ocupado:

```bash
SECURE_API_HOST_PORT=18080 docker compose up --detach --build
```

Para detener y retirar el contenedor:

```bash
docker compose down
```

## Compilación y pruebas

Compilar el proyecto:

```bash
dotnet restore
dotnet build SecureApi.csproj --configuration Release
```

Ejecutar las pruebas con SDK .NET 8 o 9:

```bash
dotnet test tests/SecureApi.Tests/SecureApi.Tests.csproj
```

Con SDK .NET 10 o posterior:

```bash
dotnet test --project tests/SecureApi.Tests/SecureApi.Tests.csproj
```

Verificar las cabeceras sobre el contenedor:

```bash
bash tests/e2e/verify-security-headers.sh
```

La suite actual cubre respuestas `200`, `400`, `403`, `404`, Swagger y un
contrato de respuesta `429` que comprueba la conservación de `Retry-After` y
`RateLimit-*`.

## Consideraciones de seguridad

- Los documentos y usuarios incluidos son datos ficticios para el laboratorio.
- La ruta `secure` no sustituye una implementación real de OAuth/JWT mientras
  conserve la identidad simulada.
- HSTS debe habilitarse únicamente cuando el despliegue utilice HTTPS/TLS real.
- Los artefactos `publish/` no se versionan; Docker los genera desde el código
  fuente para evitar ejecutar binarios desactualizados.
- Este proyecto no debe exponerse a Internet en su estado académico actual.

## Evidencia esperada

Para la entrega del laboratorio se deben conservar capturas que demuestren:

- Una respuesta `200` con las cabeceras defensivas.
- Ausencia de `Server` y `X-Powered-By`.
- Una respuesta `429` después de integrar Rate Limiting.
- Swagger funcionando después de aplicar la política de cabeceras.
