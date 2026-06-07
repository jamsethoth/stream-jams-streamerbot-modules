# Auto Shoutout Add Command Design

## Summary

Add a moderator-only command to the First Chat Shoutouts module that appends or updates a Twitch login in `firstChatShoutouts.config.people`. The command lets moderators add a user to automatic first-chat shoutouts from chat, optionally storing a per-person announcement template at the same time.

## Command

The command syntax is:

```text
!soautoadd <login> [custom announcement template...]
```

Aliases are:

```text
!soautoadd
!addsoauto
!shoutoutautoadd
```

The imported command is disabled by default and restricted to Moderators, matching the existing safety posture for `!so`, `!soall`, and `!soauto`.

## Behavior

The new `FCS - Handle Auto Shoutout Add` action loads `firstChatShoutouts.config`, checks `autoAdd.enabled` and `autoAdd.moderatorOnly`, normalizes the login, ensures `people` is an array, and upserts an entry with:

```json
{
  "login": "somecreator",
  "enabled": true
}
```

If text remains after the login in the command input, the action stores that text as `announcementTemplate`. If no custom template is provided, an existing per-person `announcementTemplate` is preserved, and new people fall back to `defaultAnnouncementTemplate`.

The custom template is stored literally. Existing shoutout resolution already supports `{login}`, `{displayName}`, `{lastGame}`, `{channelTitle}`, `{targetId}`, and `{platform}`, so moderators can include `{lastGame}` in the custom message to reference the user's last streamed Twitch game.

## Error Handling

If config is missing or invalid, the action logs the same style of error as existing command handlers and stops. If the caller is not a moderator or broadcaster while `autoAdd.moderatorOnly` is enabled, the action logs a warning and stops. If no valid Twitch login is present, the action sends usage text.

## Testing

Artifact tests cover the new action file, manifest action, manifest command aliases, default config section, docs, and source fragments that prove the handler upserts `people`, stores `announcementTemplate`, and preserves `{lastGame}` as a supported template token.
