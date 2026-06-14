# Trigger Setup

## Automatic Twitch First Chat

The generated import attaches the Twitch `First Words` trigger to:

```text
FCS - Handle Twitch First Words
```

Inspect the imported action and confirm the trigger is present and enabled. That wrapper reads the chatter login from the trigger arguments, records enabled configured people in stream-entry order for `!soall`, sets `targetId=twitch_main`, sets `shoutoutSource=automatic`, and runs `FCS - Run Shoutout`.

Automatic shoutouts only run for enabled logins listed in `firstChatShoutouts.config` when `automatic.enabled` is `true`. The stream-entry tracking still runs when automatic shoutouts are disabled.

## Stream Start Reset

The generated import attaches a Twitch `Stream Online` trigger to:

```text
FCS - Reset Stream State
```

It also includes Streamer.bot's built-in `Reset First Words` settings sub-action in the same stream-start flow. `Reset First Words` clears Streamer.bot's own first-words tracking so the trigger can fire for a new stream. The module reset updates `firstChatShoutouts.streamState` and keeps `firstChatShoutouts.streamSessionId` synchronized with the active session.

If Stream Online fires again during a short outage, `FCS - Reset Stream State` recovers the existing active state when `streamState.recoveryEnabled` is `true` and the state is still inside `streamState.recoveryWindowMinutes`. If the previous active state is outside that window, the action archives it, purges archived sessions older than the recovery window, trims archives to `streamState.maxArchivedSessions`, and starts a fresh active session.

Suggested sub-action order:

```text
1. Reset First Words
2. Execute C# Code - FCS - Reset Stream State
```

If either imported item is missing, add it manually before going live. If you do not reset both Streamer.bot First Words and the module stream state, automatic shoutouts may not fire exactly once per stream.
