# Activity-Gated Chat Announcements Design

## Summary

Build a generic Streamer.bot extension called **Activity-Gated Chat Announcements**. It periodically sends configured chat messages only when a target chat has enough real activity since that specific job last posted there.

The extension is not Discord-specific. It supports multiple reusable announcement jobs, and ships with a Discord server announcement as an example configuration people can copy and adapt.

## Goals

- Support any streaming platform that Streamer.bot can integrate with, without hard-coding platform send APIs into the core scheduler.
- Allow multiple announcement jobs, each with its own message, interval, chat threshold, and explicit target list.
- Prevent quiet chats from filling with bot messages by requiring both elapsed time and qualifying chat activity.
- Ignore bot, broadcaster, self, and system-like messages when counting chat activity.
- Allow platform or target-specific message text while keeping a simple default-message path.
- Keep state per job and target so one job does not reset or starve another job.

## Non-Goals

- Do not build a graphical configuration UI in the first version.
- Do not auto-discover every connected platform or automatically post to newly connected chats.
- Do not hard-code every possible Streamer.bot chat-send method into the scheduler.
- Do not generate a final `.sb` import bundle until it can be based on, or tested against, a known-good export from the target Streamer.bot version.

## Architecture

The extension has three action roles.

### Track Chat Activity

Each platform chat trigger calls a tracking action and provides a configured `targetId`, such as `twitch_main`, `youtube_main`, or `kick_main`.

The tracker reads the JSON config, determines whether the incoming chat event qualifies, and increments counters for every enabled job that targets that `targetId`.

The tracker must ignore:

- usernames listed in global ignored users
- usernames listed in the target-specific ignored users
- known self or broadcaster names configured for that target
- messages marked or configured as bot/system messages where Streamer.bot exposes that information

### Run Announcement Scheduler

A Streamer.bot timer calls the scheduler action at a regular cadence, such as once per minute.

The scheduler reads the JSON config and evaluates every enabled job against each explicit target in `targetIds`.

A job may send to a target only when:

- the target exists and is enabled
- the job is enabled
- the target appears in the job's explicit `targetIds`
- the job/target pair has reached `minChats`
- the job/target pair has not sent within `intervalMinutes`
- the resolved message is not blank
- the target has a configured sender action

When all gates pass, the scheduler calls the target's sender action with arguments including `message`, `targetId`, `platform`, and `jobId`.

### Sender Actions

Sender actions are platform-specific Streamer.bot actions configured by the user or shipped as examples. The generic scheduler decides when and what to send; sender actions decide how to post to a platform.

This keeps the core platform-agnostic. Adding a new platform requires:

- a chat trigger wired to the tracker with a unique `targetId`
- a sender action that accepts the scheduler's argument contract
- a target entry in the JSON config

## Configuration

Use one persisted JSON global:

```text
activityGatedAnnouncements.config
```

Example:

```json
{
  "ignoredUsers": ["streamerbot", "nightbot", "streamelements"],
  "targets": {
    "twitch_main": {
      "platform": "twitch",
      "enabled": true,
      "senderAction": "AGA - Send Twitch Message",
      "ignoredUsers": ["mychannelname"]
    },
    "youtube_main": {
      "platform": "youtube",
      "enabled": true,
      "senderAction": "AGA - Send YouTube Message",
      "ignoredUsers": []
    }
  },
  "jobs": [
    {
      "id": "discord",
      "enabled": true,
      "targetIds": ["twitch_main", "youtube_main"],
      "intervalMinutes": 30,
      "minChats": 25,
      "defaultMessage": "Join our Discord: https://discord.gg/example",
      "messagesByTarget": {
        "youtube_main": "Join our Discord: https://discord.gg/example"
      }
    }
  ]
}
```

Configuration rules:

- Jobs must use explicit `targetIds`; no job posts to all targets by default.
- `defaultMessage` is used unless `messagesByTarget[targetId]` is present and non-blank.
- `messagesByTarget` is optional.
- `ignoredUsers` are compared case-insensitively.
- Targets can define their own `ignoredUsers` in addition to the global list.

## Runtime State

Use persisted, namespaced globals:

```text
activityGatedAnnouncements.chatCounts.<jobId>.<targetId>
activityGatedAnnouncements.lastSentUtc.<jobId>.<targetId>
```

State is per job and per target. If the Discord job sends to Twitch, only `chatCounts.discord.twitch_main` resets. Other jobs and other targets keep their own counters.

## Sender Action Argument Contract

The scheduler passes these arguments to sender actions:

```text
message
targetId
platform
jobId
```

Sender actions should return success or failure if the selected Streamer.bot call path supports that. If reliable success reporting is not available, the first implementation should log the call and treat the action invocation itself as success.

## Error Handling

- If the config global is missing or invalid JSON, log a clear error and do nothing.
- If a job references an unknown target, log the job and target ID, then skip that target.
- If a target is disabled, skip it without treating it as an error.
- If a target has no sender action, log it and skip sending.
- If the resolved message is blank, log the job and target ID, then skip sending.
- If sender action invocation fails, log the failure and do not reset the chat count or last-sent timestamp.
- If sending succeeds, reset only that job/target chat count and update only that job/target last-sent timestamp.

## Testing Plan

Use a Discord example job with low test values:

```json
{
  "id": "discord",
  "enabled": true,
  "targetIds": ["twitch_main", "youtube_main"],
  "intervalMinutes": 1,
  "minChats": 2,
  "defaultMessage": "Join our Discord: https://discord.gg/example"
}
```

Test cases:

- Invalid JSON logs an error and sends nothing.
- Unknown target IDs are logged and skipped.
- Disabled jobs and targets are skipped.
- Ignored users do not increment job/target counters.
- Qualifying chat increments each enabled job targeting that target.
- Scheduler does not send before both `intervalMinutes` and `minChats` pass.
- Scheduler sends independently per target.
- Scheduler resets only the successful job/target count.
- A failed or missing sender action does not reset the count.
- A target-specific message overrides `defaultMessage`.
- A job without a target-specific message uses `defaultMessage`.

## First Implementation Deliverables

- Streamer.bot C# code for the tracker action.
- Streamer.bot C# code for the scheduler action.
- Example JSON config with a Discord announcement job.
- Example sender action instructions for at least Twitch and YouTube, using user-configured Streamer.bot actions.
- Manual test checklist for importing or recreating the actions in Streamer.bot.

