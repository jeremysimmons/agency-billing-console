-- Named invoices for task invoice_label dropdown.

create table if not exists invoice (
    id          uuid primary key,
    name        text not null,
    status      text not null default 'preparing'
                    check (status in ('preparing', 'sent', 'partially-paid', 'fully-paid')),
    created_at  timestamptz not null,
    updated_at  timestamptz not null
);

create unique index if not exists ux_invoice_name_lower
    on invoice (lower(trim(name)));

alter table invoice add column if not exists status text;
update invoice set status = 'preparing' where status is null or trim(status) = '' or lower(trim(status)) = 'open';
update invoice set status = 'fully-paid' where lower(trim(status)) = 'closed';
alter table invoice alter column status set default 'preparing';
alter table invoice alter column status set not null;
do $$
begin
    alter table invoice drop constraint if exists invoice_status_check;
    alter table invoice add constraint invoice_status_check
        check (status in ('preparing', 'sent', 'partially-paid', 'fully-paid'));
end $$;

-- Seed from existing task labels.
insert into invoice (id, name, status, created_at, updated_at)
select gen_random_uuid(), trim(invoice_label), 'preparing', now(), now()
from (
    select distinct trim(invoice_label) as invoice_label
    from task
    where invoice_label is not null and trim(invoice_label) <> ''
) labels
where not exists (
    select 1 from invoice i where lower(i.name) = lower(labels.invoice_label)
);
