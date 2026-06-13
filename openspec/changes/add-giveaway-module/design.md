## Context

This repository packages Streamer.bot modules as manifest-driven C# action sources under `modules/<module-id>/`. The import tooling builds deterministic `.sb` artifacts from module manifests, default config JSON, C# sources, and known-good Streamer.bot export fixtures. Existing modules use namespaced persisted globals for durable state and write every state mutation through `CPH.SetGlobalVar(..., true)`.

The giveaway module needs two viewer entry paths: a Twitch chat command and a Twitch channel point redemption event. Commands are already supported by the import builder. The official Streamer.bot docs confirm the Twitch Reward Redemption trigger and its `rewardId`/`rewardName` variables, but the docs do not expose the decoded `.sb` trigger record shape and this repository does not currently have a same-version reward-redemption trigger fixture. The MVP will therefore package a reward handler action and document manual Reward Redemption trigger setup instead of fabricating an unverified import trigger record.

## Goals / Non-Goals

**Goals:**

- Provide one active Twitch giveaway list.
- Let viewers enter through `!giveaway enter` or a manually attached Twitch Reward Redemption trigger that uses module config to match reward IDs or names.
- Store entrants and winners in one human-readable persisted JSON global.
- De-duplicate by Twitch user ID while addressing viewers by display name in chat.
- Allow moderators and the broadcaster to clear the giveaway and draw winners.
- Move drawn winners from entrants to winners so they cannot re-enter until clear.
- Include module docs and tests consistent with existing repository modules.

**Non-Goals:**

- Multiple named or concurrent giveaways.
- File export.
- Configurable chat response templates.
- Rerolls, weighted entries, eligibility filters, or automatic open/closed states.
- Generated reward-redemption trigger records in `.sb` output until a same-version Streamer.bot fixture is available.
- Cross-platform entry outside Twitch.

## Decisions

1. **Use `modules/giveaway-module` with action prefix `GWM`.**
   - Rationale: The repo uses one directory per packaged module and short prefixes for generated Streamer.bot actions. `GWM` keeps action names recognizable without conflicting with existing prefixes.
   - Alternative considered: Add giveaway behavior to an existing engagement module. That would mix unrelated ownership and make release artifacts harder to inspect.

2. **Store giveaway runtime data in one persisted state global named `giveawayModule.state`.**
   - Rationale: The user asked for an easily accessible persisted global variable, and one JSON object avoids high-cardinality per-user global fanout. Each mutation must write the full JSON state back with `CPH.SetGlobalVar(StateGlobal, json, true)`.
   - Alternative considered: One global per entrant. That would make cleanup and crash reasoning harder and repeats the high-cardinality pattern this repo avoids.

3. **Keep configuration in `giveawayModule.config`.**
   - Rationale: Existing modules embed default config JSON in a configure-defaults action. Config needs to hold reward matching settings and optional operational flags without mixing operator config into the entrant list.
   - Alternative considered: Hard-code reward matching in source. That would make the module less portable and force source edits for different Twitch rewards.

4. **Represent state as arrays of entrant and winner objects.**
   - Proposed shape:

```json
{
  "schemaVersion": 1,
  "giveawayId": "default",
  "entries": [
    {
      "twitchUserId": "123456",
      "displayName": "ViewerName",
      "login": "viewername",
      "enteredAtUtc": "2026-06-13T00:00:00.0000000Z",
      "source": "command"
    }
  ],
  "winners": [
    {
      "twitchUserId": "123456",
      "displayName": "ViewerName",
      "login": "viewername",
      "enteredAtUtc": "2026-06-13T00:00:00.0000000Z",
      "drawnAtUtc": "2026-06-13T00:05:00.0000000Z",
      "source": "command"
    }
  ],
  "updatedAtUtc": "2026-06-13T00:05:00.0000000Z"
}
```

   - Rationale: Arrays are easy to inspect in Streamer.bot's globals UI and simple to serialize with Newtonsoft.Json. The code can build temporary dictionaries by Twitch user ID for lookups.
   - Alternative considered: Store an object keyed by Twitch user ID. That is faster for lookup but less convenient as an operator-facing list.

5. **Use separate generated commands for entry and management.**
   - `Giveaway Enter`: alias `!giveaway enter`, available to viewers.
   - `Giveaway Clear`: alias `!giveaway clear`, permitted group `Moderators`.
   - `Giveaway Draw`: alias `!giveaway draw`, permitted group `Moderators`.
   - Rationale: Separate commands let Streamer.bot permissions block management calls before C# executes. C# actions still verify broadcaster/moderator args as defense in depth.
   - Alternative considered: One `!giveaway` command with C# subcommand parsing. That would make command-level permissions weaker because entry and management would share the same command record.

6. **Route command and redemption entry through one shared entry action.**
   - `GWM - Handle Command Entry` and `GWM - Handle Twitch Reward Entry` normalize trigger args into `entrySource`, `twitchUserId`, `displayName`, and `login`, then run `GWM - Enter Giveaway`.
   - Rationale: Shared entry logic keeps duplicate handling and chat responses identical across entry paths.
   - Alternative considered: Duplicate entry logic in each trigger action. That increases bug risk around duplicate and winner checks.

7. **Match reward redemption by configured reward IDs first, then configured names.**
   - Rationale: Reward IDs are more stable than names when Streamer.bot provides them, but names are useful during manual setup and testing.
   - Alternative considered: Name-only matching. That is easier to configure but fragile if the reward is renamed.

8. **Do not generate an unverified reward-redemption trigger record.**
   - Rationale: The repo's import builder is intentionally fixture-driven. Without an observed same-version trigger record, the generated `.sb` should avoid adding an event trigger that may fail import or attach incorrectly. Operators will attach Streamer.bot's Twitch Reward Redemption trigger to `GWM - Handle Twitch Reward Entry` and configure `giveawayModule.config.rewardEntry`.
   - Alternative considered: Guess a numeric trigger type and fields from docs. That could produce a broken or misleading import artifact.

9. **Draw from current entrants uniformly and persist before announcing.**
   - Rationale: Persisting the moved winner before chat acknowledgement preserves crash durability even if Streamer.bot exits after selection. Repeated draw commands can select additional winners from the remaining entrants until the list is empty.
   - Alternative considered: Keep winners in the entrant list with a flag. Moving winners produces the clearer operator-facing entrant list the user asked for.

## Risks / Trade-offs

- **Unsupported reward trigger shape** -> Ship the reward entry action without a generated event trigger, document manual trigger setup, and add fixture-backed builder support later when a same-version export is available.
- **Missing Twitch user ID in a trigger** -> Refuse entry, log a warning, and avoid adding a display-name-only record that would break duplicate prevention.
- **Concurrent entries during a draw** -> Load state, update one JSON document, and persist immediately for each mutation. Streamer.bot action queue settings should keep these actions serialized by default.
- **Malformed persisted JSON** -> Log the parse error and fail closed without overwriting existing state except through an explicit clear action.
- **Moderator command accidentally exposed** -> Keep command permitted groups restricted and repeat permission checks in C# using broadcaster/moderator action args.
- **Large entrant list becomes awkward in the globals UI** -> Accept for the MVP. Future file export or multiple giveaway management can be added after the single-list behavior is stable.
