#!/usr/bin/env bash
# Ejecuta el plan de JMeter en modo headless, captura `docker stats` en paralelo
# y genera dashboard HTML + summary.md.
# Uso: run-load-test.sh <etiqueta> [propiedades JMeter extra, ej. -Jthinktime=0]
# Requiere la API corriendo: docker compose up -d
# Salida: /tmp/lab-api-security-load-tests/<etiqueta>-<fecha>/ (o RESULTS_DIR)
set -euo pipefail

label="${1:?etiqueta de la corrida (ej. con-rate-limit)}"; shift || true
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
plan="$root/load-tests/jmeter/lab3-rate-limit.jmx"
container="${CONTAINER:-secure_api_lab}"
duration="${DURATION:-90}"
rampup="${RAMPUP:-30}"
threads="${THREADS:-50}"
port="${PORT:-8080}"

stamp="$(date '+%Y%m%d-%H%M%S')"
# Los resultados y logs se escriben fuera del repositorio (RESULTS_DIR para cambiarlo).
out="${RESULTS_DIR:-/tmp/lab-api-security-load-tests}/${label}-${stamp}"
mkdir -p "$out"

if ! curl -sf -o /dev/null "http://localhost:${port}/swagger/index.html"; then
  echo "La API no responde en :${port}. Ejecute: docker compose up -d" >&2
  exit 1
fi

echo "Corrida: $label"
echo "Salida:  $out"
echo "Contenedor monitoreado: $container ($(docker inspect -f '{{.State.Status}}' "$container"))"

# Muestra basal antes de la carga (para comparar en el informe USE).
docker stats "$container" --no-stream > "$out/docker-stats-baseline.txt"

# Captura de docker stats durante toda la prueba (+10 s de margen).
"$here/capture-docker-stats.sh" "$container" "$out/docker-stats.csv" $(( duration + 12 )) 2 &
stats_pid=$!

jmeter -n -t "$plan" \
  -l "$out/results.jtl" \
  -j "$out/jmeter.log" \
  -e -o "$out/dashboard" \
  -Jport="$port" -Jthreads="$threads" -Jrampup="$rampup" -Jduration="$duration" \
  "$@" | tee "$out/jmeter-console.txt"

wait "$stats_pid" || true

# Verificación del estado del contenedor tras la carga (pilar Errores del método USE).
docker inspect -f 'Estado tras la prueba: {{.State.Status}} (reinicios: {{.RestartCount}})' "$container" | tee "$out/container-state-after.txt"
# grep -c devuelve 1 cuando el conteo es 0; no es un error para este script.
error_lines=$(docker logs --since "$(( duration + 30 ))s" "$container" 2>&1 | { grep -ciE 'fail|exception|error' || true; })
echo "Líneas con fail|exception|error en logs del contenedor: ${error_lines}" | tee -a "$out/container-state-after.txt"

python3 "$here/summarize-results.py" "$out"
echo
echo "Dashboard HTML: $out/dashboard/index.html"
