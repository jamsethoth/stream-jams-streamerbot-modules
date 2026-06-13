# Giveaway Module

Single active Twitch giveaway for Streamer.bot. Viewers can enter with `!giveaway enter` or by redeeming a configured Twitch channel point reward. Moderators and the broadcaster can clear the giveaway or draw winners from chat.

## What It Does

- Stores all giveaway state in the persisted global `giveawayModule.state`.
- Stores configuration in `giveawayModule.config`.
- De-duplicates entries by Twitch user ID.
- Uses display names in chat acknowledgements.
- Keeps winners in `giveawayModule.state.winners` and prevents them from entering again until `!giveaway clear`.
- Moves a drawn winner out of `entries` and into `winners`.

State shape:

```json
{
  "schemaVersion": 1,
  "giveawayId": "default",
  "entries": [],
  "winners": [],
  "updatedAtUtc": "2026-06-13T00:00:00.0000000Z"
}
```

## Installation

Build or download `giveaway-module.sb`, then import it into a disposable Streamer.bot profile first.

After import:

1. Compile all `GWM - ...` C# actions.
2. Run `GWM - Configure Defaults` if it did not auto-run.
3. Inspect `giveawayModule.config` and `giveawayModule.state`.
4. Review imported commands and enable them when ready.
5. Attach the Twitch Reward Redemption trigger if you want channel point entries.

## Configuration

Edit `giveawayModule.config` in Streamer.bot Globals.

```json
{
  "rewardEntry": {
    "enabled": true,
    "rewardIds": [],
    "rewardNames": ["Giveaway Entry"],
    "matchAnyWhenUnconfigured": true
  },
  "permissions": {
    "manage": "moderator"
  }
}
```

Reward matching checks `rewardIds` first, then `rewardNames`. If both lists are empty and `matchAnyWhenUnconfigured` is `true`, any Reward Redemption trigger attached to `GWM - Handle Twitch Reward Entry` can enter the viewer.

`permissions.manage` supports:

```text
moderator
broadcaster
everyone
```

Keep the imported management commands restricted to moderators even though the C# actions also check permissions.

## Generated Actions

```text
GWM - Configure Defaults
GWM - Handle Command Entry
GWM - Handle Twitch Reward Entry
GWM - Enter Giveaway
GWM - Clear Giveaway
GWM - Draw Giveaway
```

Generated commands are disabled by default:

```text
!giveaway enter
!giveaway clear
!giveaway draw
```

See `docs/command-setup.md`, `docs/trigger-setup.md`, and `docs/manual-test-checklist.md` before live use.
