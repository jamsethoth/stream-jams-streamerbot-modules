# Trigger Setup

## Automatic Twitch First Chat

The generated import attaches the Twitch `First Words` trigger to:

```text
FCS - Handle Twitch First Words
```

Inspect the imported action and confirm the trigger is present and enabled. That wrapper reads the chatter login from the trigger arguments, sets `targetId=twitch_main`, sets `shoutoutSource=automatic`, and runs `FCS - Run Shoutout`.

Automatic shoutouts only run for enabled logins listed in `firstChatShoutouts.config`.

## Stream Start Reset

The generated import attaches a Twitch `Stream Online` trigger to:

```text
FCS - Reset Stream State
```

It also includes Streamer.bot's built-in `Reset First Words` settings sub-action in the same stream-start flow. The module reset creates a new `firstChatShoutouts.streamSessionId`; `Reset First Words` clears Streamer.bot's own first-words tracking so the trigger can fire for a new stream.

Suggested sub-action order:

```text
1. Reset First Words
2. Execute C# Code - FCS - Reset Stream State
```

If either imported item is missing, add it manually before going live. If you do not reset both Streamer.bot First Words and `firstChatShoutouts.streamSessionId`, automatic shoutouts may not fire exactly once per stream.
