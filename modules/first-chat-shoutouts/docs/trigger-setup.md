# Trigger Setup

## Automatic Twitch First Chat

The generated import attaches the Twitch `First Words` trigger to:

```text
FCS - Handle Twitch First Words
```

Inspect the imported action and confirm the trigger is present and enabled. That wrapper reads the chatter login from the trigger arguments, sets `targetId=twitch_main`, sets `shoutoutSource=automatic`, and runs `FCS - Run Shoutout`.

Automatic shoutouts only run for enabled logins listed in `firstChatShoutouts.config`.

## Stream Start Reset

Attach a Twitch `Stream Online` trigger to:

```text
FCS - Reset Stream State
```

Also add Streamer.bot's built-in `Reset First Words` settings sub-action to the same stream-start flow. The module reset creates a new `firstChatShoutouts.streamSessionId`; `Reset First Words` clears Streamer.bot's own first-words tracking so the trigger can fire for a new stream. Stream Online trigger generation is not included in the current import because this repository does not yet have a trusted Streamer.bot 1.0.4 export shape for that trigger.

Suggested sub-action order:

```text
1. Reset First Words
2. Execute C# Code - FCS - Reset Stream State
```

If you do not reset both Streamer.bot First Words and `firstChatShoutouts.streamSessionId`, automatic shoutouts may not fire exactly once per stream.
