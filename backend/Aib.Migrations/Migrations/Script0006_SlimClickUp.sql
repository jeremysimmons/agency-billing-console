-- Slim ClickUp task prep: containers, sheet-shaped tasks, client folder link.

create table if not exists clickup_container (
    id                   uuid primary key,
    container_type       text not null,
    external_id          text not null,
    name                 text not null,
    parent_type          text,
    parent_external_id   text,
    updated_at           timestamptz not null
);
create unique index if not exists ux_clickup_container_external on clickup_container (external_id);
create index if not exists ix_clickup_container_type on clickup_container (container_type);
create index if not exists ix_clickup_container_parent on clickup_container (parent_external_id);

alter table client add column if not exists clickup_folder_id text;
create unique index if not exists ux_client_clickup_folder
    on client (clickup_folder_id) where clickup_folder_id is not null;

alter table agency add column if not exists last_clickup_sync_at timestamptz;
alter table agency add column if not exists last_clickup_sync_summary text;

-- Manual billing-prep columns (never overwritten by sync)
alter table task add column if not exists bill text;
alter table task add column if not exists billable_hours numeric(10, 2);
alter table task add column if not exists non_billable_hours numeric(10, 2);
alter table task add column if not exists invoice_label text;
alter table task add column if not exists note text;

-- ClickUp API columns (overwritten on sync)
alter table task add column if not exists clickup_url text;
alter table task add column if not exists clickup_task_id text;
alter table task add column if not exists clickup_parent_id text;
alter table task add column if not exists clickup_folder_id text;
alter table task add column if not exists clickup_folder_name text;
alter table task add column if not exists clickup_list_id text;
alter table task add column if not exists clickup_list_name text;
alter table task add column if not exists clickup_status text;
alter table task add column if not exists tags text;
alter table task add column if not exists date_created timestamptz;
alter table task add column if not exists date_done timestamptz;
alter table task add column if not exists date_closed timestamptz;
alter table task add column if not exists order_index bigint;
alter table task add column if not exists estimated_hours numeric(10, 2);
alter table task add column if not exists actual_hours numeric(10, 2);

create unique index if not exists ux_task_clickup_url on task (clickup_url) where clickup_url is not null;
create unique index if not exists ux_task_clickup_task_id on task (clickup_task_id) where clickup_task_id is not null;
create index if not exists ix_task_clickup_folder on task (clickup_folder_id);
create index if not exists ix_task_missing_bill on task (bill) where bill is null;

-- Drop legacy billing/work-review columns no longer used
alter table task drop column if exists parent_task_id;
alter table task drop column if exists work_status;
alter table task drop column if exists billing_status;
alter table task drop column if exists billing_type;
alter table task drop column if exists billable;
alter table task drop column if exists hourly_rate;
alter table task drop column if exists fixed_fee;
alter table task drop column if exists estimated_minutes;
alter table task drop column if exists estimate_rollup_mode;
alter table task drop column if exists actual_rollup_mode;
alter table task drop column if exists billing_rollup_mode;
alter table task drop column if exists finalized_at;
alter table task drop column if exists finalized_by_user_id;
alter table task drop column if exists sort_order;

alter table task drop column if exists completed_at;

-- due_date was date; sheet uses datetime — widen to timestamptz when present
do $$
begin
    if exists (
        select 1 from information_schema.columns
        where table_name = 'task' and column_name = 'due_date'
          and data_type = 'date'
    ) then
        alter table task alter column due_date type timestamptz using due_date::timestamptz;
    end if;
end $$;
