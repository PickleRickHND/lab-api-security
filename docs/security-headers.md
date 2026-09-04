# Hardening de cabeceras HTTP

## Objetivo

Reducir el fingerprinting del servidor y aplicar una política consistente de
cabeceras defensivas a la API, Swagger y respuestas de error. El control cubre
actualmente respuestas de autenticación `200`, `401` y `403`, y debe seguir
funcionando cuando se integre rate limiting.

## Diseño

La protección tiene dos capas:

1. Kestrel usa `AddServerHeader = false` para no producir `Server: Kestrel`.
2. `SecurityHeadersMiddleware` se registra antes de Swagger y de los demás
   componentes. Mediante `Response.OnStarting` elimina `X-Powered-By` y aplica
   las cabeceras justo antes de enviar la respuesta.

Docker es la ruta de despliegue soportada por el laboratorio. Los artefactos
`publish/` no se versionan: el Dockerfile multietapa publica siempre desde el
código fuente actual y evita ejecutar un DLL desactualizado.

El middleware solo modifica las cabeceras de seguridad definidas. No elimina
`Retry-After`, `RateLimit-*`, `WWW-Authenticate` ni otras cabeceras funcionales.

## Política aplicada

| Alcance | Cabecera | Valor |
| --- | --- | --- |
| Global | `X-Content-Type-Options` | `nosniff` |
| Global | `Referrer-Policy` | `no-referrer` |
| Global | `X-Frame-Options` | `DENY` |
| Global | `Permissions-Policy` | `camera=(), geolocation=(), microphone=()` |
| Global no API | `Content-Security-Policy` | `frame-ancestors 'none'` |
| `/api` | `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'` |
| `/api` | `Cache-Control` | `no-store` |

`Strict-Transport-Security` no se envía todavía porque el laboratorio publica
HTTP local en el puerto 8080. Debe habilitarse únicamente cuando exista TLS real
en Kestrel o en un proxy confiable.

## Verificación automatizada

Pruebas de integración:

```bash
# SDK y runtime ASP.NET Core 8
dotnet test tests/SecureApi.Tests/SecureApi.Tests.csproj
```

Prueba E2E sobre Kestrel dentro del contenedor:

```bash
cp .env.example .env
# Asignar a JWT_SIGNING_KEY el resultado de: openssl rand -base64 48
docker compose up --detach --build
bash tests/e2e/verify-security-headers.sh
bash tests/e2e/verify-jwt-flow.sh
docker compose down
```

Si el puerto 8080 está ocupado, se puede cambiar solo el puerto del host:

```bash
SECURE_API_HOST_PORT=18080 docker compose up --detach --build
bash tests/e2e/verify-security-headers.sh http://127.0.0.1:18080
docker compose down
```

Las pruebas E2E comprueban respuestas `200`, `401`, `403`, `404`, tokens
alterados y Swagger. Cuando el módulo de rate limiting esté integrado, se debe
agregar un caso `429` que además confirme que `Retry-After` o las cabeceras
`RateLimit-*` se conservan.

## Evidencia para el laboratorio

Para las capturas, ejecutar:

Obtener primero un token como se describe en
[jwt-authentication.md](jwt-authentication.md) y enviarlo como Bearer al capturar
las respuestas de documentos.

La captura `429` queda condicionada a la integración del módulo de rate
limiting. Las imágenes deben mostrar las cabeceras defensivas y la ausencia de
`Server` y `X-Powered-By`.
