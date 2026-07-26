-- Optional flat fee on task: when set, invoice bills 1 unit at this amount (discounts still apply).
alter table task add column if not exists flat_fee numeric(12,2);

alter table task drop constraint if exists ck_task_flat_fee;
alter table task add constraint ck_task_flat_fee
    check (flat_fee is null or flat_fee >= 0);
