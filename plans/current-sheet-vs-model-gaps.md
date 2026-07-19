# Current Sheet vs Data Model — Gaps

Source: `import/12Legs Clickup Tasks and Billing - tasks.csv`  
Compared against: Phase 1–2 domain (internal work + ClickUp external staging) and planned billing/mapping (M4–M6).

Sheet shape matches `clickup-google-sheets.js`: columns A–F are manual; G–T are ClickUp API. ~364 task rows.

---

## Column map

| CSV column | Model home today | Fit |
|---|---|---|
| `url` | `ExternalWorkItem.Url` (task id extractable from `/t/{id}`) | OK |
| `name` | `ExternalWorkItem.Name` / `Task.Title` | OK |
| `description` | `ExternalWorkItem.Description` / `Task.Description` | OK |
| `parent` | `ExternalWorkItem.ExternalParentWorkItemId` / `Task.ParentTaskId` | OK (~146/364 filled) |
| `status` | `ExternalWorkItem.StatusName` → `ExternalStatusMapping` (M4) | Partial — free strings (`complete`, `client review`, `to do`, …) not mapped yet |
| `date_created` | `ExternalWorkItem.SourceCreatedAt` | OK |
| `due_date` | `ExternalWorkItem.DueDate` / `Task.DueDate` | OK |
| `date_done` | `ExternalWorkItem.CompletedAt` / `Task.CompletedAt` | OK |
| `date_closed` | nowhere first-class (only `IsClosed` bool) | Gap |
| `order_index` | `Task.SortOrder` only (not on external staging) | Gap on external |
| `estimated_hours` | `ExternalWorkItem.TimeEstimateMinutes` (×60) | OK |
| `actual_hours` | `TimeSpentMinutes` on work item | Partial — sheet aggregates; model prefers discrete `ExternalTimeEntry` |
| `project_name` | ClickUp folder (script maps `project=hidden` → list name) | Ambiguous — name only, no external id |
| `list_name` | ClickUp list → usually internal `Project` / client context | Ambiguous — name only |
| `tags` | nowhere | Gap |
| **`Bill`** (`yes` / `no`) | closest: `Task.Billable` / `BillingStatus` | Gap — contractor worksheet decision, not ClickUp |
| **`Billable Hours`** | `TimeEntry` / invoice line quantity | Gap — manual override hours |
| **`NonBillable Hours`** | no first-class field | Gap |
| **`invoice`** (e.g. `Aug 2025`, `TL20251214A-rev2`, `none`) | `Invoice.InvoiceNumber` / `BillingPeriod` | Gap — free-text label, not FK |
| **`Note`** | nowhere on task/time | Gap |

Manual fill rates (approx.): `Bill` 108, `Billable Hours` 108, `NonBillable Hours` 104, `invoice` 105, `Note` 105 of 364.

---

## Sheet has; model lacks (seed blockers)

1. **Manual billing decisions** (`Bill`, billable/non-billable hours, `Note`) — contractor review output. Model expects finalize workflow (`BillingStatus`, `TimeEntry`, `BillingPeriodItem`), not columns on the task.
2. **Invoice label** — free text period/number. Model has `Invoice` + `BillingPeriod`, but no seed path for display names like `Aug 2025` vs real numbers like `TL20251214A-rev2`.
3. **Tags** — useful client/project hints (e.g. `backend;406 golf carts`); no tags on external or internal task.
4. **`date_closed` vs `date_done`** — both present; model only keeps completed.
5. **Name-only hierarchy** — `project_name` / `list_name` without ClickUp ids. Live M3 import has ids; CSV seed must resolve by name (fragile) or join via `url` → `external_work_item.external_id`.

---

## Model has; sheet lacks

- Client / Project / Agency as first-class entities (sheet implies client via project prefix: `EER - …`, `TER - …`)
- Discrete time entries (live import has many; sheet only task-level `actual_hours`)
- Billing status machine (`not_ready` → … → `invoiced`) vs binary `Bill`
- Rate hierarchy, expenses, payments, rollup modes
- Mapping tables (M4) — sheet is already a post-mapping / billing-worksheet artifact

---

## Semantic conflict (important for seed)

Sheet stores **billable hours as an override**, not as ClickUp time.

Live import `time_spent` / `ExternalTimeEntry` will disagree with `Billable Hours` for already-reviewed rows. Seed must prefer sheet hours for invoiced/finalized rows, or the importer will fight historical billing later.

---

## Practical seed approach (from `assets/future.md`)

Treat the CSV as **two layers**:

1. **ClickUp snapshot** (`url` + G–T) → seed/refresh `external_*`. Prefer match on task id from `url`; do not trust folder/list names alone.
2. **Billing worksheet** (B–F) → seed **internal** state after mapping:
   - `Bill=yes` + hours → finalized / invoiced task + synthetic `TimeEntry`(ies)
   - `Bill=no` → `BillingStatus=Excluded` (or non-billable)
   - `invoice` → attach to seeded `BillingPeriod` / `Invoice` (normalize period labels vs real invoice numbers)
   - `Note` → append to description, mapping notes, or add a first-class note column if needed

**Dependency:** M4 mappings (folder/list → client/project, status mappings) should exist before a high-fidelity internal seed; external-only seed can run earlier using `url` ids.

---

## Observed sheet status vocabulary (for M4 status mapping)

| Sheet `status` | Count (approx.) |
|---|---|
| complete | 299 |
| to do | 26 |
| client review | 12 |
| cancelled | 8 |
| in progress | 7 |
| internal review | 7 |
| update required | 2 |
| planning | 2 |
| on hold | 1 |

---

## Observed invoice labels (sample)

| `invoice` | Count (approx.) |
|---|---|
| Aug 2025 | 103 |
| TL20251214A-rev2 | 1 |
| none | 1 |
