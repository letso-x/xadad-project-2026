-- Mzukulu QMS — users table for authentication.
-- Run this once against the Supabase Postgres database (SQL editor or psql).
-- The app also creates this table automatically on first connect if it is missing.

create table if not exists app_users (
    id                   text primary key,
    username             text not null unique,
    full_name            text not null,
    email                text,
    password_hash        text not null,
    password_salt        text not null,
    hash_iterations      integer not null default 100000,
    role                 integer not null default 2,   -- 1 = Admin, 2 = SiteUser
    active               boolean not null default true,
    must_change_password boolean not null default false,
    created_at           timestamptz not null default now(),
    last_login_at        timestamptz
);

-- Case-insensitive username lookups.
create unique index if not exists app_users_username_lower_idx
    on app_users (lower(username));
