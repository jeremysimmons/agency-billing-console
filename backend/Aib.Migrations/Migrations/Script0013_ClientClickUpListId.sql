alter table client add column if not exists clickup_list_id text;

create unique index if not exists ux_client_clickup_list
    on client (clickup_list_id)
    where clickup_list_id is not null;
