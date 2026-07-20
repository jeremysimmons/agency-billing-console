# ClickUp Data Hierarchy: UI Names and API Names

ClickUp’s current UI hierarchy is:

```text
Workspace
└── Space
    ├── List
    │   └── Task
    │       └── Subtask
    │           └── Nested subtask
    └── Folder
        ├── List
        │   └── Task
        └── Subfolder
            └── List
                └── Task
```

Folders and subfolders are optional, but **every task must belong to at least one List**. A task can also be added to additional Lists while retaining one home List.

## UI names compared with API names

| Hierarchy level | Current UI name | ClickUp API name | Important note |
|---|---|---|---|
| 1 | Workspace | `team` in API v2; `workspace` in newer API v3 terminology | In v2, `team_id` means the Workspace ID, not a user group |
| 2 | Space | `space` | Direct child of a Workspace |
| 3 | Folder | `folder` | Optional container beneath a Space |
| 4 | Subfolder | Typically represented as a nested folder in newer hierarchy features | API support and naming vary by endpoint/version |
| 4 or 5 | List | `list` | Required container for tasks |
| Work item | Task | `task` | Must have a home List |
| Child work item | Subtask | Also `task` | Distinguished by a non-null `parent` task ID |
| Deeper child | Nested subtask | Also `task` | `parent` points to its immediate parent task |

The largest naming discrepancy is:

```text
ClickUp UI: Workspace
ClickUp API v2: Team
```

ClickUp API v2 uses `team` as the legacy term for a Workspace. Thus:

```http
GET /api/v2/team/{team_id}/space
```

means:

> Get the Spaces inside the Workspace identified by `team_id`.

It does **not** mean a current ClickUp Team or user group.

## Container hierarchy

### 1. Workspace

**UI name:** Workspace  
**API v2 name:** Team  
**Common identifier:** `team_id`

The Workspace contains the organization’s:

- users
- Spaces
- Folders
- Lists
- tasks
- settings

For the application data model, use an internal name such as:

```text
ClickUpWorkspace
- external_workspace_id
```

Even when consuming API v2, map `team_id` into `external_workspace_id`. Avoid propagating the misleading term `team_id` throughout the internal domain model.

### 2. Space

**UI name:** Space  
**API name:** Space  
**Identifier:** `space_id`

A Space is a major organizational division beneath the Workspace.

Common uses include:

- department
- agency team
- broad work type
- collection of clients
- high-level initiative

A Space can contain:

```text
Folders
Standalone Lists
```

### 3. Folder

**UI name:** Folder  
**API name:** Folder  
**Identifier:** `folder_id`

Folders are optional organizational containers under a Space.

A Folder typically contains one or more Lists:

```text
Space
└── Folder
    ├── List A
    └── List B
```

Older ClickUp terminology sometimes called Folders “projects.” Do not map that legacy API concept directly to the internal `Project` entity merely because of the old name.

For the application:

```text
ClickUp Folder ≠ automatically Internal Project
```

A ClickUp Folder could instead map to:

- an internal Client,
- an internal Project,
- or no internal entity.

### 4. Subfolder

**UI name:** Subfolder  
**Concept:** Folder nested inside another Folder

Model containers generically enough to support nested folders:

```text
ExternalContainer
- id
- external_connection_id
- external_id
- parent_container_id nullable
- container_type
```

Possible types:

```text
workspace
space
folder
list
```

A subfolder does not need a separate database type. It can be represented as:

```text
container_type = folder
parent_container_id = another folder
```

This is safer than hard-coding a flat `Space → Folder → List` structure.

### 5. List

**UI name:** List  
**API name:** List  
**Identifier:** `list_id`

A List is the immediate organizational home of a task.

Lists may exist:

```text
directly inside a Space
```

or:

```text
inside a Folder or Subfolder
```

Tasks cannot exist outside Lists.

Examples:

```text
Workspace
└── Space
    └── List
        └── Task
```

and:

```text
Workspace
└── Space
    └── Folder
        └── List
            └── Task
```

## Tasks and subtasks

### Task

**UI name:** Task  
**API name:** Task  
**Identifier:** `task_id`

A task belongs to a home List. ClickUp also supports adding a task to additional Lists, but its statuses and Custom Fields are generally determined by its home List.

The imported task model should distinguish:

```text
home_list_id
additional_list_ids
```

A minimal version could initially store only the home List, but keeping this distinction in mind prevents mapping ambiguity later.

### Subtask

**UI name:** Subtask  
**API name:** Task  
**Identifier:** `task_id`  
**Parent field:** `parent`

The API does not expose subtasks as a separate core entity type. A subtask is a normal task object whose `parent` property contains another task ID.

```json
{
  "id": "4567",
  "parent": "1234"
}
```

This means task `4567` is a child of task `1234`.

A top-level task has:

```json
{
  "parent": null
}
```

Nested subtasks reference their **immediate parent**, not necessarily the top-level task.

For example:

```text
Task 1234
└── Subtask 4567
    └── Nested subtask 9876
```

The API relationships are:

```text
1234.parent = null
4567.parent = 1234
9876.parent = 4567
```

Therefore, the internal import table should use:

```text
ExternalWorkItem
- external_id
- external_parent_work_item_id nullable
- home_list_id
```

Separate external tables for tasks and subtasks are unnecessary.

## Recommended representation in the application

```text
ExternalWorkspace
- id
- connection_id
- clickup_workspace_id
- name
```

```text
ExternalContainer
- id
- connection_id
- workspace_id
- parent_container_id nullable
- clickup_id
- container_type
- name
- archived
```

Supported container types:

```text
space
folder
list
```

A subfolder is simply:

```text
container_type = folder
parent_container_id = another folder
```

Then:

```text
ExternalWorkItem
- id
- connection_id
- clickup_task_id
- home_list_container_id
- parent_work_item_id nullable
- name
- status
- is_closed
- source_updated_at
```

Optionally:

```text
ExternalWorkItemList
- external_work_item_id
- external_list_container_id
- is_home_list
```

That join table supports ClickUp’s ability to place a task in multiple Lists.

## Practical hierarchy example

Suppose the UI displays:

```text
Workspace: Agency Workspace
└── Space: Client Work
    └── Folder: Acme Corporation
        └── Subfolder: Website Redesign
            └── List: Development
                └── Task: Build checkout
                    └── Subtask: Add validation
```

A sensible internal mapping might be:

| ClickUp record | Internal record |
|---|---|
| Workspace `Agency Workspace` | Agency |
| Space `Client Work` | No direct mapping |
| Folder `Acme Corporation` | Client |
| Subfolder `Website Redesign` | Project |
| List `Development` | Project section or mapping rule |
| Task `Build checkout` | Task |
| Subtask `Add validation` | Child Task |

This should remain configurable. Another ClickUp Workspace might use:

```text
Space → Client
Folder → Project
List → Work queue
```

The importer should preserve the full ClickUp hierarchy first, then use explicit mappings to associate containers with internal clients and projects.

## Key terminology rule for implementation

Use these internal property names:

```text
external_workspace_id
external_space_id
external_folder_id
external_list_id
external_task_id
external_parent_task_id
```

Translate ClickUp API v2’s `team_id` only at the integration boundary:

```text
ClickUp API team_id
        ↓
application external_workspace_id
```

Do not call the Workspace a “team” inside the domain model. This avoids confusion with ClickUp’s actual user groups and makes a future move from API v2 to v3 terminology easier.

## References

- [ClickUp: Intro to Lists](https://help.clickup.com/hc/en-us/articles/6311877646999-Intro-to-Lists)
- [ClickUp: Intro to the Hierarchy](https://help.clickup.com/hc/en-us/articles/13856392825367-Intro-to-the-Hierarchy)
- [ClickUp API: General v2 and v3 API terminology](https://developer.clickup.com/docs/general-v2-v3-api)
- [ClickUp API FAQ](https://developer.clickup.com/docs/faq)
- [ClickUp: Space, Folder, Subfolder, and List settings](https://help.clickup.com/hc/en-us/articles/33777837994775-Space-Folder-Subfolder-and-List-settings)
- [ClickUp: Intro to tasks](https://help.clickup.com/hc/en-us/articles/10552031987735-Intro-to-tasks)
