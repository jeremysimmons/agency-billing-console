-- Invoice hourly rate for billing totals.
alter table invoice add column if not exists rate numeric(12,2);

-- Per-task discount percentage (0–100), default 0.
alter table task add column if not exists discount_percent numeric(5,2) not null default 0;

alter table task drop constraint if exists ck_task_discount_percent;
alter table task add constraint ck_task_discount_percent
    check (discount_percent >= 0 and discount_percent <= 100);
