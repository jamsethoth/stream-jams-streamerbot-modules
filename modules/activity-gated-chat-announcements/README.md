# Activity-Gated Chat Announcements

## What It Does

Activity-Gated Chat Announcements is a generic Streamer.bot module pattern for posting recurring chat announcements only after a target chat has had enough real activity. It supports multiple announcement jobs and multiple explicit chat targets without baking Twitch, YouTube, or any other platform send method into the scheduler.

Use the generated `.sb` import file from the release archive or CI artifact. The C# source files in this repository are build inputs for that import, not user installation steps.

## Installation

1. Download the module artifact or release archive.
2. Extract `activity-gated-chat-announcements/activity-gated-chat-announcements.sb`.
3. In Streamer.bot, open `Import`.
4. Load the `.sb` file, or open `activity-gated-chat-announcements.import.txt` and paste that import text into the import field.
5. Confirm the import contains the `Activity-Gated Announcements` actions, then import them.
6. Open the imported actions and compile the C# sub-actions.
7. Run `AGA - Configure Defaults` if Streamer.bot does not auto-run it after import.

The import file creates the module actions and default configuration globals. After import, use Streamer.bot's global variable viewer to adjust timing, chat-count thresholds, target enablement, and shared values such as the Discord invite URL.

## C# References

The generated C# sub-actions include a `System.Core.dll` reference because the module uses framework types and LINQ helpers. The default import reference is:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll
```

That path is the standard 64-bit Windows .NET Framework location and is not tied to a user profile or this repository. If Streamer.bot reports a missing metadata/reference file while compiling the imported C# actions, edit each imported C# sub-action's references to point at the `System.Core.dll` path on that machine. Common alternatives include:

```text
C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Core.dll
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll
```

## Source Layout

- `module.json`: module metadata used by the import builder.
- `src/actions/track-chat-activity.cs`: tracker action source included in the generated import.
- `src/actions/run-announcement-scheduler.cs`: scheduler action source included in the generated import.
- `src/actions/configure-defaults.cs`: default config initializer included in the generated import.
- `src/actions/send-twitch-message.cs`: Twitch sender action source included in the generated import.
- `src/actions/send-youtube-message.cs`: YouTube sender action source included in the generated import.
- `src/actions/track-twitch-main.cs`: Twitch wrapper action source included in the generated import.
- `src/actions/track-youtube-main.cs`: YouTube wrapper action source included in the generated import.
- `src/config/default-config.json`: starter config for a Discord server announcement across Twitch and YouTube.
- `docs/import-prep.md`: maintainer workflow for producing a local `.sb` import during development.
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
