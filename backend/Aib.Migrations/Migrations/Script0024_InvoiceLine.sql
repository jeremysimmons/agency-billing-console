-- Manual invoice lines (ad-hoc charges not backed by a ClickUp task).

create table if not exists invoice_line (
    id                uuid primary key,
    invoice_id        uuid not null references invoice (id) on delete cascade,
    client_id         uuid not null references client (id),
    project_id        uuid references project (id) on delete set null,
    title             text not null,
    hours             numeric(12,2) not null default 0
                        check (hours >= 0),
    flat_fee          numeric(12,2)
                        check (flat_fee is null or flat_fee >= 0),
    discount_percent  numeric(5,2) not null default 0
                        check (discount_percent >= 0 and discount_percent <= 100),
    sort_order        integer not null default 0,
    created_at        timestamptz not null,
    updated_at        timestamptz not null
);

create index if not exists ix_invoice_line_invoice
    on invoice_line (invoice_id, sort_order);
