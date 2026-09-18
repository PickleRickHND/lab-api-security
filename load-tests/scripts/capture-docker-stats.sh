#!/usr/bin/env bash
# Muestrea `docker stats` de un contenedor cada N segundos y lo guarda en CSV.
# Uso: capture-docker-stats.sh <contenedor> <salida.csv> <segundos_totales> [intervalo=2]
set -euo pipefail

container="${1:?contenedor}"
output="${2:?archivo csv de salida}"
total_seconds="${3:?segundos totales}"
interval="${4:-1}"

echo "timestamp,cpu_perc,mem_usage,mem_limit,mem_perc,net_in,net_out,block_in,block_out,pids" > "$output"

end=$(( $(date +%s) + total_seconds ))
while [ "$(date +%s)" -lt "$end" ]; do
  # Formato crudo: "12.34%|10.5MiB / 256MiB|4.10%|1.2kB / 3.4kB|0B / 0B|30"
  raw=$(docker stats "$container" --no-stream \
    --format '{{.CPUPerc}}|{{.MemUsage}}|{{.MemPerc}}|{{.NetIO}}|{{.BlockIO}}|{{.PIDs}}' 2>/dev/null || true)
  if [ -n "$raw" ]; then
    IFS='|' read -r cpu mem memp net blk pids <<< "$raw"
    mem_usage="${mem%% / *}"; mem_limit="${mem##* / }"
    net_in="${net%% / *}";    net_out="${net##* / }"
    blk_in="${blk%% / *}";    blk_out="${blk##* / }"
    echo "$(date '+%Y-%m-%dT%H:%M:%S'),$cpu,$mem_usage,$mem_limit,$memp,$net_in,$net_out,$blk_in,$blk_out,$pids" >> "$output"
  fi
  sleep "$interval"
done
