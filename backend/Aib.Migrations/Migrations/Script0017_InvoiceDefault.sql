-- Default invoice flag: at most one invoice may be default.
-- Only preparing (pending) invoices should be marked default (enforced in app).

alter table invoice add column if not exists is_default boolean not null default false;

create unique index if not exists ux_invoice_one_default
    on invoice (is_default)
    where is_default;
