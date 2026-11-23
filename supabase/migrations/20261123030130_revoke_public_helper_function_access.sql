REVOKE EXECUTE ON FUNCTION public.ensure_profile_for_user(uuid)
FROM anon, authenticated;

REVOKE EXECUTE ON FUNCTION public.handle_new_user()
FROM anon, authenticated;
