# Developing locally

Agency Billing Console: ASP.NET Core 10 API + Vue 3 (Vite) SPA + PostgreSQL.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (see `global.json`)
- Node.js 20+ and npm
- PostgreSQL 16 (`brew install postgresql@16` on macOS)
- Docker (for Mailpit — local SMTP catcher)
- Optional: ClickUp API token in `.env` (repo root) for imports
- [mkcert](https://github.com/FiloSottile/mkcert) for trusted `https://localhost:3000` (`./scripts/dev-bootstrap.sh`; also auto-run by `run-ui`)

## One-time setup

### 1. Database

```bash
brew services start postgresql@16

# Create DB (adjust role to match your local Postgres user)
createdb aib
```

Edit `backend/Aib.Api/appsettings.Development.json` → `ConnectionStrings:Postgres` so host/user/database match your machine. Current default:

```text
Host=localhost;Port=5432;Database=aib;Username=<you>;Include Error Detail=true
```

Migrations (DbUp) run via the migrations console:

```bash
./scripts/run-migrations
```

Seed data runs automatically on API startup.

### 2. Frontend deps

```bash
cd frontend
npm install
```

### 3. ClickUp token (optional)

Create `/Users/.../aib/.env` (gitignored):

```bash
CLICKUP_API_TOKEN=pk_...
```

Team/assignee IDs live in `appsettings.Development.json` under `ClickUp`. Scheduled imports are **off** in Development (`ScheduleEnabled: false`); trigger imports manually from the API or UI once wired.

### 4. Trusted TLS for `https://localhost:3000`

Vite serves **mkcert** certs from `certs/` (not the untrusted `@vitejs/plugin-basic-ssl` defaults).

```bash
./scripts/dev-bootstrap.sh
```

This installs the local CA into the system trust store and writes:

- `certs/localhost.pem`
- `certs/localhost-key.pem`

`./scripts/run-ui` (and thus `./scripts/run`) runs bootstrap automatically if those files are missing. Restart the UI after bootstrap so Vite picks up the new certs.

Firefox: ensure `nss` was installed with mkcert (`brew install nss`) and re-run `mkcert -install` if needed.

## Run (two terminals)

Vite proxies `/api` → `http://127.0.0.1:5080`. The API **must** listen on **5080**.

```bash
# Both (tmux: top api / bottom ui)
./scripts/run

# Or separately
./scripts/run-api   # Terminal A
./scripts/run-ui    # Terminal B
```

Open **https://localhost:3000** (accept the self-signed cert if prompted).

Health check: `http://localhost:5080/health`

`run-api` loads repo-root `.env` when present (`CLICKUP_API_TOKEN`, `MAIL_*`, etc.), starts Mailpit via `docker compose` if Docker is available, and defaults to `ASPNETCORE_URLS=http://localhost:5080`. Override with env vars if needed.

`./scripts/run` creates (or attaches to) a tmux session named `aib` with **two rows** (top: API, bottom: UI). Override session name with `AIB_TMUX_SESSION`. Detach with `Ctrl-b d`.

### Mailpit (local email)

Transactional mail (magic links, etc.) goes to [Mailpit](https://github.com/axllent/mailpit) in Development — not a real SMTP provider.

```bash
docker compose up -d mailpit   # or just ./scripts/run-api
```

| What | Value |
|------|-------|
| Web UI | http://localhost:8027 |
| SMTP | `localhost:1025` (no auth / no TLS) |

Config lives in `appsettings.Development.json` → `Mail`, overridable via `.env`:

```bash
MAIL_HOST=localhost
MAIL_PORT=1025
MAIL_ENCRYPTION=null
MAIL_USERNAME=null
MAIL_PASSWORD=null
```

Use `localhost` (not `mailpit`) — the API runs on the host; only the Compose service is named `mailpit`.

Host UI port is **8027** (not 8025) because DDEV’s router usually binds 8025–8026.

## Seed login

From `appsettings.Development.json` → `Seed:Owner`:

| Field    | Value           |
|----------|-----------------|
| Username | `owner`         |
| Email    | `owner@localhost` |
| Password | `ChangeMe!123`  |

## Useful URLs

| What | URL |
|------|-----|
| SPA | https://localhost:3000 |
| API (direct) | http://localhost:5080 |
| Health | http://localhost:5080/health |
| Mailpit | http://localhost:8027 |
| Clients | https://localhost:3000/clients |
| Mappings | https://localhost:3000/mappings |
| Work review | https://localhost:3000/work |

## Manual ClickUp import (API)

With a session cookie + CSRF (easiest: use the SPA after login), or via curl:

```bash
# 1) CSRF cookie
curl -c /tmp/aib.jar -b /tmp/aib.jar http://localhost:5080/api/auth/csrf

# 2) Login (read aib_csrf from jar into X-CSRF-Token)
CSRF=$(awk '/aib_csrf/{print $7}' /tmp/aib.jar)
curl -c /tmp/aib.jar -b /tmp/aib.jar -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" -H "X-CSRF-Token: $CSRF" \
  -d '{"usernameOrEmail":"owner","password":"ChangeMe!123"}'

# 3) Import
curl -b /tmp/aib.jar -X POST http://localhost:5080/api/integrations/clickup/import \
  -H "Content-Type: application/json" -H "X-CSRF-Token: $CSRF" \
  -d '{"fullResync":true}'
```

## Build checks

```bash
# Backend
cd backend && dotnet build Aib.slnx

# Frontend typecheck + production build
cd frontend && npx vue-tsc --noEmit && npm run build
```

## Layout

```text
backend/          ASP.NET solution (Api, Application, Domain, Infrastructure)
frontend/         Vue 3 + Vite + Pinia Colada + PrimeVue
plans/            Architecture + milestone tracker
import/           One-time CSV seed source (not auto-loaded)
scripts/          Local TLS bootstrap
```

## Notes

- Cookie auth + double-submit CSRF (`aib_session`, `aib_csrf`). Always call the SPA origin so cookies + proxy stay aligned.
- Do not commit `.env`, `google-auth-client-secret.json`, or `certs/`.
- Deploy target is Lightsail (nginx + Postgres + systemd), not Docker — see `plans/implementation-plan.md`.
