# Activity-Gated Chat Announcements

## What It Does

Activity-Gated Chat Announcements is a generic Streamer.bot module pattern for posting recurring chat announcements only after a target chat has had enough real activity. It supports multiple announcement jobs and multiple explicit chat targets without baking Twitch, YouTube, or any other platform send method into the scheduler.

No prebuilt .sb import bundle is committed. Generate one with `tools/streamerbot_import/build_module_import.py` from a known-good C# action export from your Streamer.bot version so the generated actions reuse the action/sub-action schema your install already accepts.

## Installation

The full module bundle can be prepared once you have a known-good exported C# action stub from your Streamer.bot install:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/activity-gated-chat-announcements \
  exports/csharp-stub.sb \
  build/activity-gated-chat-announcements.sb
```

See `docs/import-prep.md` for the full workflow and safety checks.

## Files

- `module.json`: module metadata used by the import builder.
- `src/actions/track-chat-activity.cs`: paste into the generic tracker action's Execute C# Code sub-action.
- `src/actions/run-announcement-scheduler.cs`: paste into the timer-driven scheduler action's Execute C# Code sub-action.
- `src/actions/configure-defaults.cs`: initializes the default config and shared editable globals.
- `src/actions/send-twitch-message.cs`: generated Twitch sender action source.
- `src/actions/send-youtube-message.cs`: generated YouTube sender action source.
- `src/actions/track-twitch-main.cs`: wrapper action source for Twitch chat activity.
- `src/actions/track-youtube-main.cs`: wrapper action source for YouTube chat activity.
- `src/config/default-config.json`: starter config for a Discord server announcement across Twitch and YouTube.
- `docs/import-prep.md`: workflow for turning a known-good local scheduler export into an experimental `.sb` import.
- `docs/sender-actions.md`: example sender actions for Twitch and YouTube.
- `docs/manual-test-checklist.md`: manual verification checklist before using this live.

## Configuration

The generated import includes `AGA - Configure Defaults`, which initializes these persisted globals if they do not already exist:

```text
activityGatedAnnouncements.config
activityGatedAnnouncements.discordInviteUrl
activityGatedAnnouncements.twitchChannelName
activityGatedAnnouncements.youtubeChannelName
```

Edit `activityGatedAnnouncements.discordInviteUrl`, `activityGatedAnnouncements.twitchChannelName`, and `activityGatedAnnouncements.youtubeChannelName` in Streamer.bot's global variable viewer. The JSON config references those shared globals with `{discordInviteUrl}`, `{twitchChannelName}`, and `{youtubeChannelName}`.

Most users should only edit `intervalMinutes`, `minChats`, `enabled`, and `targetIds` in `activityGatedAnnouncements.config`.

## Generated Actions

Recommended group:

```text
Activity-Gated Announcements
```

The generated import creates these actions:

```text
AGA - Configure Defaults
AGA - Track Chat Activity
AGA - Track Twitch Main
AGA - Track YouTube Main
AGA - Run Announcement Scheduler
AGA - Send Twitch Message
AGA - Send YouTube Message
```

The two core actions are platform agnostic:

- `AGA - Track Chat Activity` reads `targetId`, ignores bot/self/system-like chat events, and increments `activityGatedAnnouncements.chatCounts.<jobId>.<targetId>`.
- `AGA - Run Announcement Scheduler` runs from a timer, checks each job and target against `minChats` and `intervalMinutes`, calls the configured sender action, then updates only that job/target pair's count and `activityGatedAnnouncements.lastSentUtc.<jobId>.<targetId>`.

The generated wrapper actions set `targetId` and then run the tracker and scheduler. That means the scheduler can evaluate immediately after qualifying chat activity instead of relying only on a timer.

## Tracker Wiring

The tracker needs a `targetId` argument because the same C# code can serve more than one platform or chat. The generated wrapper actions provide that argument:

```text
AGA - Track Twitch Main -> targetId=twitch_main
AGA - Track YouTube Main -> targetId=youtube_main
```

Attach the platform chat-message trigger for each target to its matching wrapper action.

This keeps each trigger's target explicit and avoids accidentally counting one platform as another.

## Scheduler Wiring

The generated wrapper actions run `AGA - Run Announcement Scheduler` immediately after tracking chat activity, so the activity gates are evaluated on new qualifying chat.

You can also add a timer that runs `AGA - Run Announcement Scheduler` once per minute. A one-minute cadence is enough because each job has its own `intervalMinutes` gate.

## Sender Contract

The scheduler sets these arguments before running the configured sender action:

```text
message
targetId
platform
jobId
```

Sender actions can use `%message%` directly in a platform chat-send sub-action. They may also inspect `%targetId%`, `%platform%`, and `%jobId%` for logging or target-specific behavior.

## Runtime State

The module writes these persisted globals:

```text
activityGatedAnnouncements.chatCounts.<jobId>.<targetId>
activityGatedAnnouncements.lastSentUtc.<jobId>.<targetId>
```

State is intentionally per job and per target. If the Discord announcement posts to Twitch, only the Discord/Twitch count resets.
