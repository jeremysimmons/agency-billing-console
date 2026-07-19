-- Phase 1 foundation schema: organization, identity/auth, and work hierarchy.
-- Enum-like columns are stored as text (PascalCase names matching the C# enums).

create table agency (
    id                 uuid primary key,
    name               text not null,
    billing_email      text,
    billing_address    text,
    currency           text not null default 'USD',
    payment_terms_days integer not null default 30,
    active             boolean not null default true,
    created_at         timestamptz not null,
    updated_at         timestamptz not null
);

create table contractor (
    id                  uuid primary key,
    name                text not null,
    email               text not null,
    default_hourly_rate numeric(12,2),
    active              boolean not null default true,
    created_at          timestamptz not null,
    updated_at          timestamptz not null
);
create unique index ux_contractor_email on contractor (lower(email));

create table app_user (
    id                     uuid primary key,
    agency_id              uuid references agency (id),
    contractor_id          uuid references contractor (id),
    username               text not null,
    normalized_username    text not null,
    email                  text not null,
    normalized_email       text not null,
    display_name           text not null,
    status                 text not null,
    email_verified_at      timestamptz,
    password_login_enabled boolean not null default true,
    magic_link_enabled     boolean not null default true,
    social_login_enabled   boolean not null default true,
    last_login_at          timestamptz,
    created_at             timestamptz not null,
    updated_at             timestamptz not null
);
create unique index ux_app_user_normalized_username on app_user (normalized_username);
create unique index ux_app_user_normalized_email on app_user (normalized_email);

create table role (
    id   serial primary key,
    name text not null unique
);

create table user_role (
    user_id uuid not null references app_user (id) on delete cascade,
    role_id integer not null references role (id) on delete cascade,
    primary key (user_id, role_id)
);

create table client (
    id          uuid primary key,
    agency_id   uuid not null references agency (id),
    name        text not null,
    code        text,
    description text,
    status      text not null default 'Active',
    active      boolean not null default true,
    created_at  timestamptz not null,
    updated_at  timestamptz not null
);
create index ix_client_agency on client (agency_id);

create table client_access (
    user_id      uuid not null references app_user (id) on delete cascade,
    client_id    uuid not null references client (id) on delete cascade,
    access_level text not null default 'View',
    primary key (user_id, client_id)
);

create table local_credential (
    id                   uuid primary key,
    user_id              uuid not null unique references app_user (id) on delete cascade,
    password_hash        text not null,
    password_changed_at  timestamptz not null,
    must_change_password boolean not null default false,
    failed_attempt_count integer not null default 0,
    locked_until         timestamptz,
    last_failed_at       timestamptz,
    created_at           timestamptz not null,
    updated_at           timestamptz not null
);

create table magic_link_token (
    id                  uuid primary key,
    user_id             uuid not null references app_user (id) on delete cascade,
    token_hash          text not null,
    purpose             text not null default 'Login',
    requested_at        timestamptz not null,
    expires_at          timestamptz not null,
    consumed_at         timestamptz,
    revoked_at          timestamptz,
    request_ip          text,
    request_user_agent  text,
    created_at          timestamptz not null
);
create unique index ux_magic_link_token_hash on magic_link_token (token_hash);
create index ix_magic_link_user on magic_link_token (user_id);

create table identity_provider (
    id               uuid primary key,
    provider_type    text not null,
    name             text not null,
    issuer           text not null,
    client_id        text not null,
    secret_reference text,
    hosted_domain    text,
    enabled          boolean not null default true,
    created_at       timestamptz not null,
    updated_at       timestamptz not null
);
create unique index ux_identity_provider_type on identity_provider (provider_type);

create table social_identity (
    id                        uuid primary key,
    user_id                   uuid not null references app_user (id) on delete cascade,
    identity_provider_id      uuid not null references identity_provider (id),
    provider_subject          text not null,
    provider_email            text not null,
    normalized_provider_email text not null,
    provider_email_verified   boolean not null default false,
    hosted_domain             text,
    linked_at                 timestamptz not null,
    last_login_at             timestamptz,
    created_at                timestamptz not null,
    updated_at                timestamptz not null
);
create unique index ux_social_identity_provider_subject on social_identity (identity_provider_id, provider_subject);
create index ix_social_identity_user on social_identity (user_id);

create table user_session (
    id                    uuid primary key,
    user_id               uuid not null references app_user (id) on delete cascade,
    session_token_hash    text not null,
    authentication_method text not null,
    identity_provider_id  uuid references identity_provider (id),
    created_at            timestamptz not null,
    expires_at            timestamptz not null,
    last_seen_at          timestamptz,
    revoked_at            timestamptz,
    ip_address            text,
    user_agent            text
);
create unique index ux_user_session_hash on user_session (session_token_hash);
create index ix_user_session_user on user_session (user_id);

create table auth_event (
    id                    uuid primary key,
    user_id               uuid references app_user (id) on delete set null,
    event_type            text not null,
    authentication_method text,
    success               boolean not null,
    detail                text,
    ip_address            text,
    user_agent            text,
    created_at            timestamptz not null
);
create index ix_auth_event_user on auth_event (user_id);
create index ix_auth_event_created on auth_event (created_at);

create table project (
    id             uuid primary key,
    client_id      uuid not null references client (id) on delete cascade,
    name           text not null,
    code           text,
    description    text,
    status         text not null default 'Active',
    billing_type   text not null default 'Hourly',
    hourly_rate    numeric(12,2),
    fixed_fee      numeric(12,2),
    budget_minutes integer,
    budget_amount  numeric(14,2),
    start_date     date,
    end_date       date,
    active         boolean not null default true,
    created_at     timestamptz not null,
    updated_at     timestamptz not null
);
create index ix_project_client on project (client_id);

create table task (
    id                   uuid primary key,
    client_id            uuid not null references client (id) on delete cascade,
    project_id           uuid references project (id),
    parent_task_id       uuid references task (id),
    title                text not null,
    description          text,
    work_status          text not null default 'Pending',
    billing_status       text not null default 'NotReady',
    billing_type         text not null default 'Hourly',
    billable             boolean not null default true,
    hourly_rate          numeric(12,2),
    fixed_fee            numeric(12,2),
    estimated_minutes    integer,
    estimate_rollup_mode text not null default 'Direct',
    actual_rollup_mode   text not null default 'DirectAndChildren',
    billing_rollup_mode  text not null default 'Task',
    due_date             date,
    completed_at         timestamptz,
    finalized_at         timestamptz,
    finalized_by_user_id uuid references app_user (id),
    sort_order           integer not null default 0,
    created_at           timestamptz not null,
    updated_at           timestamptz not null
);
create index ix_task_client on task (client_id);
create index ix_task_project on task (project_id);
create index ix_task_parent on task (parent_task_id);
create index ix_task_work_status on task (work_status);
create index ix_task_billing_status on task (billing_status);
