#!/usr/bin/env bash

set -euo pipefail

base_url="${1:-http://127.0.0.1:8080}"
headers_file="$(mktemp)"

cleanup() {
  rm -f "$headers_file"
}

trap cleanup EXIT

assert_header() {
  local name="$1"
  local value="$2"

  if ! grep -Fqi "$name: $value" "$headers_file"; then
    echo "Falta la cabecera esperada: $name: $value" >&2
    return 1
  fi
}

assert_header_absent() {
  local name="$1"

  if grep -Eqi "^${name}:" "$headers_file"; then
    echo "La cabecera $name no debe publicarse." >&2
    return 1
  fi
}

verify_response() {
  local path="$1"
  local expected_status="$2"
  local policy="$3"
  local status

  status="$(curl --silent --show-error \
    --dump-header "$headers_file" \
    --output /dev/null \
    --write-out '%{http_code}' \
    "${base_url}${path}")"

  if [[ "$status" != "$expected_status" ]]; then
    echo "${path}: se esperaba HTTP ${expected_status} y se recibió ${status}." >&2
    return 1
  fi

  assert_header_absent "Server"
  assert_header_absent "X-Powered-By"
  assert_header "X-Content-Type-Options" "nosniff"
  assert_header "Referrer-Policy" "no-referrer"
  assert_header "X-Frame-Options" "DENY"
  assert_header "Permissions-Policy" "camera=(), geolocation=(), microphone=()"
  assert_header "Content-Security-Policy" "$policy"

  if [[ "$path" == /api/* ]]; then
    assert_header "Cache-Control" "no-store"
  fi

  echo "${path}: HTTP ${status}, cabeceras verificadas."
}

verify_response \
  "/api/v1/documents/vulnerable/1" \
  "200" \
  "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"

verify_response \
  "/api/v1/documents/vulnerable/999" \
  "404" \
  "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"

verify_response \
  "/swagger/index.html" \
  "200" \
  "frame-ancestors 'none'"
