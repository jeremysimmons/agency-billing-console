-- Slim project to name only (plus id, client_id, timestamps).

alter table project drop column if exists code;
alter table project drop column if exists description;
alter table project drop column if exists status;
alter table project drop column if exists billing_type;
alter table project drop column if exists hourly_rate;
alter table project drop column if exists fixed_fee;
alter table project drop column if exists budget_minutes;
alter table project drop column if exists budget_amount;
alter table project drop column if exists start_date;
alter table project drop column if exists end_date;
alter table project drop column if exists active;
