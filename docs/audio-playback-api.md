# Audio Playback API

## Overview

The audio playback endpoint provides an authorized user with a temporary signed URL to play a memory's audio file.

The API does not stream the audio file directly. It returns a temporary Supabase Storage signed URL.

## Endpoint

| Property | Value |
|---|---|
| Method | GET |
| Route | `/api/memories/{id}/audio` |
| Authentication | Required |
| Success status | 200 OK |

### Path Parameters

| Parameter | Type | Description |
|---|---|---|
| `id` | UUID | Memory ID |

## Authentication

All requests must include a valid Supabase access token.

```http
Authorization: Bearer <supabase_access_token>
```

The API validates the JWT issuer, audience, signature algorithm and token lifetime.

Requests without valid authentication receive `401 Unauthorized`.

## Successful Response

**HTTP 200 OK**

```json
{
  "memoryId": "00000000-0000-0000-0000-000000000000",
  "playbackUrl": "https://your-project.supabase.co/storage/v1/object/sign/memory-audio/...",
  "expiresAt": "2026-11-20T08:05:00Z"
}
```

| Field | Type | Description |
|---|---|---|
| `memoryId` | UUID | ID of the requested memory |
| `playbackUrl` | String | Temporary signed URL for audio playback |
| `expiresAt` | DateTimeOffset | Expiration timestamp in UTC |

The signed URL has a lifetime of **5 minutes (300 seconds)**.

The Flutter client should use `playbackUrl` directly with its audio playback implementation. Do not construct the Supabase Storage URL manually.

## Playback Authorization Rules

| Requester | Memory visibility | Conditions | Access |
|---|---|---|---|
| Owner | Any | Memory is not deleted | Allowed |
| Other user | Public | Moderation status is Approved | Allowed |
| Accepted friend | FriendsOnly | Moderation status is Approved | Allowed |
| Other user | Private | Any moderation status | Denied |
| Any user | Any | Memory is deleted | Denied |

Additional rules:

- Owners can play their own memories regardless of moderation status, provided the memory is not deleted.
- Friends-only access requires an accepted friendship.
- Friendship is checked in either direction.
- Deleted memories cannot be played.

Authorization is checked before requesting a signed URL from Supabase Storage.

## Error Responses

| HTTP Status | Meaning |
|---|---|
| `401 Unauthorized` | Missing or invalid authentication |
| `404 Not Found` | Memory does not exist, is deleted, or access is denied |
| `500 Internal Server Error` | Unexpected server or storage failure |

Example `404 Not Found` response:

```json
{
  "title": "Memory not found",
  "detail": "Memory not found or access is denied.",
  "status": 404
}
```

The API intentionally uses a generic not-found response to avoid revealing whether an inaccessible memory exists.

## Flutter Integration Notes

1. Obtain the current Supabase access token.
2. Send a GET request to `/api/memories/{id}/audio`.
3. Include the access token in the Authorization header.
4. Read `playbackUrl` from the response.
5. Pass the URL directly to the audio player.
6. Request a new playback URL when the existing one expires.

The signed URL is temporary and should not be treated as a permanent audio URL.

Avoid logging or unnecessarily sharing signed URLs because possession of a valid URL may allow access until it expires.

## Security Considerations

- The Supabase audio bucket is private.
- Playback authorization is enforced by the backend.
- Signed download URLs expire after five minutes.
- The API validates the returned signed URL before exposing it to the client.
- The Supabase secret key remains on the backend and must never be exposed to the Flutter application.
