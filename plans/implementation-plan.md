# ClickUp task prep for invoicing

## Goal

Single-operator app to **sync ClickUp tasks**, **keep hierarchy names current**, **assign tasks to your projects**, and **surface incomplete billing fields** so you can finish data before invoicing elsewhere.

**Out of scope:** authentication, invoices, payments, billing periods, expenses, PDF/email, Quartz scheduler, agency portal, time-entry ledger, rollups, finalize/invoiced workflow, mapping-review UI, external staging tables.

---

## Stack

* Backend: ASP.NET Core, PostgreSQL (Npgsql + Dapper), dbup-postgresql migrations
* Frontend: Vue 3 + Vite + Pinia Colada + PrimeVue
* ClickUp: live API sync via button only (no scheduler)
* Local: postgres via brew; no Docker required

---

## Domain

| Entity | Role |
|--------|------|
| `clickup_container` | Lookup: space / folder / list. Upserted on sync. |
| `client` | Linked via `clickup_folder_id` when possible. |
| `project` | You define under a client (not a ClickUp list). |
| `task` | Sheet-shaped: manual billing cols + ClickUp API cols. |

### Manual columns (never overwritten by sync)

`bill`, `billable_hours`, `non_billable_hours`, `invoice_label`, `note`, internal `project_id`

### API columns (overwrite on sync)

`clickup_url`, ids/names for folder/list, title, description, status, tags, dates, estimate/actual hours

### Missing-data filter

Needs attention when any of: no internal `project_id`, `bill` null, `bill=yes` with empty billable hours, empty `invoice_label`.

---

## Sync

`POST /api/clickup/sync`:

1. Rebuild hierarchy → upsert `clickup_container`
2. Page ClickUp tasks (same query as `clickup-google-sheets.js`)
3. Ensure clients from folder id/name
4. Upsert tasks by URL; preserve manual columns

Also: `GET /api/clickup/hierarchy`, `POST /api/clickup/import-csv` (bootstrap sheet export).

---

## UI

Nav: **Tasks | Clients | Hierarchy | Sync** (no login)

* Tasks — client filter, missing-only, edit prep fields, assign project
* Clients — CRUD + user-defined projects
* Hierarchy — nested space → folder → list
* Sync — API sync button + CSV bootstrap

---

## Progress

- [x] Strip auth (open API + SPA)
- [x] Slim schema (`Script0006_SlimClickUp.sql`)
- [x] Sync button + hierarchy refresh
- [x] CSV bootstrap
- [x] Tasks / Clients / Hierarchy / Sync UI
- [x] Cleanup dead review/auth surfaces; this plan replaces the old accounting tracker

## Later (optional)

* Re-add auth for remote deploy
* Invoice export / PDF
* Sheet `backfillClickUpTimeData` per-task estimate/spent
