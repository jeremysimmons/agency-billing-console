#!/usr/bin/env bash

set -euo pipefail

# ============================================================
# Database backup
#
# Reads ConnectionStrings:Postgres from
#   backend/Aib.Api/appsettings.json
# and writes a pg_dump to backups/yyyy-mm-dd-HHmmss.sql
# ============================================================

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APPSETTINGS="${REPO_ROOT}/backend/Aib.Api/appsettings.json"
BACKUP_DIR="${REPO_ROOT}/backups"

if [[ ! -f "${APPSETTINGS}" ]]; then
  echo "appsettings not found: ${APPSETTINGS}" >&2
  exit 1
fi

if ! command -v pg_dump >/dev/null 2>&1; then
  echo "pg_dump not found. Install PostgreSQL client tools." >&2
  exit 1
fi

CONN="$(python3 -c "
import json, sys
with open(sys.argv[1]) as f:
    print(json.load(f)['ConnectionStrings']['Postgres'])
" "${APPSETTINGS}")"

host="" port="5432" dbname="" user="" password=""
IFS=';' read -ra pairs <<< "${CONN}"
for pair in "${pairs[@]}"; do
  key="${pair%%=*}"
  val="${pair#*=}"
  key="$(printf '%s' "${key}" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
  case "${key}" in
    Host|Server) host="${val}" ;;
    Port) port="${val}" ;;
    Database|"Database Name") dbname="${val}" ;;
    Username|"User ID"|User) user="${val}" ;;
    Password) password="${val}" ;;
  esac
done

if [[ -z "${host}" || -z "${dbname}" || -z "${user}" ]]; then
  echo "Failed to parse ConnectionStrings:Postgres from ${APPSETTINGS}" >&2
  echo "Got: Host=${host:-?} Database=${dbname:-?} Username=${user:-?}" >&2
  exit 1
fi

mkdir -p "${BACKUP_DIR}"
outfile="${BACKUP_DIR}/$(date +%Y-%m-%d-%H%M%S).sql"

echo "Backing up ${dbname}@${host}:${port} → ${outfile}"
export PGPASSWORD="${password}"
pg_dump \
  --host="${host}" \
  --port="${port}" \
  --username="${user}" \
  --dbname="${dbname}" \
  --no-owner \
  --no-acl \
  --file="${outfile}"
unset PGPASSWORD

echo "Done: ${outfile}"
