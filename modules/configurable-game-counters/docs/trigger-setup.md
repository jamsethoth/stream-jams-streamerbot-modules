# Trigger Setup

The generated import creates the C# actions and default config. Add runtime triggers in Streamer.bot after importing the module.

## Chat Counter Parser

Attach platform chat message triggers to:

```text
CGC - Track Chat Counter Callout
```

Recommended triggers:

- Twitch chat message
- YouTube chat message, if you want the same aliases to work there

This action intentionally parses normal chat messages instead of relying on native Streamer.bot command records. That keeps aliases such as `!greed`, `!death`, and `!levelup` fully configurable in `gameCounters.config`.

The action ignores messages that do not match a configured alias. Use `chatParser.globalCooldownSeconds`, `chatParser.perUserCooldownSeconds`, and per-counter cooldown overrides to keep chat responses controlled.

## Twitch Category Sync

Attach a Twitch `Stream Update` trigger to:

```text
CGC - Sync Current Game From Twitch
```

Use Streamer.bot's `Game Only` option on the trigger. The sync action checks `gameUpdate`, reads `gameId` and `gameName`, and updates these canonical globals:

```text
gameCounters.currentGame.key
gameCounters.currentGame.name
gameCounters.currentGame.source
gameCounters.currentGame.updatedUtc
gameCounters.currentGame.twitchGameId
```

Default mode is `autoWithManualLock`. A manual game set writes `gameCounters.currentGame.manualLockUntilUtc`, and Twitch updates that arrive during the manual lock are stored as pending game globals instead of replacing the current game.

Game changes do not reset counters. They only change which `gameCounters.counts.byGame.<gameKey>.<counterId>` variables receive future increments.

## Manual Current Game

Run `CGC - Set Current Game` with a `gameName` argument, or wire it to a moderator-only command/hotkey that provides the desired game name as `rawInput`.

Manual game names are sanitized into stable keys. For example:

```text
Elden Ring Nightreign -> elden_ring_nightreign
```

Use `currentGame.twitchSync.categoryMappings` in config when a Twitch category ID or name should map to a custom canonical key.

## Counter Management

`CGC - Report Counter`, `CGC - Adjust Counter`, and `CGC - Reset Counter` are provided as action building blocks. If you expose them to chat, make report everyone-accessible and keep adjust/reset moderator-only.

`CGC - Reset Counter` refuses to run unless a `confirm` argument is true, `yes`, or `confirm`.
