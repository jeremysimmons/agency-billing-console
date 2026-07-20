-- Phase 4: internal time entries, ClickUp time-entry linking, indexes for rollups/review.

create table time_entry (
    id                uuid primary key,
    contractor_id     uuid not null references contractor (id),
    task_id           uuid not null references task (id) on delete cascade,
    billing_period_id uuid,
    work_date         date not null,
    started_at        timestamptz,
    ended_at          timestamptz,
    duration_minutes  integer not null default 0 check (duration_minutes >= 0),
    description       text,
    billable          boolean not null default true,
    approval_status   text not null default '0',
    hourly_rate       numeric(12,2),
    billing_amount    numeric(14,2),
    invoice_line_id   uuid,
    created_at        timestamptz not null,
    updated_at        timestamptz not null
);
create index ix_time_entry_task on time_entry (task_id);
create index ix_time_entry_work_date on time_entry (work_date);
create index ix_time_entry_contractor on time_entry (contractor_id);
create index ix_time_entry_approval on time_entry (approval_status);

create table time_entry_source (
    id                         uuid primary key,
    time_entry_id              uuid not null unique references time_entry (id) on delete cascade,
    external_time_entry_id     uuid not null unique references external_time_entry (id) on delete cascade,
    imported_duration_minutes  integer not null,
    imported_at                timestamptz not null
);
create index ix_time_entry_source_external on time_entry_source (external_time_entry_id);
