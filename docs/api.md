# Backend API

HTTP API exposed by `Aib.Api` (ASP.NET Core controllers). Controllers: `OrgControllers.cs`, `ClickUpController.cs`. Contracts: `Application/Contracts/Contracts.cs`.

Default local base URL: `http://localhost:5146` (see `launchSettings.json`).

No auth. JSON request/response bodies use camelCase. Enums serialize as strings (`JsonStringEnumConverter`). Invoice status and include-non-billable mode are kebab-case strings (custom converters).

Related:

- Schema & business rules → [entity-db-structure.md](./entity-db-structure.md)
- ClickUp sync pipeline / SSE phases → [clickup-sync.md](./clickup-sync.md)
- ClickUp UI vs API naming → [clickup-data-hierarchy.md](./clickup-data-hierarchy.md)

---

## Conventions

| Topic | Behavior |
|---|---|
| Success | `200 OK` with body, `201 Created` (client create), or `204 No Content` (deletes) |
| Errors | `{ "error": "<message>" }` — `400` domain, `404` not found, `403` forbidden, `500` unexpected |
| IDs | UUID path/query params unless noted |
| Money / hours | decimals; hours rounded to 2 places server-side |

---

## Health

| Method | Path | Response |
|---|---|---|
| `GET` | `/health` | `{ "status": "ok" }` |

---

## Agency

| Method | Path | Body | Response |
|---|---|---|---|
| `GET` | `/api/agency` | — | `AgencyDto` |
| `PUT` | `/api/agency/ui-preferences` | `UpdateAgencyUiPreferencesRequest` | `AgencyDto` |

```text
AgencyDto
  id, name, lastClickUpSyncAt?, lastClickUpSyncSummary?,
  uiPreferences: { taskGroupClientOrder: guid[] }

UpdateAgencyUiPreferencesRequest
  taskGroupClientOrder: guid[]
```

Billing fields exist on the `agency` row but are not exposed. See [entity-db-structure.md](./entity-db-structure.md#agency).

---

## Clients

| Method | Path | Body | Response |
|---|---|---|---|
| `GET` | `/api/clients` | — | `ClientDto[]` |
| `GET` | `/api/clients/{id}` | — | `ClientDto` |
| `POST` | `/api/clients` | `CreateClientRequest` | `201` + `ClientDto` |
| `PUT` | `/api/clients/{id}` | `UpdateClientRequest` | `ClientDto` |
| `DELETE` | `/api/clients/{id}` | — | `204` |
| `DELETE` | `/api/clients` | — | `{ deleted: number }` |

```text
CreateClientRequest
  name, code?, originalName?, description?, status?   // status default Active

UpdateClientRequest
  name, code?, originalName?, description?, status, active

ClientDto
  id, name, code?, originalName?, clickUpFolderId?, clickUpListId?,
  description?, status, active, billFieldAvailable

ClientStatus (string enum): Prospective | Active | Inactive | Archived
```

Most clients are created by ClickUp sync from folders/lists — see [clickup-sync.md](./clickup-sync.md#client-resolution). Sync does not overwrite user-edited `name`.

---

## Projects

| Method | Path | Query | Body | Response |
|---|---|---|---|---|
| `GET` | `/api/projects` | — | — | all `ProjectDto[]` |
| `GET` | `/api/projects` | `clientId`, `includeShared?=false` | — | projects for client (+ Shared if flag) |
| `POST` | `/api/projects` | — | `CreateProjectRequest` | `ProjectDto` |
| `PUT` | `/api/projects/{id}` | — | `UpdateProjectRequest` | `ProjectDto` |

```text
CreateProjectRequest  { clientId, name }
UpdateProjectRequest  { name, clientId }
ProjectDto            { id, clientId, clientName, name }
```

Not created by ClickUp sync. Assignable to tasks/lines only if same client or the seeded `Shared` client — see [entity-db-structure.md](./entity-db-structure.md#project).

---

## Invoices

| Method | Path | Body | Response |
|---|---|---|---|
| `GET` | `/api/invoices` | — | `InvoiceDto[]` |
| `POST` | `/api/invoices` | `CreateInvoiceRequest` | `InvoiceDto` |
| `PUT` | `/api/invoices/{id}` | `UpdateInvoiceRequest` | `InvoiceDto` |
| `PUT` | `/api/invoices/reorder` | `ReorderInvoicesRequest` | `InvoiceDto[]` |

```text
CreateInvoiceRequest
  name, status?=preparing, isDefault?=false, rate?, includeNonBillableTasks?=none

UpdateInvoiceRequest
  name, status, isDefault?=false, rate?, includeNonBillableTasks?

ReorderInvoicesRequest
  orderedIds: guid[]   // must be a permutation of all invoice ids

InvoiceDto
  id, name, status, sortOrder, isDefault, rate?, effectiveRate, includeNonBillableTasks

status: preparing | sent | partially-paid | fully-paid
includeNonBillableTasks: none | detail | summary
```

`effectiveRate` = `rate ??` configured default (usually `70`). Default flag rules and uniqueness: [entity-db-structure.md](./entity-db-structure.md#invoice). Tasks link by soft name match (`task.invoice_label`), not FK.

### Invoice lines

Manual ad-hoc charges (not ClickUp tasks).

| Method | Path | Body | Response |
|---|---|---|---|
| `GET` | `/api/invoices/{invoiceId}/lines` | — | `InvoiceLineDto[]` |
| `POST` | `/api/invoices/{invoiceId}/lines` | `CreateInvoiceLineRequest` | `InvoiceLineDto` |
| `PUT` | `/api/invoices/{invoiceId}/lines/{id}` | `UpdateInvoiceLineRequest` | `InvoiceLineDto` |
| `PUT` | `/api/invoices/{invoiceId}/lines/reorder` | `ReorderInvoiceLinesRequest` | `InvoiceLineDto[]` |
| `DELETE` | `/api/invoices/{invoiceId}/lines/{id}` | — | `204` |

```text
CreateInvoiceLineRequest / UpdateInvoiceLineRequest
  clientId, projectId?, title, hours?=0, flatFee?, discountPercent?=0

ReorderInvoiceLinesRequest
  orderedIds: guid[]

InvoiceLineDto
  id, invoiceId, clientId, clientName, projectId?, projectName?,
  title, hours, flatFee?, discountPercent, sortOrder
```

Must supply hours > 0 or a flat fee. Flat fee forces stored `hours = 0`. Billing math: [entity-db-structure.md](./entity-db-structure.md#billing-math-ui).

---

## Tasks

Tasks are not created via this API in normal flow; they come from ClickUp sync or CSV import. Updates are field patches.

### List / filter / summary

| Method | Path | Response |
|---|---|---|
| `GET` | `/api/tasks` | `TaskDto[]` |
| `GET` | `/api/tasks/summary` | `TaskSummaryDto` |
| `GET` | `/api/tasks/filter-options` | `TaskFilterOptionsDto` |

Shared query params for list + summary:

| Param | Type | Notes |
|---|---|---|
| `clientId` | guid? | |
| `missingOnly` | bool? | needs-attention filter |
| `invoiced` | string[]? | invoice-label filter values |
| `projectId` | guid? | |
| `unassignedOnly` | bool? | no project |
| `createdMonth` | string? | e.g. `2025-01` |
| `doneMonth` | string? | |
| `statuses` | string[]? | ClickUp statuses |
| `listId` / `folderId` / `spaceId` | string? | ClickUp location ids |
| `invoiceLabel` | string? | |

`filter-options` accepts optional `clientId` and returns available `createdMonths`, `doneMonths`, `statuses`.

Filtered lists pull in ClickUp ancestor tasks and order DFS (children after parents). Needs-attention rules: [entity-db-structure.md](./entity-db-structure.md#needs-attention).

```text
TaskDto
  id, shortId, clientId, clientName, projectId?, projectName?,
  bill?, billableHours?, nonBillableHours?, invoiceLabel?, discountPercent,
  flatFee?, note?,
  clickUpUrl?, clickUpTaskId?, clickUpParentId?,
  clickUpFolderId?, clickUpFolderName?, clickUpListId?, clickUpListName?,
  title, description?, clickUpStatus?, tags?,
  dateCreated?, dueDate?, dateDone?, dateClosed?,
  orderIndex?, estimatedHours?, actualHours?,
  needsAttention

TaskSummaryDto
  byClient:  { clientId, clientName, taskCount, missingCount, uninvoicedCount }[]
  byDoneMonth: { month, taskCount, missingCount, uninvoicedCount }[]

TaskFilterOptionsDto
  createdMonths, doneMonths, statuses
```

### Sync one task

| Method | Path | Response |
|---|---|---|
| `POST` | `/api/tasks/{id}/sync` | `TaskDto` |

Fetches that ClickUp task + descendants. Does not create a sync-run row. Details: [clickup-sync.md](./clickup-sync.md#single-task-sync).

### Prep patches

| Method | Path | Body | Response |
|---|---|---|---|
| `PATCH` | `/api/tasks/{id}/bill` | `{ bill? }` | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/project` | `{ projectId? }` | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/invoice` | `{ invoiceLabel? }` | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/discount` | `{ discountPercent }` | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/flat-fee` | `{ flatFee? }` | `TaskDto` |
| `PATCH` | `/api/tasks/{id}/billable-hours` | `{ hours? }` | `TaskHoursUpdateDto` |
| `PATCH` | `/api/tasks/{id}/non-billable-hours` | `{ hours? }` | `TaskHoursUpdateDto` |
| `PATCH` | `/api/tasks/{id}/prep` | `UpdateTaskPrepRequest` | `TaskDto` |

```text
UpdateTaskPrepRequest
  projectId?, bill?, billableHours?, nonBillableHours?,
  invoiceLabel?, flatFee?, note?

TaskHoursUpdateDto
  task: TaskDto
  clickUpTrackedHours?
  warning?    // e.g. reducing below tracked time (no ClickUp delete)
```

Side effects (bill → ClickUp custom field; hours → time entry; project cascade): [clickup-sync.md](./clickup-sync.md#bidirectional-bill--hours-outside-full-sync) and [entity-db-structure.md](./entity-db-structure.md#task-billing-prep).

---

## ClickUp

| Method | Path | Notes | Response |
|---|---|---|---|
| `POST` | `/api/clickup/sync` | SSE `text/event-stream` | `event: sync` + JSON `ClickUpSyncProgressEvent` |
| `GET` | `/api/clickup/hierarchy` | Cached container tree | `ClickUpHierarchyNodeDto[]` (roots) |
| `GET` | `/api/clickup/sync-runs` | `?limit=20` | `ClickUpSyncRunSummaryDto[]` |
| `GET` | `/api/clickup/sync-runs/{id}` | Full log | `ClickUpSyncRunDto` |
| `POST` | `/api/clickup/import-csv` | `multipart/form-data` field `file` | `CsvImportResultDto` |

Full sync pipeline, SSE phases, and field ownership: [clickup-sync.md](./clickup-sync.md). Hierarchy naming: [clickup-data-hierarchy.md](./clickup-data-hierarchy.md).

```text
ClickUpSyncProgressEvent
  phase, message?, containersUpserted?, page?,
  tasksCreated?, tasksUpdated?, clientsCreated?,
  clientsProcessed?, clientsTotal?, parentsFetched?,
  syncedAt?, summary?, error?, syncRunId?

  phases: started | hierarchy | page | descendants | parents
          | bill_fields | hours | invoices | log | completed | error

ClickUpHierarchyNodeDto
  type, id, name, parentType?, parentId?, updatedAt, taskCount, children[]
  type: workspace | space | folder | list

ClickUpSyncRunSummaryDto / ClickUpSyncRunDto
  id, startedAt, finishedAt?, status, summary?,
  containersUpserted, tasksCreated, tasksUpdated, clientsCreated, parentsFetched
  (+ log on full DTO)
  status: running | completed | failed

CsvImportResultDto
  imported, updated, skipped, summary
```

CSV import upserts tasks/clients from spreadsheet columns; prefer sync for ongoing use.

---

## Endpoint index

| Method | Path |
|---|---|
| `GET` | `/health` |
| `GET` | `/api/agency` |
| `PUT` | `/api/agency/ui-preferences` |
| `GET` | `/api/clients` |
| `GET` | `/api/clients/{id}` |
| `POST` | `/api/clients` |
| `PUT` | `/api/clients/{id}` |
| `DELETE` | `/api/clients/{id}` |
| `DELETE` | `/api/clients` |
| `GET` | `/api/projects` |
| `POST` | `/api/projects` |
| `PUT` | `/api/projects/{id}` |
| `GET` | `/api/invoices` |
| `POST` | `/api/invoices` |
| `PUT` | `/api/invoices/{id}` |
| `PUT` | `/api/invoices/reorder` |
| `GET` | `/api/invoices/{invoiceId}/lines` |
| `POST` | `/api/invoices/{invoiceId}/lines` |
| `PUT` | `/api/invoices/{invoiceId}/lines/{id}` |
| `PUT` | `/api/invoices/{invoiceId}/lines/reorder` |
| `DELETE` | `/api/invoices/{invoiceId}/lines/{id}` |
| `GET` | `/api/tasks` |
| `GET` | `/api/tasks/summary` |
| `GET` | `/api/tasks/filter-options` |
| `POST` | `/api/tasks/{id}/sync` |
| `PATCH` | `/api/tasks/{id}/bill` |
| `PATCH` | `/api/tasks/{id}/project` |
| `PATCH` | `/api/tasks/{id}/invoice` |
| `PATCH` | `/api/tasks/{id}/discount` |
| `PATCH` | `/api/tasks/{id}/flat-fee` |
| `PATCH` | `/api/tasks/{id}/billable-hours` |
| `PATCH` | `/api/tasks/{id}/non-billable-hours` |
| `PATCH` | `/api/tasks/{id}/prep` |
| `POST` | `/api/clickup/sync` |
| `GET` | `/api/clickup/hierarchy` |
| `GET` | `/api/clickup/sync-runs` |
| `GET` | `/api/clickup/sync-runs/{id}` |
| `POST` | `/api/clickup/import-csv` |
