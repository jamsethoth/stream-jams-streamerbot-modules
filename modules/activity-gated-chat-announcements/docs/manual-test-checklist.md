# Manual Test Checklist

Use a disposable Streamer.bot profile first, or duplicate the actions into a temporary group before enabling them in a live setup.

For faster testing, temporarily set the Discord job to:

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

## Cases

- Invalid JSON: break `activityGatedAnnouncements.config`, run the tracker and scheduler, and confirm both log a clear error and send nothing.
- Unknown target: add a fake target ID to a job and confirm the scheduler logs it and skips only that target.
- Disabled job: set a job's `enabled` to `false`, send qualifying chat, and confirm the scheduler does not send it.
- Disabled target: set a target's `enabled` to `false` and confirm jobs skip that target without an error.
- Ignored user: send chat as a user listed in global or target `ignoredUsers` and confirm the chat count does not increase.
- Bot/self/broadcaster guard: send or simulate events with `isBot`, `isSelf`, or `isBroadcaster` set and confirm the chat count does not increase.
- Qualifying chat: send two normal messages to one target and confirm `activityGatedAnnouncements.chatCounts.discord.<targetId>` increments.
- Interval gate: meet `minChats`, run the scheduler before one minute passes, and confirm it does not send yet.
- Target-specific message: set `messagesByTarget.youtube_main`, meet the YouTube gates, and confirm the YouTube sender receives the override.
- Default message: remove the target-specific override, meet the gates, and confirm the sender receives `defaultMessage`.
- Independent targets: meet the Twitch gates only and confirm Twitch sends while YouTube keeps its count.
- Successful sender reset: after a sender succeeds, confirm only that job/target count resets to `0` and only that job/target last-sent global updates.
- Failed sender: rename or disable a sender action, run the scheduler, and confirm the count does not reset.

After testing, restore production values for `intervalMinutes`, `minChats`, and message text.
