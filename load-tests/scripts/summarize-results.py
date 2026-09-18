#!/usr/bin/env python3
"""Resume un .jtl de JMeter y el CSV de docker stats en un summary.md (Método USE)."""
import csv
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path


def pct(values, p):
    if not values:
        return 0
    values = sorted(values)
    k = max(0, min(len(values) - 1, int(round(p / 100 * len(values) + 0.5)) - 1))
    return values[k]


def to_mib(text):
    text = text.strip()
    for unit, factor in (("GiB", 1024), ("MiB", 1), ("kiB", 1 / 1024), ("KiB", 1 / 1024), ("B", 1 / 1024 / 1024)):
        if text.endswith(unit):
            return float(text[: -len(unit)]) * factor
    return float("nan")


def main(results_dir):
    results_dir = Path(results_dir)
    jtl = results_dir / "results.jtl"
    stats_csv = results_dir / "docker-stats.csv"
    out = results_dir / "summary.md"

    rows = [r for r in csv.DictReader(jtl.open()) if not r["label"].startswith("POST")]
    if not rows:
        sys.exit("El .jtl no contiene muestras del Thread Group principal")

    elapsed = [int(r["elapsed"]) for r in rows]
    latency = [int(r["Latency"]) for r in rows]
    ts = [int(r["timeStamp"]) for r in rows]
    duration_s = max(1e-9, (max(ts) + int(rows[-1]["elapsed"]) - min(ts)) / 1000)
    codes = Counter(r["responseCode"] for r in rows)
    failures = sum(1 for r in rows if r["success"] != "true")
    by_code = defaultdict(list)
    for r in rows:
        by_code[r["responseCode"]].append(int(r["elapsed"]))
    max_threads = max(int(r["allThreads"]) for r in rows)

    lines = [f"# Resumen de la corrida `{results_dir.name}`", ""]
    lines += ["## JMeter (equivalente al Aggregate Report)", "",
              "| # Samples | Average (ms) | Median (ms) | 90% Line | 95% Line | 99% Line | Min | Max | Error % | Throughput (req/s) | Hilos máx |",
              "|---|---|---|---|---|---|---|---|---|---|---|",
              f"| {len(rows)} | {statistics.mean(elapsed):.1f} | {statistics.median(elapsed):.0f} | {pct(elapsed, 90)} | {pct(elapsed, 95)} | {pct(elapsed, 99)} | {min(elapsed)} | {max(elapsed)} | {failures / len(rows) * 100:.2f}% | {len(rows) / duration_s:.2f} | {max_threads} |",
              "",
              f"Latencia (TTFB) media: {statistics.mean(latency):.1f} ms · Duración observada: {duration_s:.1f} s",
              "", "### Códigos de respuesta", "",
              "| Código | Muestras | % | Avg (ms) | 95% Line (ms) | Significado |", "|---|---|---|---|---|---|"]
    meaning = {"200": "Procesada por la API", "429": "Rechazada por Rate Limiting (defensa controlada)",
               "401": "Sin JWT válido", "500": "Error interno (fallo real)", "Non HTTP response code: java.net.ConnectException": "Conexión rechazada (caída)"}
    for code, n in sorted(codes.items(), key=lambda kv: -kv[1]):
        e = by_code[code]
        lines.append(f"| {code} | {n} | {n / len(rows) * 100:.1f}% | {statistics.mean(e):.1f} | {pct(e, 95)} | {meaning.get(code, '-')} |")
    lines.append("")

    if stats_csv.exists():
        srows = list(csv.DictReader(stats_csv.open()))
        srows = [r for r in srows if r["cpu_perc"].endswith("%")]
        if srows:
            cpu = [float(r["cpu_perc"].rstrip("%")) for r in srows]
            mem = [to_mib(r["mem_usage"]) for r in srows]
            memp = [float(r["mem_perc"].rstrip("%")) for r in srows]
            pids = [int(r["pids"]) for r in srows]
            lines += ["## docker stats (Método USE - Utilización)", "",
                      "| Métrica | Mínimo | Promedio | Máximo | Límite del contenedor |", "|---|---|---|---|---|",
                      f"| CPU % | {min(cpu):.2f}% | {statistics.mean(cpu):.2f}% | {max(cpu):.2f}% | 0.50 CPU (50% de un núcleo) |",
                      f"| Memoria | {min(mem):.1f} MiB | {statistics.mean(mem):.1f} MiB | {max(mem):.1f} MiB | {srows[0]['mem_limit']} |",
                      f"| Memoria % del límite | {min(memp):.2f}% | {statistics.mean(memp):.2f}% | {max(memp):.2f}% | 100% |",
                      f"| PIDs (hilos) | {min(pids)} | {statistics.mean(pids):.0f} | {max(pids)} | - |",
                      f"| Red acumulada (in / out) | - | - | {srows[-1]['net_in']} / {srows[-1]['net_out']} | - |",
                      f"| Disco acumulado (read / write) | - | - | {srows[-1]['block_in']} / {srows[-1]['block_out']} | - |",
                      "", f"Muestras de docker stats: {len(srows)} (cada ~2 s)", ""]

    out.write_text("\n".join(lines))
    print("\n".join(lines))


if __name__ == "__main__":
    main(sys.argv[1])
