-- Phase 2 ClickUp ingestion: external staging tables kept separate from internal records.
-- Enum-like columns stored as text (PascalCase names matching the C# enums).

create table external_connection (
    id                       uuid primary key,
    agency_id                uuid not null references agency (id),
    provider_type            text not null default 'clickup',
    name                     text not null,
    external_workspace_id    text,
    authentication_reference text not null,
    status                   text not null default 'Active',
    last_successful_sync_at  timestamptz,
    last_attempted_sync_at   timestamptz,
    created_at               timestamptz not null,
    updated_at               timestamptz not null
);
create index ix_external_connection_agency on external_connection (agency_id);
create unique index ux_external_connection_provider_workspace
    on external_connection (provider_type, external_workspace_id);

create table external_identity (
    id                    uuid primary key,
    external_connection_id uuid not null references external_connection (id) on delete cascade,
    user_id               uuid references app_user (id),
    contractor_id         uuid references contractor (id),
    external_user_id      text not null,
    external_username     text,
    external_email        text,
    active                boolean not null default true,
    last_synced_at        timestamptz,
    created_at            timestamptz not null,
    updated_at            timestamptz not null
);
create unique index ux_external_identity_conn_user
    on external_identity (external_connection_id, external_user_id);

create table external_container (
    id                     uuid primary key,
    external_connection_id uuid not null references external_connection (id) on delete cascade,
    external_parent_id     text,
    external_id            text not null,
    container_type         text not null,
    name                   text not null,
    archived               boolean not null default false,
    raw_data_json          jsonb,
    first_seen_at          timestamptz not null,
    last_seen_at           timestamptz not null,
    created_at             timestamptz not null,
    updated_at             timestamptz not null
);
create unique index ux_external_container_conn_type_id
    on external_container (external_connection_id, container_type, external_id);
create index ix_external_container_external_id on external_container (external_id);
create index ix_external_container_parent on external_container (external_parent_id);

create table external_work_item (
    id                          uuid primary key,
    external_connection_id      uuid not null references external_connection (id) on delete cascade,
    external_container_id       uuid references external_container (id),
    external_parent_work_item_id text,
    external_id                 text not null,
    item_type                   text not null default 'Task',
    name                        text not null,
    description                 text,
    status_name                 text,
    status_type                 text,
    is_closed                   boolean not null default false,
    archived                    boolean not null default false,
    assignee_external_user_id   text,
    start_date                  timestamptz,
    due_date                    timestamptz,
    completed_at                timestamptz,
    time_estimate_minutes       integer,
    time_spent_minutes          integer,
    url                         text,
    source_created_at           timestamptz,
    source_updated_at           timestamptz,
    raw_data_json               jsonb,
    first_seen_at               timestamptz not null,
    last_seen_at                timestamptz not null,
    last_synced_at              timestamptz not null
);
create unique index ux_external_work_item_conn_id
    on external_work_item (external_connection_id, external_id);
create index ix_external_work_item_external_id on external_work_item (external_id);
create index ix_external_work_item_source_updated on external_work_item (source_updated_at);
create index ix_external_work_item_parent on external_work_item (external_parent_work_item_id);
create index ix_external_work_item_container on external_work_item (external_container_id);

create table external_time_entry (
    id                            uuid primary key,
    external_connection_id        uuid not null references external_connection (id) on delete cascade,
    external_work_item_id         uuid references external_work_item (id),
    external_work_item_external_id text,
    external_user_id              text not null,
    external_id                   text not null,
    work_date                     date not null,
    started_at                    timestamptz,
    ended_at                      timestamptz,
    duration_minutes              integer not null default 0,
    description                   text,
    billable                      boolean,
    source_created_at             timestamptz,
    source_updated_at             timestamptz,
    raw_data_json                 jsonb,
    last_synced_at                timestamptz not null
);
create unique index ux_external_time_entry_conn_id
    on external_time_entry (external_connection_id, external_id);
create index ix_external_time_entry_external_id on external_time_entry (external_id);
create index ix_external_time_entry_work_item on external_time_entry (external_work_item_id);

create table import_run (
    id                     uuid primary key,
    external_connection_id uuid not null references external_connection (id) on delete cascade,
    import_type            text not null,
    status                 text not null default 'Queued',
    started_at             timestamptz not null,
    completed_at           timestamptz,
    source_updated_after   timestamptz,
    records_fetched        integer not null default 0,
    records_created        integer not null default 0,
    records_updated        integer not null default 0,
    records_unchanged      integer not null default 0,
    records_failed         integer not null default 0,
    error_summary          text,
    triggered_by_user_id   uuid references app_user (id)
);
create index ix_import_run_connection on import_run (external_connection_id);
create index ix_import_run_started on import_run (started_at);

create table import_record (
    id                   uuid primary key,
    import_run_id        uuid not null references import_run (id) on delete cascade,
    external_entity_type text not null,
    external_entity_id   text not null,
    action               text not null,
    status               text not null,
    external_record_id   uuid,
    error_message        text,
    imported_at          timestamptz not null
);
create index ix_import_record_run on import_record (import_run_id);
create index ix_import_record_status on import_record (status);

create table sync_cursor (
    id                     uuid primary key,
    external_connection_id uuid not null references external_connection (id) on delete cascade,
    entity_type            text not null,
    cursor_value           text,
    last_source_updated_at timestamptz,
    last_successful_sync_at timestamptz
);
create unique index ux_sync_cursor_conn_entity
    on sync_cursor (external_connection_id, entity_type);
