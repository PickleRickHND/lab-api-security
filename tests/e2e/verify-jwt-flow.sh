#!/usr/bin/env bash

set -euo pipefail

base_url="${1:-http://127.0.0.1:8080}"
body_file="$(mktemp)"

cleanup() {
  rm -f "$body_file"
}

trap cleanup EXIT

request_status() {
  local path="$1"
  local expected_status="$2"
  local access_token="${3:-}"
  local curl_args=(
    --silent
    --show-error
    --output "$body_file"
    --write-out '%{http_code}'
  )

  if [[ -n "$access_token" ]]; then
    curl_args+=(--header "Authorization: Bearer ${access_token}")
  fi

  local status
  status="$(curl "${curl_args[@]}" "${base_url}${path}")"
  if [[ "$status" != "$expected_status" ]]; then
    echo "${path}: se esperaba HTTP ${expected_status} y se recibió ${status}." >&2
    return 1
  fi

  echo "${path}: HTTP ${status}."
}

login_status="$(curl --silent --show-error \
  --output "$body_file" \
  --write-out '%{http_code}' \
  --header 'Content-Type: application/json' \
  --data '{"username":"alice","password":"Alice123!"}' \
  "${base_url}/api/v1/auth/login")"

if [[ "$login_status" != "200" ]]; then
  echo "El login válido debía responder HTTP 200 y respondió ${login_status}." >&2
  exit 1
fi

access_token="$(sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p' "$body_file")"
if [[ -z "$access_token" ]]; then
  echo "El login válido no devolvió accessToken." >&2
  exit 1
fi

echo "/api/v1/auth/login: HTTP 200 y token recibido."

invalid_login_status="$(curl --silent --show-error \
  --output "$body_file" \
  --write-out '%{http_code}' \
  --header 'Content-Type: application/json' \
  --data '{"username":"alice","password":"incorrecta"}' \
  "${base_url}/api/v1/auth/login")"

if [[ "$invalid_login_status" != "401" ]]; then
  echo "El login inválido debía responder HTTP 401 y respondió ${invalid_login_status}." >&2
  exit 1
fi

request_status "/api/v1/documents/secure/1" "401"
request_status "/api/v1/documents/secure/1" "200" "$access_token"
request_status "/api/v1/documents/secure/2" "403" "$access_token"
request_status "/api/v1/documents/vulnerable/2" "200" "$access_token"

IFS='.' read -r token_header token_payload token_signature <<<"$access_token"
replacement="A"
if [[ "${token_payload:0:1}" == "A" ]]; then
  replacement="B"
fi
tampered_token="${token_header}.${replacement}${token_payload:1}.${token_signature}"

request_status "/api/v1/documents/secure/1" "401" "$tampered_token"

echo "Flujo JWT Bearer verificado."
