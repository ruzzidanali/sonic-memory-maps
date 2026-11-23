SET local check_function_bodies = off;

CREATE ROLE "sonic_api" WITH NOSUPERUSER NOINHERIT NOCREATEROLE NOCREATEDB LOGIN NOREPLICATION NOBYPASSRLS;

CREATE EXTENSION "postgis" SCHEMA "public";

CREATE TABLE "public"."friendships" (
  "id"           uuid                     NOT NULL DEFAULT gen_random_uuid(),
  "user_id"      uuid                     NOT NULL,
  "friend_id"    uuid                     NOT NULL,
  "requested_by" uuid                     NOT NULL,
  "status"       character varying(20)    NOT NULL DEFAULT 'pending'::character varying,
  "created_at"   timestamp with time zone NOT NULL DEFAULT now(),
  "updated_at"   timestamp with time zone NOT NULL DEFAULT now(),
  CONSTRAINT "ck_friendships_distinct_users" CHECK ((user_id < friend_id)),
  CONSTRAINT "ck_friendships_requester" CHECK (((requested_by = user_id) OR (requested_by = friend_id))),
  CONSTRAINT "ck_friendships_status" CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'accepted'::character varying, 'blocked'::character varying])::text[]))),
  CONSTRAINT "friendships_pkey" PRIMARY KEY (id),
  CONSTRAINT "ux_friendships_pair" UNIQUE (user_id, friend_id)
);

ALTER TABLE "public"."friendships"
  ENABLE ROW LEVEL SECURITY;

CREATE TABLE "public"."memories" (
  "id"                         uuid                         NOT NULL DEFAULT gen_random_uuid(),
  "user_id"                    uuid                         NOT NULL,
  "title"                      character varying(100)       NOT NULL,
  "description"                character varying(500),
  "audio_path"                 text                         NOT NULL,
  "audio_duration_seconds"     integer                      NOT NULL,
  "audio_content_type"         character varying(100)       NOT NULL,
  "audio_size_bytes"           bigint                       NOT NULL,
  "location"                   public.geography(Point,4326) NOT NULL,
  "visibility"                 character varying(20)        NOT NULL DEFAULT 'public'::character varying,
  "notification_radius_meters" integer                      NOT NULL DEFAULT 50,
  "moderation_status"          character varying(20)        NOT NULL DEFAULT 'pending'::character varying,
  "created_at"                 timestamp with time zone     NOT NULL DEFAULT now(),
  "updated_at"                 timestamp with time zone     NOT NULL DEFAULT now(),
  "deleted_at"                 timestamp with time zone,
  CONSTRAINT "ck_memories_audio_duration" CHECK (((audio_duration_seconds >= 1) AND (audio_duration_seconds <= 600))),
  CONSTRAINT "ck_memories_audio_size" CHECK (((audio_size_bytes >= 1) AND (audio_size_bytes <= 52428800))),
  CONSTRAINT "ck_memories_moderation_status"
    CHECK (((moderation_status)::text = ANY ((ARRAY['pending'::character varying, 'approved'::character varying, 'rejected'::character varying])::text[]))),
  CONSTRAINT "ck_memories_notification_radius" CHECK (((notification_radius_meters >= 10) AND (notification_radius_meters <= 250))),
  CONSTRAINT "ck_memories_title" CHECK (((char_length(TRIM(BOTH FROM title)) >= 1) AND (char_length(TRIM(BOTH FROM title)) <= 100))),
  CONSTRAINT "ck_memories_visibility"
    CHECK (((visibility)::text = ANY ((ARRAY['public'::character varying, 'friends_only'::character varying, 'private'::character varying])::text[]))),
  CONSTRAINT "memories_pkey" PRIMARY KEY (id)
);

ALTER TABLE "public"."memories"
  ENABLE ROW LEVEL SECURITY;

CREATE TABLE "public"."memory_likes" (
  "id"         uuid                     NOT NULL DEFAULT gen_random_uuid(),
  "memory_id"  uuid                     NOT NULL,
  "user_id"    uuid                     NOT NULL,
  "created_at" timestamp with time zone NOT NULL DEFAULT now(),
  CONSTRAINT "memory_likes_pkey" PRIMARY KEY (id),
  CONSTRAINT "ux_memory_likes_user_memory" UNIQUE (memory_id, user_id)
);

ALTER TABLE "public"."memory_likes"
  ENABLE ROW LEVEL SECURITY;

CREATE TABLE "public"."memory_reports" (
  "id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
  "memory_id"   uuid                     NOT NULL,
  "reporter_id" uuid                     NOT NULL,
  "reason"      character varying(50)    NOT NULL,
  "description" character varying(500),
  "status"      character varying(20)    NOT NULL DEFAULT 'pending'::character varying,
  "created_at"  timestamp with time zone NOT NULL DEFAULT now(),
  "reviewed_at" timestamp with time zone,
  CONSTRAINT "ck_memory_reports_reason"
    CHECK
    (((reason)::text = ANY ((ARRAY['inappropriate'::character varying, 'spam'::character varying, 'harassment'::character varying, 'copyright'::character varying,
    'other'::character varying])::text[]))),
  CONSTRAINT "ck_memory_reports_status"
    CHECK (((status)::text = ANY ((ARRAY['pending'::character varying, 'reviewed'::character varying, 'dismissed'::character varying])::text[]))),
  CONSTRAINT "memory_reports_pkey" PRIMARY KEY (id),
  CONSTRAINT "ux_memory_reports_reporter_memory" UNIQUE (memory_id, reporter_id)
);

ALTER TABLE "public"."memory_reports"
  ENABLE ROW LEVEL SECURITY;

CREATE TABLE "public"."profiles" (
  "id"           uuid                     NOT NULL,
  "username"     character varying(30)    NOT NULL,
  "display_name" character varying(100),
  "avatar_url"   text,
  "bio"          character varying(300),
  "created_at"   timestamp with time zone NOT NULL DEFAULT now(),
  "updated_at"   timestamp with time zone NOT NULL DEFAULT now(),
  CONSTRAINT "ck_profiles_username_length" CHECK (((char_length((username)::text) >= 3) AND (char_length((username)::text) <= 30))),
  CONSTRAINT "profiles_pkey" PRIMARY KEY (id)
);

ALTER TABLE "public"."profiles"
  ENABLE ROW LEVEL SECURITY;

CREATE OR REPLACE FUNCTION public.ensure_profile_for_user (
  p_user_id uuid
)
  RETURNS void
  LANGUAGE plpgsql
  SECURITY DEFINER
  SET search_path TO ''
  AS $function$
DECLARE
    v_username text;
    v_attempt integer := 0;
BEGIN
    -- Avoid duplicate profiles.
    IF EXISTS (
        SELECT 1
        FROM public.profiles
        WHERE id = p_user_id
    ) THEN
        RETURN;
    END IF;

    -- Generate a deterministic default username.
    v_username :=
        'user_' ||
        substr(replace(p_user_id::text, '-', ''), 1, 25);

    LOOP
        BEGIN
            INSERT INTO public.profiles (
                id,
                username
            )
            VALUES (
                p_user_id,
                v_username
            )
            ON CONFLICT (id) DO NOTHING;

            RETURN;

        EXCEPTION WHEN unique_violation THEN

            -- Another operation may have created this profile.
            IF EXISTS (
                SELECT 1
                FROM public.profiles
                WHERE id = p_user_id
            ) THEN
                RETURN;
            END IF;

            -- Handle a username collision.
            v_attempt := v_attempt + 1;

            v_username :=
                'user_' ||
                substr(
                    md5(p_user_id::text || ':' || v_attempt::text),
                    1,
                    25
                );
        END;
    END LOOP;
END;
$function$;

CREATE OR REPLACE FUNCTION public.handle_new_user()
  RETURNS TRIGGER
  LANGUAGE plpgsql
  SECURITY DEFINER
  SET search_path TO ''
  AS $function$
BEGIN
    PERFORM public.ensure_profile_for_user(NEW.id);

    RETURN NEW;
END;
$function$;

CREATE OR REPLACE FUNCTION public.rls_auto_enable()
  RETURNS event_trigger
  LANGUAGE plpgsql
  SECURITY DEFINER
  SET search_path TO 'pg_catalog'
  AS $function$
DECLARE
  cmd record;
  tbl name;
BEGIN
  FOR cmd IN
    SELECT *
    FROM pg_event_trigger_ddl_commands()
    WHERE command_tag IN ('CREATE TABLE', 'CREATE TABLE AS', 'SELECT INTO')
      AND object_type IN ('table','partitioned table')
  LOOP
     IF cmd.schema_name IS NOT NULL AND cmd.schema_name IN ('public') AND cmd.schema_name NOT IN ('pg_catalog','information_schema') AND cmd.schema_name NOT LIKE 'pg_toast%' AND cmd.schema_name NOT LIKE 'pg_temp%' THEN
      BEGIN
        SELECT c.relname INTO tbl FROM pg_catalog.pg_class c WHERE c.oid = cmd.objid;
        EXECUTE format('alter table if exists %I.%I enable row level security', cmd.schema_name, tbl);
        RAISE LOG 'rls_auto_enable: enabled RLS on %', cmd.object_identity;
      EXCEPTION
        WHEN OTHERS THEN
          RAISE LOG 'rls_auto_enable: failed to enable RLS on %', cmd.object_identity;
      END;
     ELSE
        RAISE LOG 'rls_auto_enable: skip % (either system schema or not in enforced list: %.)', cmd.object_identity, cmd.schema_name;
     END IF;
  END LOOP;
END;
$function$;

ALTER TABLE "public"."memory_likes"
  ADD CONSTRAINT "memory_likes_memory_id_fkey" FOREIGN KEY (memory_id) REFERENCES public.memories(id) ON DELETE CASCADE;

ALTER TABLE "public"."memory_reports"
  ADD CONSTRAINT "memory_reports_memory_id_fkey" FOREIGN KEY (memory_id) REFERENCES public.memories(id) ON DELETE CASCADE;

ALTER TABLE "public"."profiles"
  ADD CONSTRAINT "profiles_id_fkey" FOREIGN KEY (id) REFERENCES auth.users(id) ON DELETE CASCADE;

ALTER TABLE "public"."friendships"
  ADD CONSTRAINT "friendships_friend_id_fkey" FOREIGN KEY (friend_id) REFERENCES public.profiles(id) ON DELETE CASCADE;

ALTER TABLE "public"."friendships"
  ADD CONSTRAINT "friendships_requested_by_fkey" FOREIGN KEY (requested_by) REFERENCES public.profiles(id) ON DELETE CASCADE;

ALTER TABLE "public"."friendships"
  ADD CONSTRAINT "friendships_user_id_fkey" FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON DELETE CASCADE;

ALTER TABLE "public"."memories"
  ADD CONSTRAINT "memories_user_id_fkey" FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON DELETE CASCADE;

ALTER TABLE "public"."memory_likes"
  ADD CONSTRAINT "memory_likes_user_id_fkey" FOREIGN KEY (user_id) REFERENCES public.profiles(id) ON DELETE CASCADE;

ALTER TABLE "public"."memory_reports"
  ADD CONSTRAINT "memory_reports_reporter_id_fkey" FOREIGN KEY (reporter_id) REFERENCES public.profiles(id) ON DELETE CASCADE;

CREATE INDEX ix_friendships_friend_id ON public.friendships USING btree (friend_id, status);

CREATE INDEX ix_memories_created_at ON public.memories USING btree (created_at DESC);

CREATE INDEX ix_memories_location ON public.memories USING gist (location);

CREATE INDEX ix_memories_user_id ON public.memories USING btree (user_id);

CREATE INDEX ix_memories_visibility ON public.memories USING btree (visibility, moderation_status)
  WHERE (deleted_at IS NULL);

CREATE INDEX ix_memory_likes_memory_id ON public.memory_likes USING btree (memory_id);

CREATE INDEX ix_memory_reports_status ON public.memory_reports USING btree (status, created_at);

CREATE UNIQUE INDEX ux_profiles_username_lower ON public.profiles USING btree (lower((username)::text));

CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW
  EXECUTE FUNCTION public.handle_new_user();

CREATE POLICY "friendships_select_participant" ON "public"."friendships"
  FOR SELECT
  TO "sonic_api"
  USING (((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid) OR (friend_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)));

CREATE POLICY "memories_insert_owner" ON "public"."memories"
  FOR INSERT
  TO "sonic_api"
  WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));

CREATE POLICY "memories_select_authorized" ON "public"."memories"
  FOR SELECT
  TO "sonic_api"
  USING
    (((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid) OR ((deleted_at IS NULL) AND ((moderation_status)::text = 'approved'::text) AND
    ((visibility)::text = 'public'::text)) OR
    ((deleted_at IS NULL) AND ((moderation_status)::text = 'approved'::text) AND ((visibility)::text = 'friends_only'::text) AND (EXISTS ( SELECT 1
   FROM public.friendships f
  WHERE
    (((f.status)::text = 'accepted'::text) AND (((f.user_id = memories.user_id) AND (f.friend_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)) OR
    ((f.friend_id = memories.user_id) AND (f.user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid)))))))));

CREATE POLICY "memories_update_owner" ON "public"."memories"
  FOR UPDATE
  TO "sonic_api"
  USING (((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid) AND (deleted_at IS NULL)))
  WITH CHECK ((user_id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));

CREATE POLICY "profiles_select_own" ON "public"."profiles"
  FOR SELECT
  TO "sonic_api"
  USING ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));

CREATE POLICY "profiles_update_own" ON "public"."profiles"
  FOR UPDATE
  TO "sonic_api"
  USING ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid))
  WITH CHECK ((id = (NULLIF(current_setting('app.user_id'::text, true), ''::text))::uuid));

CREATE EVENT TRIGGER "ensure_rls"
  ON ddl_command_end
  WHEN TAG IN ('CREATE TABLE', 'CREATE TABLE AS', 'SELECT INTO')
  EXECUTE FUNCTION "public"."rls_auto_enable"();

COMMENT ON EXTENSION "postgis" IS 'PostGIS geometry and geography spatial types and functions';

REVOKE ALL ON FUNCTION "public"."ensure_profile_for_user"(uuid) FROM PUBLIC;

GRANT EXECUTE ON FUNCTION "public"."ensure_profile_for_user"(uuid) TO "anon", "authenticated";

REVOKE ALL ON FUNCTION "public"."ensure_profile_for_user"(uuid) FROM "postgres";

GRANT EXECUTE ON FUNCTION "public"."ensure_profile_for_user"(uuid) TO "postgres";

GRANT EXECUTE ON FUNCTION "public"."ensure_profile_for_user"(uuid) TO "service_role";

REVOKE ALL ON FUNCTION "public"."handle_new_user"() FROM PUBLIC;

GRANT EXECUTE ON FUNCTION "public"."handle_new_user"() TO "anon", "authenticated";

REVOKE ALL ON FUNCTION "public"."handle_new_user"() FROM "postgres";

GRANT EXECUTE ON FUNCTION "public"."handle_new_user"() TO "postgres";

GRANT EXECUTE ON FUNCTION "public"."handle_new_user"() TO "service_role";

GRANT EXECUTE ON FUNCTION "public"."rls_auto_enable"() TO PUBLIC, "anon", "authenticated";

REVOKE ALL ON FUNCTION "public"."rls_auto_enable"() FROM "postgres";

GRANT EXECUTE ON FUNCTION "public"."rls_auto_enable"() TO "postgres";

GRANT EXECUTE ON FUNCTION "public"."rls_auto_enable"() TO "service_role";

REVOKE ALL ON SCHEMA "public" FROM "sonic_api";

GRANT USAGE ON SCHEMA "public" TO "sonic_api";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."friendships" TO "anon", "authenticated";

REVOKE ALL ON TABLE "public"."friendships" FROM "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."friendships" TO "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."friendships" TO "service_role";

GRANT SELECT ON TABLE "public"."friendships" TO "sonic_api";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memories" TO "anon", "authenticated";

REVOKE ALL ON TABLE "public"."memories" FROM "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memories" TO "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memories" TO "service_role";

GRANT INSERT ("audio_content_type") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("audio_duration_seconds") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("audio_path") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("audio_size_bytes") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("deleted_at"), UPDATE ("deleted_at") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("description"), UPDATE ("description") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("location") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("moderation_status") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("notification_radius_meters"), UPDATE ("notification_radius_meters") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("title"), UPDATE ("title") ON TABLE "public"."memories" TO "sonic_api";

GRANT UPDATE ("updated_at") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("user_id") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT ("visibility"), UPDATE ("visibility") ON TABLE "public"."memories" TO "sonic_api";

GRANT INSERT, SELECT, UPDATE ON TABLE "public"."memories" TO "sonic_api";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memory_likes" TO "anon", "authenticated";

REVOKE ALL ON TABLE "public"."memory_likes" FROM "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memory_likes" TO "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memory_likes" TO "service_role";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memory_reports" TO "anon", "authenticated";

REVOKE ALL ON TABLE "public"."memory_reports" FROM "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memory_reports" TO "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."memory_reports" TO "service_role";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."profiles" TO "anon", "authenticated";

REVOKE ALL ON TABLE "public"."profiles" FROM "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."profiles" TO "postgres";

GRANT DELETE, INSERT, MAINTAIN, REFERENCES, SELECT, TRIGGER, TRUNCATE, UPDATE ON TABLE "public"."profiles" TO "service_role";

GRANT UPDATE ("bio") ON TABLE "public"."profiles" TO "sonic_api";

GRANT UPDATE ("display_name") ON TABLE "public"."profiles" TO "sonic_api";

GRANT UPDATE ("updated_at") ON TABLE "public"."profiles" TO "sonic_api";

GRANT UPDATE ("username") ON TABLE "public"."profiles" TO "sonic_api";

GRANT SELECT, UPDATE ON TABLE "public"."profiles" TO "sonic_api";