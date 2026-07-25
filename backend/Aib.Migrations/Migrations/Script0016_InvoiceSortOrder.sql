-- Custom display order for invoices (shared by invoices page + task dropdown).

alter table invoice add column if not exists sort_order int;

with ordered as (
    select id,
           row_number() over (
               order by case lower(status)
                   when 'preparing' then 0
                   when 'sent' then 1
                   when 'partially-paid' then 2
                   else 3
               end, name
           ) - 1 as rn
    from invoice
)
update invoice i
set sort_order = o.rn
from ordered o
where i.id = o.id
  and (i.sort_order is null or i.sort_order <> o.rn);

alter table invoice alter column sort_order set default 0;
update invoice set sort_order = 0 where sort_order is null;
alter table invoice alter column sort_order set not null;
