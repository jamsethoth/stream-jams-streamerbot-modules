# Manual Test Checklist

Use a disposable Streamer.bot profile before live use.

## Import And Defaults

- Import `configurable-game-counters.sb`.
- Compile all `CGC - ...` C# sub-actions.
- Run `CGC - Configure Defaults`.
- Confirm `gameCounters.config` exists.
- Confirm the current game fallback is active: `gameCounters.currentGame.key` is `uncategorized` when no game has been set.

## Chat Parser

- Attach a Twitch chat message trigger to `CGC - Track Chat Counter Callout`.
- Send a greed callout with `!greed`; confirm `gameCounters.counts.global.greed` increments.
- Confirm the same greed callout increments `gameCounters.counts.byGame.uncategorized.greed`.
- Send a death callout with `!death`; confirm death state changes but greed state does not.
- Send a level up callout with `!levelup`; confirm level-up state changes independently.
- Send ordinary chat; confirm no counter changes.
- Trigger the same counter inside the cooldown window; confirm it does not increment again.

## Current Game

- Run `CGC - Set Current Game` with `gameName=Elden Ring`.
- Confirm `gameCounters.currentGame.key` becomes `elden_ring`.
- Send `!greed`; confirm the per-game count is written under `gameCounters.counts.byGame.elden_ring.greed`.
- Confirm the old `uncategorized` count still exists and was not reset.
- Change the game manually again; confirm game change does not reset any counter.

## Twitch Category Sync

- Attach a Twitch Stream Update trigger with `Game Only` enabled to `CGC - Sync Current Game From Twitch`.
- Change the Twitch category and confirm Twitch category sync updates `gameCounters.currentGame.name`.
- Confirm the key uses `twitch_<gameId>` when Streamer.bot provides a `gameId`.
- Set the game manually and then change the Twitch category during the manual lock; confirm pending game globals update but current game remains manual.
- Clear or expire the manual lock, then change the Twitch category again; confirm sync updates the current game.

## Management Actions

- Run `CGC - Report Counter` for `greed`; confirm chat shows current-game and global totals.
- Run `CGC - Adjust Counter` with `counterId=greed`, `amount=-1`, and `scope=both`; confirm both totals decrease but do not go below zero.
- Run `CGC - Reset Counter` without confirmation; confirm reset confirmation is required and no state changes.
- Run `CGC - Reset Counter` with `counterId=greed`, `scope=game`, and `confirm=true`; confirm only the current game's greed counter resets.
- Confirm the global greed count remains unchanged after a game-scope reset.
