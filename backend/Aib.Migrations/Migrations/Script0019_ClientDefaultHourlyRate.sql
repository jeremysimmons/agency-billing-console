-- Client default hourly rate (used as fallback / editable on client detail).
alter table client add column if not exists default_hourly_rate numeric(12,2);
