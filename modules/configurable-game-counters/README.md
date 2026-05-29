# Configurable Game Counters

## What It Does

Configurable Game Counters lets chat call out configured stream events such as greed, deaths, and level ups. Each callout increments two persisted Streamer.bot counters: one global total for that counter type and one total for the current game.

Counter types are created in `gameCounters.config`. Add a new counter ID, aliases, label, permission, cooldown, and response template, then attach chat message triggers to `CGC - Track Chat Counter Callout`. The action ignores chat messages that do not match configured aliases.

The module stores the current game in Streamer.bot globals. Twitch category updates can sync those globals through `CGC - Sync Current Game From Twitch`, but Twitch sync never resets counter state. Resets and corrections require explicit streamer or moderator actions.

## Installation

1. Download the module artifact or release archive.
2. Extract `configurable-game-counters/configurable-game-counters.sb`.
3. In Streamer.bot, open `Import`.
4. Load the `.sb` file, or open `configurable-game-counters.import.txt` and paste that import text into the import field.
5. Confirm the import contains the `Configurable Game Counters` actions, then import them.
6. Open the imported actions and compile the C# sub-actions.
7. Run `CGC - Configure Defaults` if Streamer.bot does not auto-run it after import.
8. Attach chat message triggers and the Twitch Stream Update trigger using `docs/trigger-setup.md`.
9. Test in a disposable Streamer.bot profile before using the module live.

## C# References

The generated C# sub-actions include framework references because the module uses regular expressions and other framework types. The default import references are:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll
```

If Streamer.bot reports a missing metadata/reference file while compiling the imported C# actions, edit each imported C# sub-action's references to point at those framework DLL paths on that machine.

## Configuration

The generated import includes `CGC - Configure Defaults`, which initializes:

```text
gameCounters.config
gameCounters.currentGame.key
gameCounters.currentGame.name
gameCounters.currentGame.source
gameCounters.currentGame.updatedUtc
gameCounters.currentGame.twitchGameId
gameCounters.currentGame.manualLockUntilUtc
```

Edit `gameCounters.config` in Streamer.bot's global variable viewer.

Important fields:

```json
{
  "chatParser": {
    "enabled": true,
    "globalCooldownSeconds": 2,
    "perUserCooldownSeconds": 8,
    "sendResponses": true
  },
  "currentGame": {
    "fallbackKey": "uncategorized",
    "fallbackName": "Uncategorized",
    "twitchSync": {
      "enabled": true,
      "mode": "autoWithManualLock",
      "manualLockMinutes": 180,
      "ignoredCategories": [],
      "categoryMappings": {}
    }
  },
  "counters": {
    "greed": {
      "enabled": true,
      "aliases": ["!greed", "!greedy"],
      "responseTemplate": "{user} called greed. {gameName}: {gameCount}, all-time: {globalCount}."
    }
  }
}
```

Add new counter types under `counters`. Counter IDs are sanitized before they are used in variable names, so prefer lowercase letters, numbers, and underscores.

Supported template tokens:

```text
{counterId}
{label}
{gameKey}
{gameName}
{user}
{gameCount}
{globalCount}
```

## Generated Actions

Recommended group:

```text
Configurable Game Counters
```

The generated import creates these actions:

```text
CGC - Configure Defaults
CGC - Track Chat Counter Callout
CGC - Set Current Game
CGC - Sync Current Game From Twitch
CGC - Report Counter
CGC - Adjust Counter
CGC - Reset Counter
```

`CGC - Track Chat Counter Callout` reads chat text, matches configured aliases, checks permissions and cooldowns, then writes `gameCounters.counts.global.<counterId>` and `gameCounters.counts.byGame.<gameKey>.<counterId>`.

`CGC - Set Current Game` manually sets the canonical current game and creates a manual lock so Twitch sync cannot immediately overwrite it.

`CGC - Sync Current Game From Twitch` reads Twitch Stream Update arguments such as `gameId`, `gameName`, and `gameUpdate`, then updates the canonical current-game globals when sync is enabled and no manual lock is active.

`CGC - Report Counter` sends the configured report template for one counter.

`CGC - Adjust Counter` applies a positive or negative correction to the global total, current-game total, or both.

`CGC - Reset Counter` resets a counter scope to zero only when called with `confirm=true`.

## Runtime State

The module writes persisted, namespaced globals:

```text
gameCounters.counts.global.<counterId>
gameCounters.counts.byGame.<gameKey>.<counterId>
gameCounters.lastIncrementUtc.<gameKey>.<counterId>
gameCounters.cooldowns.counter.<counterId>
gameCounters.cooldowns.user.<counterId>.<userName>
gameCounters.pendingGame.key
gameCounters.pendingGame.name
gameCounters.pendingGame.twitchGameId
gameCounters.pendingGame.updatedUtc
```

State is intentionally independent per counter and per game. A greed increment does not touch death or level-up state. A game change does not reset any counter.
