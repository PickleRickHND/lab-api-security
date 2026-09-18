# Pruebas de carga (Laboratorio 3)

Plan de Apache JMeter y scripts para ejecutar la prueba de carga y estrés del
laboratorio 3 contra la API con Rate Limiting y recolectar la telemetría de
`docker stats` para el análisis con el Método USE.

## Estructura del plan `jmeter/lab3-rate-limit.jmx`

```
Test Plan: Lab 3 - Carga y estrés con Rate Limiting (Secure API)
├── Variables de usuario: HOST, PORT, ENDPOINT, THREADS, RAMPUP, DURATION, THINK_TIME_MS
├── setUp Thread Group - Login JWT (alice)            (1 hilo, 1 iteración)
│   └── POST /api/v1/auth/login
│       ├── HTTP Header Manager - JSON                 (Content-Type: application/json)
│       ├── JSON Extractor - accessToken               ($.accessToken)
│       ├── JSR223 PostProcessor                       (publica ACCESS_TOKEN como propiedad global)
│       └── Response Assertion - login 200
└── Thread Group - 50 usuarios / ramp-up 30 s / 90 s   (loop infinito + duración)
    ├── GET ${ENDPOINT}                                (/api/v1/documents/secure/1)
    │   ├── HTTP Header Manager - Bearer JWT           (Authorization: Bearer ${__P(ACCESS_TOKEN)})
    │   ├── Response Assertion - código 200 o 429      (Ignore Status activado)
    │   └── Constant Timer - 1000 ms
    ├── Aggregate Report
    └── Summary Report
```

Los endpoints de documentos exigen JWT, por eso el plan agrega un `setUp Thread
Group` que hace login una sola vez y comparte el token con los 50 hilos. El
token dura 30 minutos, suficiente para la prueba de 90 s.

*Ignore Status* en la Response Assertion es obligatorio: JMeter marca cualquier
4xx como error; con esa opción el `429` del Rate Limiting cuenta como bloqueo
controlado y cualquier otro código (`401`, `500`) sí se reporta como error.

Todos los parámetros se pueden sobreescribir desde la línea de comandos:

| Propiedad | Default | Ejemplo |
| --- | --- | --- |
| `host` / `port` | `localhost` / `8080` | `-Jport=5010` |
| `endpoint` | `/api/v1/documents/secure/1` | `-Jendpoint=/api/v1/documents/whoami` |
| `threads` / `rampup` / `duration` | `50` / `30` / `90` | `-Jthreads=100` |
| `thinktime` | `1000` ms | `-Jthinktime=0` (estrés sin pausa) |

## Ejecución

Requiere la API corriendo (`docker compose up -d`) y JMeter instalado
(`brew install jmeter`).

```bash
./load-tests/scripts/run-load-test.sh con-rate-limit
```

El script:

1. Toma una muestra basal de `docker stats`.
2. Lanza `capture-docker-stats.sh` en segundo plano (CSV cada ~2 s).
3. Ejecuta JMeter en modo headless (`-n`) y genera el dashboard HTML.
4. Registra el estado del contenedor y los errores en sus logs tras la prueba.
5. Genera `summary.md` con el equivalente al Aggregate Report, el desglose por
   código de respuesta y las métricas de `docker stats`.

Salida en `/tmp/lab-api-security-load-tests/<etiqueta>-<fecha>/` (fuera del
repositorio; `RESULTS_DIR` cambia la ubicación):

| Archivo | Contenido |
| --- | --- |
| `summary.md` | Aggregate Report + códigos de respuesta + docker stats |
| `docker-stats.csv`, `docker-stats-baseline.txt` | Telemetría del contenedor |
| `container-state-after.txt`, `jmeter-console.txt`, `jmeter.log` | Estado final y salida de JMeter |
| `results.jtl` | Muestras crudas (hasta 177 MB en estrés) |
| `dashboard/` | Dashboard HTML (regenerable con `jmeter -g results.jtl -o dashboard -Jjmeter.reportgenerator.overall_granularity=1000`) |

Variables de entorno del script: `THREADS`, `RAMPUP`, `DURATION`, `PORT`,
`CONTAINER`, `RESULTS_DIR`. Cualquier argumento extra se pasa a JMeter (`-Jthinktime=0`).

## Corrida sin Rate Limiting (Plus)

```bash
RATE_LIMITING_ENABLED=false docker compose up -d
./load-tests/scripts/run-load-test.sh sin-rate-limit
docker compose up -d   # vuelve a activar la defensa
```

## Capturas para la entrega

Abrir el plan en la GUI (`jmeter -t load-tests/jmeter/lab3-rate-limit.jmx`),
capturar la jerarquía del árbol, ejecutar con Play y capturar el Aggregate
Report y el Summary Report con `docker stats secure_api_lab` visible en una
terminal. Durante la prueba debe estar corriendo únicamente `secure_api_lab`
para que la telemetría corresponda a la API.
