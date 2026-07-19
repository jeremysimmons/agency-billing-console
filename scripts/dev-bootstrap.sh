#!/usr/bin/env bash

set -euo pipefail

# ============================================================
# Local dev bootstrap
#
# Generates locally-trusted TLS certs so the app can serve
# https://localhost:3000, which is what the Google OAuth client
# expects for its redirect URI:
#   https://localhost:3000/auth/callback
# ============================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CERT_DIR="${REPO_ROOT}/certs"
HOST="localhost"
PORT="3000"

CERT_FILE="${CERT_DIR}/${HOST}.pem"
KEY_FILE="${CERT_DIR}/${HOST}-key.pem"

# ------------------------------------------------------------
# Ensure mkcert is installed
# ------------------------------------------------------------

if ! command -v mkcert >/dev/null 2>&1; then
  echo "mkcert not found."
  if command -v brew >/dev/null 2>&1; then
    echo "Installing mkcert (and nss for Firefox support) via Homebrew..."
    brew install mkcert nss
  else
    echo "Homebrew not found. Install mkcert manually: https://github.com/FiloSottile/mkcert" >&2
    exit 1
  fi
fi

# ------------------------------------------------------------
# Install the local CA (idempotent)
# ------------------------------------------------------------

echo "Ensuring mkcert local CA is installed..."
mkcert -install

# ------------------------------------------------------------
# Generate certs
# ------------------------------------------------------------

mkdir -p "${CERT_DIR}"

if [[ -f "${CERT_FILE}" && -f "${KEY_FILE}" ]]; then
  echo "Certs already exist:"
  echo "  ${CERT_FILE}"
  echo "  ${KEY_FILE}"
  echo "Delete them and re-run to regenerate."
else
  echo "Generating certs for ${HOST} / 127.0.0.1..."
  mkcert \
    -cert-file "${CERT_FILE}" \
    -key-file "${KEY_FILE}" \
    "${HOST}" 127.0.0.1 ::1
fi

# ------------------------------------------------------------
# Done
# ------------------------------------------------------------

echo
echo "Local TLS ready. Point your dev server at:"
echo "  cert: ${CERT_FILE}"
echo "  key:  ${KEY_FILE}"
echo
echo "Serve on https://${HOST}:${PORT} so the OAuth callback matches:"
echo "  https://${HOST}:${PORT}/auth/callback"
