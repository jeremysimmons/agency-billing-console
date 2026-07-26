create table clickup_sync_run (
    id                   uuid primary key,
    agency_id            uuid not null references agency (id) on delete cascade,
    started_at           timestamptz not null,
    finished_at          timestamptz,
    status               text not null,
    summary              text,
    log                  text not null default '',
    containers_upserted  integer not null default 0,
    tasks_created        integer not null default 0,
    tasks_updated        integer not null default 0,
    clients_created      integer not null default 0,
    parents_fetched      integer not null default 0
);

create index ix_clickup_sync_run_agency_started
    on clickup_sync_run (agency_id, started_at desc);
