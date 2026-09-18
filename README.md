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
| Rate Limiting | Implementado | 10 solicitudes por 10 segundos por IP; exceso rechazado con `429 Too Many Requests` |
| Pruebas de carga (Lab 3) | Implementado | Plan JMeter (50 usuarios / 30 s / 90 s) y scripts de ejecución con telemetría `docker stats` en `load-tests/` |
| JWT Bearer | Implementado | Login local, emisión HS256 y validación de firma, issuer, audience y expiración |
| Demostración BOLA/IDOR | Implementada | Ambos endpoints requieren JWT; uno omite intencionalmente el control de propietario |

## Tecnologías

- .NET 8 y ASP.NET Core Web API.
- Autenticación JWT Bearer con `Microsoft.AspNetCore.Authentication.JwtBearer`.
- Swagger/OpenAPI mediante Swashbuckle.
- Docker y Docker Compose.
- xUnit v3 y `Microsoft.AspNetCore.Mvc.Testing`.
- Pruebas E2E con Bash y cURL.

## Estructura principal

```text
Controllers/                       Endpoints de demostración
Middleware/                        Hardening de cabeceras HTTP
Security/                          Configuración y emisión de JWT
tests/SecureApi.Tests/             Pruebas automatizadas de integración
tests/e2e/                         Verificación sobre Kestrel real
docs/security-headers.md           Documento técnico del hardening
docs/jwt-authentication.md         Documento técnico de autenticación
Program.cs                         Configuración y pipeline de la API
Dockerfile                         Construcción multietapa de la imagen
docker-compose.yml                 Ejecución local del contenedor
```

## Endpoints disponibles

| Método | Ruta | Propósito |
| --- | --- | --- |
| `POST` | `/api/v1/auth/login` | Valida un usuario ficticio y emite un access token |
| `GET` | `/api/v1/documents/vulnerable/{id}` | Requiere JWT, pero demuestra una vulnerabilidad BOLA/IDOR |
| `GET` | `/api/v1/documents/secure/{id}` | Requiere JWT y valida el propietario mediante el claim `sub` |
| `GET` | `/api/v1/documents/whoami` | Muestra la identidad y los claims autenticados |
| `GET` | `/swagger/index.html` | Interfaz Swagger para explorar la API |

> [!WARNING]
> El endpoint `vulnerable` expone documentos sin validar propietario de forma
> intencional. Solo debe utilizarse con fines académicos y datos ficticios.

Usuarios ficticios disponibles:

| Usuario | Contraseña | Identidad (`sub`) |
| --- | --- | --- |
| `alice` | `Alice123!` | `usr_alice` |
| `bob` | `Bob123!` | `usr_bob` |
| `charlie` | `Charlie123!` | `usr_charlie` |

Swagger incluye el esquema **Bearer**. Primero se debe ejecutar el login, copiar
`accessToken`, presionar **Authorize** y pegar solamente el token.

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

El diseño de autenticación, los claims y las pruebas están documentados en
[docs/jwt-authentication.md](docs/jwt-authentication.md).

## Requisitos

Se puede ejecutar el laboratorio con cualquiera de estas opciones:

- Docker Desktop con Docker Compose, recomendada para reproducir .NET 8.
- SDK y runtime ASP.NET Core 8 para compilación y pruebas locales.
- cURL para las comprobaciones E2E.

## Ejecución con Docker

Crear la configuración local —el archivo `.env` está ignorado por Git—:

```bash
cp .env.example .env
openssl rand -base64 48
```

Copiar el valor generado después de `JWT_SIGNING_KEY=` en `.env`. La clave debe
tener al menos 32 bytes y Docker Compose rechazará el arranque si no existe.

```bash
docker compose up --detach --build
```

La API queda disponible en:

- Swagger: <http://localhost:8080/swagger/index.html>
- Login: <http://localhost:8080/api/v1/auth/login>

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

Ejecutar las pruebas con SDK y runtime .NET 8:

```bash
dotnet test tests/SecureApi.Tests/SecureApi.Tests.csproj
```

Un SDK posterior puede compilar `net8.0`, pero las pruebas de integración
requieren además el runtime ASP.NET Core 8 instalado en paralelo. Docker evita
esa dependencia local.

Verificar las cabeceras y el flujo JWT sobre el contenedor:

```bash
bash tests/e2e/verify-security-headers.sh
bash tests/e2e/verify-jwt-flow.sh
```

La suite cubre login válido e inválido, emisión y validación del JWT, firma,
issuer, audience, expiración, autorización por propietario, respuestas `200`,
`400`, `401`, `403`, `404`, Swagger y un contrato `429` que comprueba la
conservación de `Retry-After` y `RateLimit-*`.

## Consideraciones de seguridad

- Los documentos y usuarios incluidos son datos ficticios para el laboratorio.
- La clave JWT no se almacena en el repositorio; se exige mediante
  `Jwt__SigningKey` o `JWT_SIGNING_KEY` al usar Docker Compose.
- Esta implementación demuestra JWT Bearer con emisión local. No implementa un
  Authorization Server ni todos los flujos del protocolo OAuth 2.0.
- El rate limiting global protege también el endpoint de login frente a
  solicitudes excesivas.
- HSTS debe habilitarse únicamente cuando el despliegue utilice HTTPS/TLS real.
- Los artefactos `publish/` no se versionan; Docker los genera desde el código
  fuente para evitar ejecutar binarios desactualizados.
- Este proyecto no debe exponerse a Internet en su estado académico actual.

## Evidencia esperada

Para la entrega del laboratorio se deben conservar capturas que demuestren:

- Una respuesta `200` con las cabeceras defensivas.
- Ausencia de `Server` y `X-Powered-By`.
- Una respuesta `429` después de integrar Rate Limiting.
- Una respuesta `401` sin Bearer o con un token alterado.
- Una respuesta `200` al consultar el documento propio y `403` al consultar el
  documento de otro usuario mediante el endpoint seguro.
- Swagger funcionando después de aplicar la política de cabeceras.
