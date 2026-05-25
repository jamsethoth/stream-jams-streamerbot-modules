# Sender Actions

The generated import includes platform-specific sender actions. The scheduler stays generic by calling the sender action named by each target in `activityGatedAnnouncements.config`.

The scheduler sets these arguments before calling the sender action:

```text
message
targetId
platform
jobId
```

## Twitch

Action:

```text
Activity-Gated Announcements / AGA - Send Twitch Message
```

Generated C# behavior:

```text
CPH.SendMessage(%message%, useBot: true, fallback: true)
```

Suggested options:

- Keep this action focused on sending one message.
- Do not add a trigger to this action.
- Let the scheduler call it inline with `CPH.RunAction`.

## YouTube

Action:

```text
Activity-Gated Announcements / AGA - Send YouTube Message
```

Generated C# behavior:

```text
CPH.SendYouTubeMessageToLatestMonitored(%message%, useBot: true, fallback: true)
```

Suggested options:

- Do not add a trigger to this action.
- Use the same `%message%` argument contract as Twitch.
- If your Streamer.bot version requires a monitored broadcast, verify the YouTube account and broadcast monitor before testing the scheduler.

## Adding Another Platform

Add a target entry to the config, then provide:

1. A wrapper tracker action that sets the target's `targetId`.
2. A sender action whose name matches that target's `senderAction`.
3. A platform chat-send sub-action that uses `%message%`.

The core scheduler does not need to change when adding a target.
