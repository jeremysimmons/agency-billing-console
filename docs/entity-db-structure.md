# Entity / Database Structure

Current slim schema after migrations through `Script0024_InvoiceLine.sql`. Auth/contractor tables from the original foundation were dropped (`Script0008`).

Source of truth for column shapes: `backend/Aib.Api/Domain/Entities/Organization.cs` + `backend/Aib.Migrations/Migrations/`.

## Overview

```text
agency (1)
  └── client (N)
        ├── project (N)
        └── task (N) ──optional──► project
              └── invoice_label (soft FK by name) ──► invoice

invoice (global, not per-client)
  └── invoice_line (N) ──► client, optional project

clickup_container (flat hierarchy cache)
clickup_sync_run (per sync attempt log)
```

Tasks are **not** hard-linked to invoices. `task.invoice_label` stores the invoice **name** (case-insensitive match). Renaming an invoice does not cascade to tasks.

---

## Entity relationship diagram

```text
┌─────────────┐
│   agency    │
└──────┬──────┘
       │ 1:N
       ▼
┌─────────────┐       ┌──────────────────┐
│   client    │───────│ clickup_container│  (no FK; matched by external ids)
└──────┬──────┘       └──────────────────┘
       │
       ├──────── 1:N ────────► project
       │
       └──────── 1:N ────────► task ◄── project_id (nullable)
                                  │
                                  │ invoice_label ≈ invoice.name
                                  ▼
                             ┌─────────┐
                             │ invoice │
                             └────┬────┘
                                  │ 1:N
                                  ▼
                            invoice_line
```

---

## Tables

### `agency`

Single-tenant org record (app uses `GetDefaultAsync` — first/only agency).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `name` | text | |
| `billing_email` | text | |
| `billing_address` | text | |
| `currency` | text | default `USD` |
| `payment_terms_days` | int | default `30` |
| `active` | bool | default true |
| `last_clickup_sync_at` | timestamptz | set by sync |
| `last_clickup_sync_summary` | text | human summary |
| `ui_preferences` | jsonb | default `{}`; currently `taskGroupClientOrder: Guid[]` |
| `created_at` / `updated_at` | timestamptz | |

**Writable via API:** UI preferences only (`PUT /api/agency/ui-preferences`). Billing fields exist in DB but are not exposed in the slim app.

---

### `client`

Billing client. Usually created by ClickUp sync from a folder or space-level list.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `agency_id` | uuid FK → agency | |
| `name` | text | **User-editable; sync does not overwrite** |
| `code` | text | Parsed from ClickUp title `CODE - Name` |
| `original_name` | text | Full ClickUp folder/list title; sync keeps this updated |
| `clickup_folder_id` | text | Unique when set. XOR with list id |
| `clickup_list_id` | text | Unique when set. Used for folderless / hidden-folder lists |
| `description` | text | |
| `status` | text | `Prospective` \| `Active` \| `Inactive` \| `Archived` |
| `active` | bool | |
| `bill_field_available` | bool | Whether ClickUp Billable dropdown was found |
| `bill_custom_field_id` | text | Resolved field id |
| `bill_yes_option_id` / `bill_no_option_id` | text | Dropdown option ids |
| `bill_field_checked_at` | timestamptz | Last probe time |
| `created_at` / `updated_at` | timestamptz | |

**Indexes:** `ux_client_clickup_folder`, `ux_client_clickup_list` (partial unique on non-null).

**Special client:** `Shared` (seeded). Projects under Shared may be assigned to tasks/lines of any client.

---

### `project`

Slim: id, client, name, timestamps only.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `client_id` | uuid FK → client (cascade delete) | |
| `name` | text | required |
| `created_at` / `updated_at` | timestamptz | |

**Not** created by ClickUp sync. Manual / UI only.

---

### `task` (`WorkTask`)

Sheet-shaped billing prep row + ClickUp mirror columns.

#### Manual / prep columns (owned by app)

| Column | Type | Notes |
|---|---|---|
| `project_id` | uuid? FK → project | |
| `bill` | text | `yes` \| `no` \| null |
| `billable_hours` | numeric(10,2) | |
| `non_billable_hours` | numeric(10,2) | |
| `invoice_label` | text | Matches `invoice.name`; `"none"` = explicitly not invoiced |
| `discount_percent` | numeric(5,2) | 0–100, default 0 |
| `flat_fee` | numeric(12,2) | When set, invoice bills 1 unit at this amount |
| `note` | text | |

#### ClickUp / API columns (sync overwrites)

| Column | Type | Notes |
|---|---|---|
| `clickup_url` | text | Unique when set (primary match key) |
| `clickup_task_id` | text | Unique when set |
| `clickup_parent_id` | text | Parent ClickUp task id (subtasks) |
| `clickup_folder_id` / `_name` | text | |
| `clickup_list_id` / `_name` | text | |
| `title` | text | |
| `description` | text | |
| `clickup_status` | text | |
| `clickup_status_order` | int | |
| `tags` | text | `;`-joined |
| `date_created` / `due_date` / `date_done` / `date_closed` | timestamptz | |
| `order_index` | bigint | |
| `estimated_hours` / `actual_hours` | numeric(10,2) | Tracked time from ClickUp |

#### Identity

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `short_id` | int identity | Human-friendly sequential id |
| `client_id` | uuid FK → client (cascade) | Sync may reassign client |
| `created_at` / `updated_at` | timestamptz | |

**Sync may also update** `bill`, empty hours, and empty `invoice_label` when derived from ClickUp Billable / post-processing. Sync **never** touches `project_id`, `discount_percent`, `flat_fee`, or `note`.

---

### `invoice`

Global named invoices used as `task.invoice_label` values and for billing views.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `name` | text | Unique case-insensitive (`ux_invoice_name_lower`) |
| `status` | text | `preparing` \| `sent` \| `partially-paid` \| `fully-paid` |
| `sort_order` | int | Display / dropdown order |
| `is_default` | bool | At most one true (`ux_invoice_one_default`) |
| `rate` | numeric(12,2)? | Hourly rate; null → app default (`InvoiceOptions.DefaultRate`, usually 70) |
| `include_non_billable_tasks` | text | `none` \| `detail` \| `summary` |
| `created_at` / `updated_at` | timestamptz | |

---

### `invoice_line`

Manual ad-hoc charges on an invoice (not backed by a ClickUp task).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `invoice_id` | uuid FK → invoice (cascade delete) | |
| `client_id` | uuid FK → client | |
| `project_id` | uuid? FK → project (set null on delete) | |
| `title` | text | required |
| `hours` | numeric(12,2) | ≥ 0; forced to 0 when `flat_fee` set |
| `flat_fee` | numeric(12,2)? | ≥ 0 |
| `discount_percent` | numeric(5,2) | 0–100 |
| `sort_order` | int | |
| `created_at` / `updated_at` | timestamptz | |

---

### `clickup_container`

Cached ClickUp hierarchy (workspace / space / folder / list). Upserted by sync; keyed by `external_id`.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | Regenerated each sync upsert batch |
| `container_type` | text | `workspace` \| `space` \| `folder` \| `list` |
| `external_id` | text | Unique |
| `name` | text | |
| `parent_type` / `parent_external_id` | text? | |
| `updated_at` | timestamptz | |

---

### `clickup_sync_run`

One row per sync attempt (SSE progress + persisted log).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `agency_id` | uuid FK → agency | |
| `started_at` / `finished_at` | timestamptz | |
| `status` | text | `running` \| `completed` \| `failed` |
| `summary` | text | |
| `log` | text | Full timestamped log |
| `containers_upserted` / `tasks_created` / `tasks_updated` / `clients_created` / `parents_fetched` | int | Counters |

See [clickup-sync.md](./clickup-sync.md).

---

## Soft relationships & conventions

| Relationship | Mechanism |
|---|---|
| Task → Invoice | `task.invoice_label` ↔ `lower(trim(invoice.name))` |
| Task hierarchy | `task.clickup_parent_id` → `task.clickup_task_id` (no DB FK) |
| Client ↔ ClickUp location | Exactly one of `clickup_folder_id` **or** `clickup_list_id` |
| Shared projects | Client named `Shared`; assignable across clients |

---

## Billing math (UI)

On an invoice detail page (`effectiveRate` = `invoice.rate ?? DefaultRate`):

| Line type | Units | Rate | Subtotal |
|---|---|---|---|
| Billable task with `flat_fee` | 1 | `flat_fee` | `1 × flat_fee × (1 − discount/100)` |
| Billable task hourly | `billable_hours` | invoice effective rate | `hours × rate × (1 − discount/100)` |
| Non-billable (`include=detail`) | `non_billable_hours` | 0 | 0 |
| Non-billable (`include=summary`) | sum of hours | 0 | 0 (one row per client) |
| Manual `invoice_line` with flat fee | 1 | `flat_fee` | same discount formula |
| Manual `invoice_line` hourly | `hours` | invoice rate | same discount formula |

`include_non_billable_tasks = none` hides non-billable tasks from the invoice view.

---

# Business rules: create / update

Rules enforced in application services (`CoreServices`, `ClickUpSyncService`). DB check constraints mirror money/discount bounds.

## Agency

- Update UI preferences only.
- Requires an agency row to exist (seeded).

## Client

### Create
- `name` required (trimmed).
- Defaults: `Status = Active`, `Active = true`, attached to default agency.
- ClickUp location keys optional on manual create.

### Update
- `name` required.
- Sync never overwrites `name`; it may update `original_name` and ClickUp folder/list keys.

### Delete
- Single or delete-all supported.
- Cascades to projects/tasks via FKs.

### Sync-created clients
- Folder title `CODE - Name` → `code` + `name`; full title → `original_name`.
- Location is folder XOR list (never both).
- Bill-field metadata filled during sync probe.

## Project

### Create / update
- `name` required.
- `client_id` must exist.
- Can reassign client on update.

### Assignment to tasks / invoice lines
- Project must belong to the **same client**, **or** to the `Shared` client.
- Otherwise: `"Project must belong to the same client as the task, or Shared."`

## Invoice

### Create
- `name` required; unique case-insensitive.
- Status ∈ `{preparing, sent, partially-paid, fully-paid}` (default `preparing`).
- `rate` ≥ 0 if set.
- `include_non_billable_tasks` ∈ `{none, detail, summary}` (default `none`).
- `sort_order` = max+1.
- **Default flag:**
  - Only `preparing` invoices may be default.
  - Name `"none"` cannot be default.
  - Setting default clears any previous default.

### Update
- Same uniqueness / status / rate / include rules.
- Leaving `preparing` forces `is_default = false`.
- Sort order changed only via reorder endpoint.

### Reorder
- Payload must list **every** invoice id exactly once.

### Effective rate
- API returns `rate` and `effectiveRate` (`rate ?? configured DefaultRate`).

## Invoice line

### Create / update
- Invoice must exist.
- `title` required.
- Client must exist.
- Project (if set) must belong to client or Shared.
- Hours ≥ 0; flat fee ≥ 0 or null; discount 0–100.
- Must have **hours > 0 or a flat fee** (`"Enter hours or a flat fee."`).
- If `flat_fee` set → stored `hours = 0`.
- Sort order = max+1 on create; reorder requires exact permutation.

### Delete
- Line must belong to the given invoice id.

## Task (billing prep)

Tasks are **not** created via the task API in normal flow; they come from ClickUp sync (or CSV import). Updates are field patches.

### `bill`
- Values: `yes` | `no` | empty/null.
- When set to `no` and `invoice_label` empty → set to `"none"`.
- When set to `yes` and `billable_hours` empty and `actual_hours` present → fill billable from actual.
- When set to `no` and `non_billable_hours` empty → fill from `actual_hours` (or `0`).
- On change: push Billable custom field to ClickUp when client has `bill_field_available`.
- On `bill=yes`: ensure configured ClickUp assignee is on the remote task (so assignee-filtered sync keeps it).

### Project
- Same client / Shared rule.
- Assigning a project to a parent with a ClickUp id **propagates** that project to unassigned descendants (recursive via `clickup_parent_id`).
- For billable descendants with empty invoice, also apply the **default** preparing invoice name (if any, not `"none"`).
- Assigning project on a billable task also applies the default invoice label to that task.

### Invoice label
- Free text matching invoice names; empty clears.
- `"none"` means explicitly not invoiced (counts as invoiced for “uninvoiced” filters).

### Hours
- Cannot be negative; rounded to 2 decimals.
- Updating **billable** hours may create a ClickUp time entry for the positive delta vs tracked time (configured assignee). Reducing below tracked time does not delete ClickUp time; returns a warning.

### Flat fee / discount
- Flat fee ≥ 0 or null; discount 0–100; both rounded to 2 decimals.
- Flat fee present ⇒ invoice treats task as 1 unit at that amount (hours ignored for billing).

### Needs attention
A task needs attention unless it is **complete** (`clickup_status = cancelled` **and** `bill = no`), and any of:
- `bill` empty, or
- `bill = yes`, no `flat_fee`, and no positive billable/non-billable hours, or
- `invoice_label` empty.

### List / filter behavior
- Filtered task lists pull in ClickUp **ancestor** tasks so children are not orphaned in the UI.
- Results ordered DFS: children immediately after parents (`order_index`, then title).

## CSV import

Separate path (`POST /api/clickup/import-csv`) that upserts tasks/clients from spreadsheet columns. Prefer ClickUp sync for ongoing use.

---

## What sync owns vs what the app owns

| Field group | Owner | Sync behavior |
|---|---|---|
| ClickUp ids, title, status, dates, tags, estimates, actual hours | ClickUp | Always overwrite |
| `bill` | Shared | Overwrite only when Billable custom field present on remote task |
| Empty hours / empty invoice for `bill=no` | Derived | Filled on sync + on local bill change |
| `project_id`, `discount_percent`, `flat_fee`, `note` | App | Never touched by sync |
| Client `name` | App | Never overwritten by sync |
| Client `original_name`, location keys, bill-field probe | Sync | Updated |

---

## Related docs

- [ClickUp sync process](./clickup-sync.md)
- [ClickUp data hierarchy (UI vs API naming)](./clickup-data-hierarchy.md)
