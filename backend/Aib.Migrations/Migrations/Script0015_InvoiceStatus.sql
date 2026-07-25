-- Invoice statuses: preparing, sent, partially-paid, fully-paid

alter table invoice drop constraint if exists invoice_status_check;

update invoice set status = 'preparing' where lower(trim(status)) in ('open', 'preparing');
update invoice set status = 'fully-paid' where lower(trim(status)) in ('closed', 'fully-paid', 'fully paid');
update invoice set status = 'sent' where lower(trim(status)) = 'sent';
update invoice set status = 'partially-paid'
  where lower(trim(status)) in ('partially-paid', 'partially paid');
update invoice set status = 'preparing'
  where status is null
     or trim(status) = ''
     or lower(trim(status)) not in ('preparing', 'sent', 'partially-paid', 'fully-paid');

alter table invoice alter column status set default 'preparing';

alter table invoice add constraint invoice_status_check
    check (status in ('preparing', 'sent', 'partially-paid', 'fully-paid'));
