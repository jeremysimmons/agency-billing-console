-- Phase 3 mapping: external ↔ internal links + ClickUp status vocabulary.

create table external_container_mapping (
    id                     uuid primary key,
    external_container_id  uuid not null unique references external_container (id) on delete cascade,
    client_id              uuid references client (id),
    project_id             uuid references project (id),
    mapping_status         text not null default '0',
    mapping_source         text not null default '0',
    mapped_by_user_id      uuid references app_user (id),
    mapped_at              timestamptz,
    notes                  text
);
create index ix_external_container_mapping_client on external_container_mapping (client_id);
create index ix_external_container_mapping_project on external_container_mapping (project_id);
create index ix_external_container_mapping_status on external_container_mapping (mapping_status);

create table external_task_mapping (
    id                     uuid primary key,
    external_work_item_id  uuid not null unique references external_work_item (id) on delete cascade,
    task_id                uuid references task (id),
    mapping_status         text not null default '0',
    mapping_source         text not null default '0',
    mapped_by_user_id      uuid references app_user (id),
    mapped_at              timestamptz,
    notes                  text
);
create index ix_external_task_mapping_task on external_task_mapping (task_id);
create index ix_external_task_mapping_status on external_task_mapping (mapping_status);

create table external_status_mapping (
    id                      uuid primary key,
    external_connection_id  uuid not null references external_connection (id) on delete cascade,
    external_status_name    text not null,
    external_status_type    text,
    internal_status         text not null,
    treated_as_completed    boolean not null default false,
    treated_as_billable     boolean not null default true,
    active                  boolean not null default true
);
create unique index ux_external_status_mapping_conn_name
    on external_status_mapping (external_connection_id, external_status_name);
