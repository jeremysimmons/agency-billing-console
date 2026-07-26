-- Hourly rate belongs on invoice only; remove client override.
alter table client drop column if exists default_hourly_rate;
