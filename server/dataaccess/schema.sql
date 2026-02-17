BEGIN;

-- Drop in correct order
DROP TABLE IF EXISTS refresh_tokens CASCADE;
DROP TABLE IF EXISTS messages CASCADE;
DROP TABLE IF EXISTS user_roles CASCADE;
DROP TABLE IF EXISTS roles CASCADE;
DROP TABLE IF EXISTS rooms CASCADE;
DROP TABLE IF EXISTS app_users CASCADE;
DROP TYPE IF EXISTS message_type;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TYPE message_type AS ENUM ('public', 'dm');

-- Users (with upgraded fields)
CREATE TABLE app_users (
                           id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                           username text NOT NULL UNIQUE,
                           email text UNIQUE,
                           password_hash text NOT NULL,
                           created_at timestamptz NOT NULL DEFAULT now(),
                           updated_at timestamptz NOT NULL DEFAULT now(),
                           last_login_at timestamptz NULL,
                           is_active boolean NOT NULL DEFAULT true
);

-- Roles
CREATE TABLE roles (
                       id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                       name text NOT NULL UNIQUE
);

CREATE TABLE user_roles (
                            user_id uuid REFERENCES app_users(id) ON DELETE CASCADE,
                            role_id uuid REFERENCES roles(id) ON DELETE CASCADE,
                            PRIMARY KEY (user_id, role_id)
);

-- Rooms
CREATE TABLE rooms (
                       id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                       name text NOT NULL UNIQUE,
                       created_at timestamptz NOT NULL DEFAULT now(),
                       is_archived boolean NOT NULL DEFAULT false
);

-- Messages
CREATE TABLE messages (
                          id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                          room_id uuid NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
                          sender_user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE RESTRICT,
                          type message_type NOT NULL,
                          content text NOT NULL,
                          recipient_user_id uuid NULL REFERENCES app_users(id),
                          sent_at timestamptz NOT NULL DEFAULT now(),

                          CONSTRAINT messages_content_not_blank CHECK (length(btrim(content)) > 0),
                          CONSTRAINT dm_requires_recipient CHECK (
                              (type = 'dm' AND recipient_user_id IS NOT NULL) OR
                              (type = 'public' AND recipient_user_id IS NULL)
                              )
);

-- Refresh Tokens (if you include them)
CREATE TABLE refresh_tokens (
                                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                                user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                                token_hash text NOT NULL,
                                created_at timestamptz NOT NULL DEFAULT now(),
                                expires_at timestamptz NOT NULL,
                                revoked_at timestamptz NULL,
                                replaced_by_token_id uuid NULL REFERENCES refresh_tokens(id)
);

COMMIT;
