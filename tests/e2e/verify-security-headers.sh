#!/usr/bin/env bash

set -euo pipefail

base_url="${1:-http://127.0.0.1:8080}"
headers_file="$(mktemp)"
body_file="$(mktemp)"

cleanup() {
  rm -f "$headers_file" "$body_file"
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
  local access_token="${4:-}"
  local status
  local curl_args=(
    --silent
    --show-error
    --dump-header "$headers_file"
    --output /dev/null
    --write-out '%{http_code}'
  )

  if [[ -n "$access_token" ]]; then
    curl_args+=(--header "Authorization: Bearer ${access_token}")
  fi

  status="$(curl "${curl_args[@]}" "${base_url}${path}")"

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

login_status="$(curl --silent --show-error \
  --dump-header "$headers_file" \
  --output "$body_file" \
  --write-out '%{http_code}' \
  --header 'Content-Type: application/json' \
  --data '{"username":"alice","password":"Alice123!"}' \
  "${base_url}/api/v1/auth/login")"

if [[ "$login_status" != "200" ]]; then
  echo "El login debía responder HTTP 200 y respondió ${login_status}." >&2
  exit 1
fi

access_token="$(sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p' "$body_file")"
if [[ -z "$access_token" ]]; then
  echo "El login no devolvió accessToken." >&2
  exit 1
fi

assert_header_absent "Server"
assert_header_absent "X-Powered-By"
assert_header "X-Content-Type-Options" "nosniff"
assert_header "Cache-Control" "no-store"
echo "/api/v1/auth/login: HTTP 200, token y cabeceras verificados."

verify_response \
  "/api/v1/documents/secure/1" \
  "401" \
  "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"

verify_response \
  "/api/v1/documents/vulnerable/1" \
  "200" \
  "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'" \
  "$access_token"

verify_response \
  "/api/v1/documents/vulnerable/999" \
  "404" \
  "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'" \
  "$access_token"

verify_response \
  "/swagger/index.html" \
  "200" \
  "frame-ancestors 'none'"
