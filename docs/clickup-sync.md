# ClickUp Sync Process

How AIB pulls ClickUp hierarchy + tasks into local `clickup_container`, `client`, and `task` rows.

Implementation: `ClickUpSyncService`, `ClickUpClient`, `ClickUpHierarchyBuilder`.  
Config: `ClickUp` section (`ClickUpOptions`).  
UI: Sync page streams progress over SSE.

---

## Prerequisites

| Setting | Purpose |
|---|---|
| `ApiToken` | Personal/API token |
| `TeamId` | Workspace id (ClickUp API v2 still calls this `team`) |
| `AssigneeId` | Optional; filters team task list to that user |
| `InitialCreatedAfterMs` | `date_created_gt` cutoff (default ~2025-01-01) |
| `PageLimit` | Page size (default 100) |
| `BillCustomFieldId` / `BillYesOptionId` / `BillNoOptionId` / `BillFieldName` | Map ClickUp Billable dropdown ↔ local `task.bill` |

Sync throws if token or team id missing, or if no agency row exists.

---

## Entry points

| Endpoint | Behavior |
|---|---|
| `POST /api/clickup/sync` | Full sync; `text/event-stream` progress events |
| `GET /api/clickup/sync-runs` | Recent run summaries |
| `GET /api/clickup/sync-runs/{id}` | Full log for one run |
| `GET /api/clickup/hierarchy` | Cached container tree (+ task counts) |
| Task-level sync | `ClickUpSyncService.SyncTaskAsync` — single task + its descendants |

Each full sync inserts a `clickup_sync_run` (`status=running`), then completes or fails with log + counters.

---

## High-level pipeline

```text
1. Insert sync run (running)
2. Build + upsert ClickUp hierarchy → clickup_container
3. Page through assignee-filtered team tasks → create/update clients + tasks
4. Resolve missing descendants (subtasks under assigned tasks)
5. Resolve missing parents (ancestors not in assignee filter)
6. Probe Billable custom fields per client location
7. Fill empty hours from actual_hours
8. Set invoice_label=none for bill=no with empty invoice
9. Warn if any parent ids still missing
10. Update agency last sync summary; mark run completed
```

On exception: run `status=failed`, summary = error message, log persisted, exception rethrown (SSE reports `error` event).

---

## Step details

### 1. Hierarchy upsert

`ClickUpHierarchyBuilder.BuildAsync(teamId)` walks API v2:

```text
GET /team                         → workspace name
GET /team/{id}/space              → spaces
GET /space/{id}/folder            → folders (+ nested lists)
GET /space/{id}/list              → folderless lists
```

Emits flat rows with v3-style types: `workspace` → `space` → `folder` → `list`.

All rows upserted into `clickup_container` (unique on `external_id`).

Subfolders are not modeled separately; nested folders would appear as folders with folder parents if the API returned them (current builder only walks space→folder→list and space→list).

### 2. Assignee task pages

```text
GET /team/{teamId}/task
  ?reverse=true
  &include_closed=true
  &subtasks=true
  &page=N
  &limit=PageLimit
  &date_created_gt=InitialCreatedAfterMs
  &assignees[]=AssigneeId   (if configured)
```

For each remote task:

1. **Ensure client** (see Client resolution).
2. Match local task by `clickup_url` (preferred) else `clickup_task_id`.
3. **Create** → `MapNewTask` + insert (identity `short_id` assigned by DB).
4. **Update** → `ApplyApiFields` + `UpdateApiFieldsAsync`.

### 3. Missing descendants

Assignee filter often returns parents without every nested subtask (or stubs without list/folder).

BFS over assigned task ids:

1. `GET /task/{id}?include_subtasks=true`
2. For each subtask stub: if list/folder missing, fetch full task.
3. Upsert; enqueue children for further expansion.

Counts newly **created** tasks toward `tasks_created` / summary “missing descendants”.

### 4. Missing parents

Local tasks may reference `clickup_parent_id` values not present locally (parent not assigned to filter user).

Queue = `ListMissingParentClickUpIdsAsync`:

1. Fetch each parent by id.
2. Ensure client + upsert task.
3. If that parent has its own missing parent, enqueue ancestor.

`parents_fetched` = count of newly created parent tasks.

### 5. Billable field probe

For every client touched in this run (parallelism 8):

1. Resolve location hint (list / folder / hidden folder).
2. Merge custom fields from list, folder (if not hidden), and parent space.
3. Find Billable field by configured id, else drop_down named `BillFieldName` (default `"Billable"`).
4. Resolve yes/no option ids by configured id or names (`yes`/`y`/`true`, `no`/`n`/`false`).
5. Persist on client: `bill_field_available`, field/option ids, `bill_field_checked_at`.

Used later when the app writes bill changes back to ClickUp.

### 6. Post-process hours + invoices

SQL batch updates (all tasks, not only this page):

**Fill empty hours**

| Condition | Action |
|---|---|
| `bill=yes`, `billable_hours` null, `actual_hours` set | `billable_hours = actual_hours` |
| `bill=no`, `non_billable_hours` null | `non_billable_hours = coalesce(actual_hours, 0)` |

**None invoice**

| Condition | Action |
|---|---|
| `bill=no`, invoice empty | `invoice_label = 'none'` |

Same rules run inline during `ApplyApiFields` for the tasks just synced.

### 7. Finish

- Agency `last_clickup_sync_at` + summary string.
- Sync run: counters, full log, `completed`.

---

## Client resolution

For each remote task:

```text
if folderId present AND folder not hidden:
    client key = folder   (ClickUpFolderId)
    display name = folder name
else if listId present:
    client key = list     (ClickUpListId)
    display name = list name (or folder name fallback)
else:
    error — cannot resolve client
```

Name parsing (`ClickUpFolderNaming`):

```text
"ACME - Acme Corp"  →  code=ACME, name=Acme Corp, original=full title
"Acme Corp"         →  code=null, name=Acme Corp
```

Lookup order (folder clients): by folder id, then legacy list-id column with same id.  
List clients: by list id, then legacy folder-id column.

**On match:** update `original_name` + location keys (folder XOR list). **Do not** overwrite user-edited `name`.

**On miss:** insert Active client with parsed name/code.

---

## Task field mapping

### Always overwritten from ClickUp

`client_id`, `clickup_*`, `title`, `description`, `clickup_status` (+ order), `tags`, dates, `order_index`, `estimated_hours`, `actual_hours`.

### Conditionally from Billable custom field

If the remote task includes the Billable field:

| Remote option | Local `bill` |
|---|---|
| yes / configured yes option | `yes` |
| no / configured no option | `no` |
| empty value | `null` |
| field absent | **leave local bill unchanged** |

Then apply hours/invoice fill rules (empty slots only).

### Never touched by sync

`project_id`, `discount_percent`, `flat_fee`, `note`, `short_id`.

`UpdateApiFieldsAsync` writes bill/hours/invoice_label as currently held on the entity (so unchanged bill stays unchanged when the field is absent).

---

## Progress events (SSE)

`POST /api/clickup/sync` emits `event: sync` with JSON payloads (`ClickUpSyncProgressEvent`):

| `phase` | Meaning |
|---|---|
| `started` | Run begun |
| `hierarchy` | Containers upserted |
| `page` | Task page processed (counts) |
| `descendants` | Descendant resolve |
| `parents` | Parent resolve |
| `bill_fields` | Custom-field probe progress |
| `hours` / `invoices` | Post-process steps |
| `log` | Timestamped log line |
| `completed` | Success + summary |
| `error` | Domain/unexpected failure |

---

## Single-task sync

`SyncTaskAsync(taskId)`:

1. Require local task with `clickup_task_id`.
2. Fetch remote task; ensure client; apply API fields.
3. Resolve descendants under that task (same helper as full sync).
4. Return updated `TaskDto` (includes `needsAttention`).

Does **not** create a `clickup_sync_run` or refresh all client bill fields.

---

## Bidirectional bill / hours (outside full sync)

When a user updates prep fields in the app (`TaskService`):

| Local action | ClickUp side effect |
|---|---|
| Change `bill` | Set Billable custom field (if client `bill_field_available`) |
| Set `bill=yes` | Add configured `AssigneeId` if missing (keeps task in sync filter) |
| Increase billable hours above tracked | Create billable time entry for the delta |
| Decrease billable hours below tracked | Local save only + warning (no delete) |

If ClickUp returns “field missing”, client is marked `bill_field_available=false`.

---

## Mapping summary (ClickUp → AIB)

| ClickUp | AIB |
|---|---|
| Workspace (`team`) | `clickup_container` type `workspace`; agency is separate |
| Space / Folder / List | `clickup_container` |
| Folder (or folderless list) | `client` |
| Task / Subtask | `task` (`clickup_parent_id` for nesting) |
| Billable dropdown | `task.bill` |
| Tracked time | `task.actual_hours` (+ hour fill into billable/non-billable) |
| — | `project` / invoice assignment are **manual** |

Agency ↔ Workspace is configuration (`TeamId`), not a synced FK.

---

## Operational notes

- Sync is **additive/updating**; it does not delete local tasks missing from ClickUp.
- Closed tasks are included (`include_closed=true`).
- Hierarchy rebuild replaces container names/parents via upsert; orphaned containers from deleted ClickUp locations are not pruned automatically.
- Full log is stored on `clickup_sync_run.log` for debugging (Sync UI history).
- See [api.md](./api.md) for HTTP endpoints (including SSE sync and task patches).
- See [clickup-data-hierarchy.md](./clickup-data-hierarchy.md) for UI vs API naming (`team` = Workspace).
- See [entity-db-structure.md](./entity-db-structure.md) for column ownership and billing rules.
