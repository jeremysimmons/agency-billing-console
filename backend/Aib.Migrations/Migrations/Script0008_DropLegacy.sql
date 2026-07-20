-- Drop legacy auth, ClickUp staging, and billing tables (unused by slim app).

drop table if exists
    import_record,
    sync_cursor,
    external_time_entry,
    external_task_mapping,
    external_status_mapping,
    external_container_mapping,
    external_work_item,
    external_container,
    external_identity,
    external_connection,
    import_run,
    time_entry_source,
    time_entry,
    client_access,
    user_session,
    social_identity,
    local_credential,
    user_role,
    magic_link_token,
    auth_event,
    app_user,
    contractor,
    role,
    identity_provider
cascade;
