## Why

Stream Jams needs a packaged Streamer.bot giveaway module that can collect Twitch viewers from chat commands or channel point redemptions without manual list handling. The first version should keep scope tight: one active giveaway, durable entrant state, duplicate prevention, mod-only management, and a draw flow that prevents winners from re-entering until the giveaway is cleared.

## What Changes

- Add a new Streamer.bot module for a single active Twitch giveaway.
- Allow viewers to enter with `!giveaway enter` or a configurable Twitch channel point reward trigger.
- Store giveaway state in one persisted global variable so entries and winners survive Streamer.bot restarts or crashes.
- De-duplicate entrants by Twitch user ID while using the viewer's displayed screen name in chat responses.
- Send fixed Twitch chat acknowledgements for successful entry, duplicate entry, and previous-winner lockout.
- Add mod/broadcaster-only management commands:
  - `!giveaway clear` clears entrants and winner history for the current giveaway.
  - `!giveaway draw` chooses one winner, moves them from entrants to winners, and announces the result.
- Treat multiple concurrent/named giveaways, configurable response text, rerolls, eligibility filters, file export, and cross-platform entry as future enhancements.

## Capabilities

### New Capabilities
- `giveaway-module`: Defines a single active Twitch giveaway with command/reward entry, persisted state, duplicate prevention, mod-only clear/draw management, and winner lockout until clear.

### Modified Capabilities

None.

## Impact

- Adds a new module directory under `modules/giveaway-module`.
- Adds Streamer.bot C# action sources, default configuration, module manifest, generated README content, and module artifact tests.
- Updates repository-wide module build expectations so release builds include the giveaway module.
- Uses Streamer.bot persisted globals via `CPH.GetGlobalVar(..., true)` and `CPH.SetGlobalVar(..., true)` for durable JSON state.
