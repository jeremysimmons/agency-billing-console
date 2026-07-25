alter table client
    add column if not exists bill_field_available boolean not null default false,
    add column if not exists bill_custom_field_id text,
    add column if not exists bill_yes_option_id text,
    add column if not exists bill_no_option_id text,
    add column if not exists bill_field_checked_at timestamptz;
