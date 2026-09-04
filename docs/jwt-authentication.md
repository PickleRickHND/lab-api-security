# Autenticación JWT Bearer

## Objetivo

Autenticar usuarios ficticios, emitir access tokens firmados y proteger los
endpoints de documentos sin perder el hardening HTTP del laboratorio. El alcance
es JWT Bearer con un emisor local; no se implementa un Authorization Server
OAuth 2.0 completo.

## Diseño

1. `POST /api/v1/auth/login` valida la contraseña mediante PBKDF2-SHA256 con
   100 000 iteraciones y comparación en tiempo constante.
2. `JwtTokenService` emite tokens HS256 con los claims `sub`, `unique_name`,
   `jti` y `role`.
3. El middleware JWT valida firma, issuer, audience y expiración con un margen
   máximo de un minuto.
4. `[Authorize]` exige un Bearer válido para todos los endpoints de documentos.
5. El endpoint `secure` compara `sub` con `OwnerId`; el endpoint `vulnerable`
   omite intencionalmente esa comprobación para demostrar BOLA/IDOR.

## Configuración

`appsettings.json` contiene únicamente valores no secretos:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:ExpirationMinutes`

La clave se exige mediante `Jwt__SigningKey`. Para Docker Compose se obtiene de
`JWT_SIGNING_KEY` en el archivo local `.env`, ignorado por Git:

```bash
cp .env.example .env
openssl rand -base64 48
```

No se debe reutilizar la clave de pruebas ni publicar el contenido de `.env`.

## Flujo manual

```bash
curl -sS -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"alice","password":"Alice123!"}'
```

Copiar `accessToken` y enviarlo como Bearer:

```bash
ACCESS_TOKEN='PEGUE_AQUI_EL_TOKEN'
curl -i http://localhost:8080/api/v1/documents/secure/1 \
  --oauth2-bearer "$ACCESS_TOKEN"
```

Con Alice, `/secure/1` responde `200` y `/secure/2` responde `403`. Sin token o
con una firma inválida, ambos endpoints protegidos responden `401`.

## Verificación automatizada

```bash
dotnet test tests/SecureApi.Tests/SecureApi.Tests.csproj
docker compose up --detach --build
bash tests/e2e/verify-security-headers.sh
bash tests/e2e/verify-jwt-flow.sh
docker compose down
```

El login es un endpoint anónimo con trabajo criptográfico deliberadamente
costoso. La política de rate limiting se incorporará con el punto b y deberá
aplicarse especialmente a esta ruta.
