# Contractor Billing and ClickUp Integration System

## 1. Objective

Build a web application for a single contractor working for one agency.

The contractor performs work for multiple agency clients. Work may belong to a project or may exist as a standalone task. Tasks may contain nested subtasks.

The system must:

* Import work and time data regularly from ClickUp.
* Map ClickUp teams, folders, lists, tasks, subtasks, and users to internal records.
* Track pending, completed, finalized, and invoiced work.
* Support billing periods, task rollups, time entries, expenses, invoices, and payments.
* Allow agency users to sign in and view authorized work and invoices.
* Support username/password, one-hour email magic links, and Google Workspace login.
* Use PostgreSQL as the authoritative datastore.

---

# 2. Recommended Technology

## Backend

Use a conventional server-side application framework with PostgreSQL.

Recommended .NET implementation:

* ASP.NET Core
* PostgreSQL through Npgsql and Dapper
* ASP.NET Core Identity
* Google OpenID Connect authentication
* Background jobs using Hangfire, Quartz.NET, or a hosted worker
* REST API for frontend communication

## Frontend

Use VueJS, Nuxt and Nuxt UI.

The frontend should communicate only with the backend API. It should not communicate directly with ClickUp or PostgreSQL.

## Supporting services

Use:

* Transactional email provider for magic links, invitations, and invoices
* Secret manager for ClickUp API credentials and Google client secrets
* Object storage for generated invoice PDFs and expense receipts
* Structured application logging
* Scheduled background processing

Firebase is not required. It may optionally be used for push notifications or analytics, but not as the primary database.

---

# 3. Architectural Layers

Organize the application into these layers:

```text
Presentation
Application Services
Domain Model
Persistence
External Integrations
Background Jobs
```

## Presentation

Contains:

* Web UI
* API controllers
* Request validation
* Authentication middleware
* Authorization policies

## Application Services

Contains use cases such as:

* Import ClickUp data
* Map external tasks
* Finalize completed work
* Create a billing period
* Generate an invoice
* Record a payment
* Invite an agency user

## Domain Model

Contains business rules for:

* Clients, projects, tasks, and subtasks
* Rollup calculations
* Work status and billing status
* Billing periods
* Invoice generation
* Access permissions

## Persistence

Contains:

* PostgreSQL schema
* Entity Framework mappings
* Repositories or query services
* Database migrations
* Transactions

## External Integrations

Contains:

* ClickUp API client
* Google OpenID Connect
* Email provider
* PDF generation
* Object storage

## Background Jobs

Contains:

* Scheduled ClickUp imports
* Incremental synchronization
* Invoice generation
* Email delivery
* Cleanup of expired authentication tokens

---

# 4. Primary Domain Model

## Agency

The company that hires the contractor and pays invoices.

```text
Agency
- id
- name
- billing_email
- billing_address
- currency
- payment_terms_days
- active
- created_at
- updated_at
```

The first version may contain only one agency, but retain the entity boundary.

## Contractor

```text
Contractor
- id
- name
- email
- default_hourly_rate
- active
- created_at
- updated_at
```

## Client

```text
Client
- id
- agency_id
- name
- code
- description nullable
- status
- active
- created_at
- updated_at
```

## Project

A project belongs to one client.

```text
Project
- id
- client_id
- name
- code nullable
- description nullable
- status
- billing_type
- hourly_rate nullable
- fixed_fee nullable
- budget_minutes nullable
- budget_amount nullable
- start_date nullable
- end_date nullable
- active
- created_at
- updated_at
```

Supported billing types:

```text
hourly
fixed_fee
non_billable
```

## Task

A task always belongs to a client. A project is optional.

A subtask is represented by another task with a populated `parent_task_id`.

```text
Task
- id
- client_id
- project_id nullable
- parent_task_id nullable
- title
- description nullable
- work_status
- billing_status
- billing_type
- billable
- hourly_rate nullable
- fixed_fee nullable
- estimated_minutes nullable
- estimate_rollup_mode
- actual_rollup_mode
- billing_rollup_mode
- due_date nullable
- completed_at nullable
- finalized_at nullable
- finalized_by_user_id nullable
- sort_order
- created_at
- updated_at
```

Supported work statuses:

```text
pending
in_progress
blocked
completed
cancelled
archived
```

Supported billing statuses:

```text
not_ready
pending_review
ready
finalized
invoiced
excluded
```

Task rules:

* `client_id` is required.
* `project_id` is optional.
* `parent_task_id` is optional.
* A child task must belong to the same client as its parent.
* A child task should belong to the same project as its parent.
* Descendants of standalone tasks should also have no project.
* Circular parent relationships must be prevented.
* A completed task is not automatically finalized for billing.

---

# 5. Rollup Rules

Maintain separate rollup modes for estimates, actual time, and invoice presentation.

## Estimate rollup mode

```text
direct
children
direct_and_children
```

Behavior:

* `direct`: use only the task’s own estimate.
* `children`: ignore the task’s own estimate and total descendants.
* `direct_and_children`: add the task’s estimate and descendant estimates.

## Actual rollup mode

```text
direct
children
direct_and_children
```

Behavior:

* `direct`: include time attached directly to the task.
* `children`: include time attached only to descendants.
* `direct_and_children`: include direct and descendant time.

## Billing rollup mode

```text
detailed
task
parent
project
client
```

Behavior:

* `detailed`: individual time entries may become invoice lines.
* `task`: combine entries by task.
* `parent`: combine descendant work under a selected parent task.
* `project`: combine work by project.
* `client`: combine work by client.

Do not persist rollup totals as the sole source of truth. Calculate from source records or maintain rebuildable cached summaries.

Use PostgreSQL recursive common table expressions for task hierarchy queries.

---

# 6. Time and Expense Model

## TimeEntry

```text
TimeEntry
- id
- contractor_id
- task_id
- billing_period_id nullable
- work_date
- started_at nullable
- ended_at nullable
- duration_minutes
- description nullable
- billable
- approval_status
- hourly_rate nullable
- billing_amount nullable
- invoice_line_id nullable
- created_at
- updated_at
```

Approval statuses:

```text
draft
submitted
approved
rejected
invoiced
```

Resolve the hourly rate in this order:

```text
TimeEntry.hourly_rate
Task.hourly_rate
Project.hourly_rate
Contractor.default_hourly_rate
```

Snapshot the effective rate once approved or invoiced.

## Expense

```text
Expense
- id
- contractor_id
- client_id
- project_id nullable
- task_id nullable
- billing_period_id nullable
- expense_date
- description
- amount
- reimbursable
- markup_percent nullable
- billable_amount
- approval_status
- receipt_url nullable
- invoice_line_id nullable
- created_at
- updated_at
```

---

# 7. Billing Periods

## BillingPeriod

```text
BillingPeriod
- id
- agency_id
- period_start
- period_end
- status
- billing_frequency
- invoice_grouping_mode
- opened_at nullable
- review_started_at nullable
- approved_at nullable
- invoiced_at nullable
- closed_at nullable
- invoice_id nullable
- created_at
- updated_at
```

Statuses:

```text
open
review
approved
invoiced
closed
```

Frequencies:

```text
weekly
biweekly
semimonthly
monthly
custom
```

## BillingPeriodItem

Use explicit billing-period items to freeze what was reviewed.

```text
BillingPeriodItem
- id
- billing_period_id
- item_type
- task_id nullable
- time_entry_id nullable
- expense_id nullable
- source_status
- included
- exclusion_reason nullable
- finalized_at nullable
- created_at
```

The finalization workflow should be:

```text
Imported
Pending
Completed
Pending review
Finalized
Invoiced
```

Once finalized, included quantities and rates must not change silently.

---

# 8. Invoice Model

## Invoice

```text
Invoice
- id
- agency_id
- contractor_id
- billing_period_id nullable
- invoice_number
- invoice_date
- due_date
- status
- currency
- subtotal
- tax_amount
- total_amount
- balance_due
- notes nullable
- sent_at nullable
- paid_at nullable
- created_at
- updated_at
```

Statuses:

```text
draft
submitted
sent
partially_paid
paid
void
overdue
```

## InvoiceLine

```text
InvoiceLine
- id
- invoice_id
- client_id
- project_id nullable
- task_id nullable
- parent_task_id nullable
- line_type
- description
- quantity
- unit
- unit_price
- amount
- sort_order
- created_at
```

Line types:

```text
time
fixed_fee
expense
adjustment
discount
```

Invoice lines are immutable snapshots of billing data.

Changing a client, project, task, description, or rate later must not alter issued invoices.

## Payment

```text
Payment
- id
- invoice_id
- payment_date
- amount
- payment_method
- reference_number nullable
- notes nullable
- created_at
```

Support multiple and partial payments.

---

# 9. User and Authorization Model

## User

```text
User
- id
- agency_id nullable
- contractor_id nullable
- username
- normalized_username
- email
- normalized_email
- display_name
- status
- email_verified_at nullable
- password_login_enabled
- magic_link_enabled
- social_login_enabled
- last_login_at nullable
- created_at
- updated_at
```

Constraints:

* Unique normalized username.
* Unique normalized email.

Statuses:

```text
invited
active
suspended
disabled
```

## Role

```text
Role
- id
- name
```

Initial roles:

```text
contractor_admin
contractor
agency_admin
agency_manager
agency_viewer
billing_viewer
```

## UserRole

```text
UserRole
- user_id
- role_id
```

## ClientAccess

Use this to restrict agency users to specific clients.

```text
ClientAccess
- user_id
- client_id
- access_level
```

Access levels:

```text
view
manage
billing
```

Authorization must be enforced by the backend, not only by hiding frontend controls.

---

# 10. Authentication

Every user supports username and password authentication.

Users may additionally authenticate through a one-hour magic link or linked Google Workspace identity.

## Local credentials

```text
LocalCredential
- id
- user_id
- password_hash
- password_changed_at
- must_change_password
- failed_attempt_count
- locked_until nullable
- last_failed_at nullable
- created_at
- updated_at
```

Use Argon2id or the framework’s secure password-hashing implementation.

Never store plain-text or reversibly encrypted passwords.

## Magic links

```text
MagicLinkToken
- id
- user_id
- token_hash
- purpose
- requested_at
- expires_at
- consumed_at nullable
- revoked_at nullable
- request_ip nullable
- request_user_agent nullable
- created_at
```

Requirements:

* Login links expire after exactly one hour.
* Tokens are single-use.
* Store only a cryptographic hash of the token.
* Return the same response whether an email exists or not.
* Rate-limit magic-link requests.
* Revoke outstanding links after successful use.
* Do not expose tokens in logs.

## Google identity

```text
IdentityProvider
- id
- provider_type
- name
- issuer
- client_id
- secret_reference
- enabled
- created_at
- updated_at
```

```text
SocialIdentity
- id
- user_id
- identity_provider_id
- provider_subject
- provider_email
- normalized_provider_email
- provider_email_verified
- hosted_domain nullable
- linked_at
- last_login_at nullable
- created_at
- updated_at
```

Initial linking flow:

1. Validate the Google ID token.
2. Require a verified Google email.
3. Normalize the supplied email.
4. Find exactly one active internal user with the same email.
5. Confirm that the Google subject is not linked elsewhere.
6. Link the identity to that user.
7. Use provider subject for future logins.

Do not automatically create a new internal user from Google login.

Optionally require the Google `hd` claim to match the agency’s configured Workspace domain.

## Sessions and audit

```text
UserSession
- id
- user_id
- session_token_hash
- authentication_method
- identity_provider_id nullable
- created_at
- expires_at
- last_seen_at nullable
- revoked_at nullable
- ip_address nullable
- user_agent nullable
```

Authentication methods:

```text
password
magic_link
google
```

Also record authentication events for successful logins, failed logins, password changes, account linking, logout, and session revocation.

---

# 11. ClickUp Integration Model

Keep imported ClickUp records separate from internal business records.

The integration architecture must preserve three distinct concepts:

```text
External ClickUp record
Internal application record
Mapping between them
```

## ExternalConnection

```text
ExternalConnection
- id
- agency_id
- provider_type
- name
- external_workspace_id nullable
- authentication_reference
- status
- last_successful_sync_at nullable
- last_attempted_sync_at nullable
- created_at
- updated_at
```

Do not store ClickUp API tokens directly in the database. Store a secret-manager reference.

## ExternalIdentity

Use this to store the contractor’s ClickUp user ID.

```text
ExternalIdentity
- id
- external_connection_id
- user_id nullable
- contractor_id nullable
- external_user_id
- external_username nullable
- external_email nullable
- active
- last_synced_at nullable
- created_at
- updated_at
```

Add a unique constraint on:

```text
external_connection_id
external_user_id
```

## ExternalContainer

Represent ClickUp workspace, team, space, folder, and list records.

```text
ExternalContainer
- id
- external_connection_id
- external_parent_id nullable
- external_id
- container_type
- name
- archived
- raw_data_json nullable
- first_seen_at
- last_seen_at
- created_at
- updated_at
```

Container types:

```text
workspace
team
space
folder
list
```

Unique constraint:

```text
external_connection_id
container_type
external_id
```

## ExternalWorkItem

Store the latest imported state of each ClickUp task or subtask.

```text
ExternalWorkItem
- id
- external_connection_id
- external_container_id
- external_parent_work_item_id nullable
- external_id
- item_type
- name
- description nullable
- status_name nullable
- status_type nullable
- is_closed
- archived
- assignee_external_user_id nullable
- start_date nullable
- due_date nullable
- completed_at nullable
- time_estimate_minutes nullable
- time_spent_minutes nullable
- url nullable
- source_created_at nullable
- source_updated_at nullable
- raw_data_json nullable
- first_seen_at
- last_seen_at
- last_synced_at
```

## ExternalTimeEntry

```text
ExternalTimeEntry
- id
- external_connection_id
- external_work_item_id
- external_user_id
- external_id
- work_date
- started_at nullable
- ended_at nullable
- duration_minutes
- description nullable
- billable nullable
- source_created_at nullable
- source_updated_at nullable
- raw_data_json nullable
- last_synced_at
```

Add a unique constraint on the connection and ClickUp time-entry ID.

---

# 12. External-to-Internal Mapping

Prefer strongly typed mapping tables over a single polymorphic table where practical.

## ExternalContainerMapping

```text
ExternalContainerMapping
- id
- external_container_id
- client_id nullable
- project_id nullable
- mapping_status
- mapping_source
- mapped_by_user_id nullable
- mapped_at nullable
- notes nullable
```

Examples:

```text
ClickUp folder -> Client
ClickUp list -> Project
ClickUp list -> Client for standalone tasks
```

## ExternalTaskMapping

```text
ExternalTaskMapping
- id
- external_work_item_id
- task_id
- mapping_status
- mapping_source
- mapped_by_user_id nullable
- mapped_at nullable
- notes nullable
```

## TimeEntrySource

```text
TimeEntrySource
- id
- time_entry_id
- external_time_entry_id
- imported_duration_minutes
- imported_at
```

Mapping statuses:

```text
suggested
confirmed
ignored
conflict
unmapped
```

Mapping sources:

```text
manual
rule
name_match
parent_mapping
import_created
```

Once a mapping exists, use IDs rather than names.

---

# 13. ClickUp Status Mapping

Do not directly copy ClickUp status strings into the internal task status.

## ExternalStatusMapping

```text
ExternalStatusMapping
- id
- external_connection_id
- external_status_name
- external_status_type nullable
- internal_status
- treated_as_completed
- treated_as_billable
- active
```

For example:

```text
ClickUp "Open" -> pending
ClickUp "In Progress" -> in_progress
ClickUp "Client Review" -> in_progress
ClickUp "Complete" -> completed
ClickUp "Closed" -> completed
```

A ClickUp-completed task should become internally completed but remain `pending_review` for billing until finalized.

---

# 14. Import Processing

## ImportRun

```text
ImportRun
- id
- external_connection_id
- import_type
- status
- started_at
- completed_at nullable
- source_updated_after nullable
- records_fetched
- records_created
- records_updated
- records_unchanged
- records_failed
- error_summary nullable
- triggered_by_user_id nullable
```

Import types:

```text
full
incremental
manual
retry
```

Statuses:

```text
queued
running
completed
completed_with_errors
failed
```

## ImportRecord

```text
ImportRecord
- id
- import_run_id
- external_entity_type
- external_entity_id
- action
- status
- external_record_id nullable
- internal_entity_type nullable
- internal_entity_id nullable
- error_message nullable
- imported_at
```

## SyncCursor

```text
SyncCursor
- id
- external_connection_id
- entity_type
- cursor_value nullable
- last_source_updated_at nullable
- last_successful_sync_at nullable
```

Only advance the cursor after the relevant import transaction succeeds.

## Import workflow

Each scheduled import should:

1. Create an `ImportRun`.
2. Fetch ClickUp records changed since the last successful cursor.
3. Upsert external containers.
4. Upsert external tasks and subtasks.
5. Upsert external time entries.
6. Resolve existing mappings by external ID.
7. Apply list-to-project and folder-to-client mappings.
8. Apply parent-task mappings for subtasks.
9. Create suggested mappings for unmatched items.
10. Update internal task state using status mappings.
11. Create or update internal time entries from imported time.
12. Recalculate task rollups.
13. Flag newly completed tasks as pending billing review.
14. Save import results and errors.
15. Advance the sync cursor only after successful completion.

Imports must be idempotent. Reprocessing the same ClickUp data must not duplicate tasks or time entries.

---

# 15. Mapping Resolution Priority

Use this priority order:

```text
1. Existing external task mapping
2. Existing external parent-task mapping
3. ClickUp list to project mapping
4. ClickUp folder or list to client mapping
5. Configured external identifier rule
6. Exact normalized name match
7. Suggested match requiring manual confirmation
```

Never silently overwrite a confirmed mapping.

Conflicts should enter a review queue.

---

# 16. Agency Dashboard

Provide four primary views.

## Pending work

Show tasks where internal work status is not completed or cancelled.

Display:

* Client
* Project
* Task hierarchy
* ClickUp status
* Internal status
* Assignee
* Estimate
* Actual time
* Due date
* Last synchronized time

## Completed work

Show completed ClickUp tasks that have not been finalized.

Display:

* Client
* Project
* Task
* Completion date
* Estimated time
* Imported actual time
* Billable time
* Billing amount
* Mapping warnings

Actions:

* Confirm time
* Adjust billable amount
* Exclude from billing
* Finalize
* Assign to billing period

## Finalized work

Show work included in an approved billing period but not yet invoiced.

Finalized records should be read-only except for an explicit reopen action with an audit entry.

## Invoices

Show:

* Invoice number
* Billing period
* Included clients and projects
* Total hours
* Expenses
* Total amount
* Status
* Due date
* Payment status
* PDF download

Agency users should only see clients permitted by their role and `ClientAccess` records.

---

# 17. Database Constraints and Indexes

Add foreign keys for all internal relationships.

Add indexes for:

```text
Task.client_id
Task.project_id
Task.parent_task_id
Task.work_status
Task.billing_status
TimeEntry.task_id
TimeEntry.work_date
TimeEntry.billing_period_id
Invoice.billing_period_id
ExternalContainer.external_id
ExternalWorkItem.external_id
ExternalWorkItem.source_updated_at
ExternalTimeEntry.external_id
ExternalTaskMapping.external_work_item_id
ExternalTaskMapping.task_id
ImportRun.started_at
```

Add unique constraints for:

```text
User.normalized_username
User.normalized_email
SocialIdentity(identity_provider_id, provider_subject)
ExternalIdentity(external_connection_id, external_user_id)
ExternalContainer(external_connection_id, container_type, external_id)
ExternalWorkItem(external_connection_id, external_id)
ExternalTimeEntry(external_connection_id, external_id)
ExternalTaskMapping.external_work_item_id
```

Use database transactions for:

* Finalizing billing periods
* Generating invoices
* Applying payments
* Linking social identities
* Completing imports and advancing cursors

---

# 18. Security Requirements

* Store secrets outside the application database.
* Hash passwords securely.
* Hash magic-link and session tokens.
* Rate-limit login, reset, and magic-link endpoints.
* Validate Google issuer, audience, signature, nonce, and expiration.
* Require verified Google email before initial linking.
* Enforce agency and client access on every backend query.
* Audit mapping changes, finalization, invoice changes, authentication, and payments.
* Do not expose ClickUp API tokens to the browser.
* Sanitize imported ClickUp HTML or rich-text content before rendering.
* Use HTTPS everywhere.
* Protect state-changing endpoints against CSRF when cookie authentication is used.

---

# 19. Suggested Implementation Phases

## Phase 1: Foundation

Implement:

* PostgreSQL schema
* Agency, contractor, users, roles, and client access
* Username/password authentication
* Magic-link authentication
* Google Workspace account linking
* Client, project, task, and subtask management

## Phase 2: ClickUp ingestion

Implement:

* ClickUp API client
* External connections
* External users, containers, tasks, and time entries
* Import runs and sync cursors
* Scheduled incremental imports
* Import diagnostics

## Phase 3: Mapping

Implement:

* Folder/list-to-client/project mappings
* ClickUp task-to-internal-task mappings
* Automatic mapping rules
* Suggested mapping review screen
* Status mappings
* Conflict handling

## Phase 4: Time and rollups

Implement:

* Internal time entries
* Imported time-entry linking
* Estimate and actual rollups
* Parent and descendant task calculations
* Completed-work review queue

## Phase 5: Billing

Implement:

* Billing periods
* Billing-period items
* Work finalization
* Invoice generation
* Invoice lines
* Expenses
* Payments
* Invoice PDF generation

## Phase 6: Agency portal

Implement:

* Pending-work dashboard
* Completed-work review
* Finalized-work view
* Invoice history
* Client-based authorization
* Audit views

## Phase 7: Hardening

Implement:

* Automated tests
* Retry behavior
* Import idempotency tests
* Security tests
* Performance indexes
* Monitoring and alerting
* Backup and restore procedures

---

# 20. Initial API Surface

Suggested endpoints:

```text
POST   /auth/login
POST   /auth/magic-link/request
GET    /auth/magic-link/consume
GET    /auth/google/start
GET    /auth/google/callback
POST   /auth/logout

GET    /clients
POST   /clients
GET    /projects
POST   /projects
GET    /tasks
POST   /tasks
PATCH  /tasks/{id}

GET    /integrations/clickup/connections
POST   /integrations/clickup/import
GET    /integrations/clickup/imports
GET    /integrations/clickup/unmapped
POST   /integrations/clickup/mappings
PATCH  /integrations/clickup/mappings/{id}

GET    /work/pending
GET    /work/completed
GET    /work/finalized
POST   /work/{taskId}/finalize
POST   /work/{taskId}/exclude

GET    /billing-periods
POST   /billing-periods
POST   /billing-periods/{id}/review
POST   /billing-periods/{id}/approve
POST   /billing-periods/{id}/generate-invoice

GET    /invoices
GET    /invoices/{id}
POST   /invoices/{id}/send
POST   /invoices/{id}/payments
```

---

# 21. Testing Requirements

Create tests for:

* Recursive task hierarchy calculations
* Circular task prevention
* Client and project consistency
* Every rollup mode
* Duplicate ClickUp imports
* Changed ClickUp task status
* Subtask mapping through parent mappings
* Duplicate time-entry prevention
* Billing-period finalization
* Invoice snapshot immutability
* Partial payments
* Expired and reused magic links
* Google email matching
* Conflicting Google identities
* Agency and client-level authorization

Include integration tests against PostgreSQL rather than relying entirely on an in-memory database.

---

# 22. Definition of Done

The first production-ready version is complete when:

* Every user can log in using username and password.
* Users can request a single-use email login link valid for one hour.
* A verified Google Workspace account can be linked by exact email match.
* ClickUp data imports automatically on a schedule.
* Repeated imports do not create duplicates.
* ClickUp lists and tasks can be mapped to internal clients, projects, and tasks.
* The contractor’s ClickUp user ID is mapped through an external identity.
* Tasks and subtasks correctly roll up estimates and actual time.
* Completed ClickUp work appears in a billing review queue.
* Work can be finalized into a billing period.
* An invoice can be generated from finalized work.
* Agency users can view authorized pending work, completed work, finalized work, and invoices.
* Historical invoices remain unchanged when current task or rate data changes.

The agent should begin with the PostgreSQL schema and authentication boundaries before implementing the ClickUp importer.

---

# 23. Progress Tracker

Milestones map to Section 19 phases.

- [x] **M1 — Phase 1: Foundation** (DONE, verified)
  - PostgreSQL schema + dbup-postgresql migrations
  - Agency, contractor, users, roles, client access
  - Username/password auth, magic-link (1h single-use), Google Workspace linking
  - Client/project/task/subtask management + REST API
  - CSRF protection, seeded owner
- [x] **M2 — Vue SPA** (DONE, verified)
  - Vite (https:3000 + `/api` proxy), Pinia + Pinia Colada + PrimeVue + router
  - `api/http.ts` CSRF interceptor, `stores/auth.ts`, `queries/{clients,projects,tasks}.ts`
  - Views: Login (password + magic-link + Google GIS), MagicLink consume, Dashboard, Clients, Client detail
  - Route guards; vue-tsc clean; prod build OK; live E2E green
- [x] **M3 — Phase 2: ClickUp ingestion** (DONE, verified)
  - ClickUp v2 REST client (retry/backoff), external connection seeded from config
  - External staging: `external_connection/identity/container/work_item/time_entry` (+ `import_run/import_record/sync_cursor`), Script0002
  - Idempotent upserts (unique on connection+external_id); work-item unchanged-detection via `source_updated_at`
  - Import runs + per-entity diagnostics; sync cursors advanced only on success; incremental via `date_updated_gt` watermark
  - Quartz scheduled incremental job (`ClickUpImportJob`, cron-configurable; disabled in dev)
  - API: `GET /api/integrations/clickup/connections`, `POST .../import`, `GET .../imports`
  - Verified live: 336 work items / 75 containers / 9 identities / 47 time entries; repeat full import = 0 created, 336 unchanged, 0 failed; incremental fetched 1 (watermark)
- [ ] **M4 — Phase 3: Mapping** (folder/list→client/project, task mappings, rules, review, status mappings, conflicts)
- [ ] **M5 — Phase 4: Time & rollups** (internal time entries, imported linking, estimate/actual rollups, review queue)
- [ ] **M6 — Phase 5: Billing** (billing periods/items, finalization, invoices, lines, expenses, payments, PDF)
- [ ] **M7 — Phase 6: Agency portal** (pending/completed/finalized dashboards, invoice history, client authz, audit)
- [ ] **M8 — Phase 7: Hardening + deploy** (tests, idempotency, security, indexes, monitoring, backup; AWS Lightsail + nginx + systemd bootstrap on Ubuntu 24)

## Decisions locked
- Stack: ASP.NET Core 10, PostgreSQL via Npgsql, dbup-postgresql migrations, Quartz.NET jobs
- Frontend: Vue 3 + Vite + Pinia + Pinia Colada + PrimeVue
- Local: postgres via brew (`postgresql@16`), no Docker
- Deploy: nginx (SSL term, `/assets/` static + `/api/` reverse proxy) + postgres + dotnet on AWS Lightsail Ubuntu 24; prod host `abc.jeremysimmons.net`; bootstrap script

## Workflow
- Pause at each milestone for review before proceeding.
