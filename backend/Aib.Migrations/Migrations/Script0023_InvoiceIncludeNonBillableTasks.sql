-- How non-billable tasks appear on an invoice: none | detail | summary.
alter table invoice add column if not exists include_non_billable_tasks text not null default 'none';

alter table invoice drop constraint if exists ck_invoice_include_non_billable_tasks;
alter table invoice add constraint ck_invoice_include_non_billable_tasks
    check (include_non_billable_tasks in ('none', 'detail', 'summary'));
