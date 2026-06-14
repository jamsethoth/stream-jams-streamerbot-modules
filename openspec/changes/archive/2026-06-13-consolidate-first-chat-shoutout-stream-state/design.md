## Context

This repository packages Streamer.bot modules as manifest-driven C# action sources under `modules/<module-id>/`. First Chat Shoutouts currently persists operator configuration in `firstChatShoutouts.config`, stores the active session id in `firstChatShoutouts.streamSessionId`, stores entered configured chatters in `firstChatShoutouts.entered.<targetId>.<sessionId>`, and stores handled shoutouts in one bool global per login at `firstChatShoutouts.sent.<targetId>.<sessionId>.<login>`.

The current model is crash-durable because all mutations use persisted globals, but the runtime state is split across many names. `sent` grows with every shouted-out login and old session keys are not cleaned up. A Stream Online event also creates a new session every time, which means a short stream outage can lose the previous entered/sent view even though the data still exists under old keys.

This change keeps `firstChatShoutouts.config` as the operator-owned configuration global and moves runtime stream state into one persisted JSON global named `firstChatShoutouts.streamState`.

## Goals / Non-Goals

**Goals:**

- Store active First Chat Shoutouts runtime state in one persisted JSON global.
- Preserve first-entry order for `!soall`.
- Preserve automatic duplicate suppression for logins already shouted out in the active stream session.
- Track both entered and sent state per target/login.
- Store `enteredTimeUtc` and `sentTimeUtc` as ISO 8601 UTC strings.
- Preserve crash and close durability by writing every mutation with `CPH.SetGlobalVar(..., true)`.
- Add a bounded recovery mechanism for short stream outages so a second Stream Online event does not necessarily erase active state.
- Keep existing manual, automatic, shoutout-all, auto-toggle, and auto-add behavior unless state storage requires a visible documentation update.
- Keep stale legacy globals untouched instead of trying to enumerate or delete them from Streamer.bot.

**Non-Goals:**

- Broad command redesign beyond state recovery support.
- Multi-platform target implementation beyond preserving target IDs in the state shape.
- Full cleanup of old `firstChatShoutouts.entered.*` or `firstChatShoutouts.sent.*` globals.
- File-based state export.
- Perfect detection of every intentional same-day new stream versus accidental stream outage.

## Decisions

1. **Use one runtime state global named `firstChatShoutouts.streamState`.**
   - Rationale: One JSON document avoids high-cardinality per-login globals and gives operators one place to inspect active stream state.
   - Alternative considered: One state global per target, such as `firstChatShoutouts.streamState.<targetId>`. That reduces write size but keeps multi-global cleanup and recovery complexity.

2. **Keep `firstChatShoutouts.streamSessionId` only as a compatibility/session pointer.**
   - Rationale: Existing docs and tests already expose the session id, and other action code may be easier to update incrementally if `CurrentSessionId()` still exists. The authoritative session metadata will live in `firstChatShoutouts.streamState.activeSessionId`.
   - Alternative considered: Remove `firstChatShoutouts.streamSessionId` entirely. That is cleaner eventually, but it increases migration risk and breaks more existing operator expectations at once.

3. **Represent target state as an `enteredOrder` array plus a `logins` object keyed by normalized login.**
   - Proposed active state shape:

```json
{
  "schemaVersion": 1,
  "activeSessionId": "638854000000000000",
  "activeStartedAtUtc": "2026-06-13T18:00:00.0000000Z",
  "lastUpdatedAtUtc": "2026-06-13T18:31:05.0000000Z",
  "lastRecoveredAtUtc": null,
  "targets": {
    "twitch_main": {
      "enteredOrder": ["thenoble1", "anothercreator"],
      "logins": {
        "thenoble1": {
          "login": "thenoble1",
          "entered": true,
          "enteredTimeUtc": "2026-06-13T18:30:00.0000000Z",
          "sent": true,
          "sentTimeUtc": "2026-06-13T18:31:05.0000000Z",
          "sentSource": "automatic"
        },
        "anothercreator": {
          "login": "anothercreator",
          "entered": true,
          "enteredTimeUtc": "2026-06-13T18:45:00.0000000Z",
          "sent": false,
          "sentTimeUtc": null,
          "sentSource": ""
        }
      }
    }
  },
  "archivedSessions": []
}
```

   - Rationale: `enteredOrder` preserves `!soall` behavior without needing to sort object properties. The login-keyed object gives constant-time sent/entered checks while remaining readable in Streamer.bot's global viewer.
   - Alternative considered: An array of records per target. That is simpler JSON but every lookup requires a scan and duplicate prevention is easier to get wrong.

4. **Manual shoutouts create or update a login record even if the viewer has not entered.**
   - Rationale: Existing behavior marks manual shoutouts as handled for the automatic path. If a moderator manually shouts out a configured login before that login's First Words event, the later automatic path must still skip it.
   - Alternative considered: Only track sent state for automatic shoutouts. That would regress the current manual-suppresses-automatic behavior.

5. **First Words always records entered state for enabled configured people, even when automatic shoutouts are disabled.**
   - Rationale: Existing `!soall` behavior depends on entry tracking continuing while `automatic.enabled` is false.
   - Alternative considered: Skip tracking when automatic is disabled. That would make `!soall` less useful and contradict current docs.

6. **Use config-driven stream recovery settings.**
   - Add a config section to `firstChatShoutouts.config`:

```json
{
  "streamState": {
    "recoveryEnabled": true,
    "recoveryWindowMinutes": 30,
    "maxArchivedSessions": 3
  }
}
```

   - Rationale: A fixed window is simple for operators and testable. Thirty minutes is long enough for common Twitch disconnects without likely spanning a planned separate stream.
   - Alternative considered: Always recover if a previous active session exists. That would be dangerous for intentionally separate streams.

7. **On Stream Online, recover first when the active state is still fresh; otherwise archive and start fresh.**
   - `FCS - Reset Stream State` should load config and state.
   - If recovery is enabled and the existing active state's `lastUpdatedAtUtc` or `activeStartedAtUtc` is within `recoveryWindowMinutes`, the action should keep the active session, set `lastRecoveredAtUtc`, persist the state, refresh `firstChatShoutouts.streamSessionId`, and log that state was recovered.
   - If no recoverable active session exists, the action should append the previous active session snapshot to `archivedSessions`, prune archives outside the recovery window, enforce `maxArchivedSessions`, then create a new blank active session and persist it.
   - Rationale: This handles the common "stream died and came back" path automatically while still starting fresh after the configured window.
   - Alternative considered: Always reset and rely on manual recovery. That protects intentional restarts but fails the expected automatic recovery path.

8. **Prune archived sessions on every stream-state write.**
   - Every state save should prune `archivedSessions` before `CPH.SetGlobalVar(StateGlobal, ...)`.
   - Pruning should first remove archived sessions whose archive timestamp or last update timestamp is older than `recoveryWindowMinutes`, then keep only the newest `maxArchivedSessions`.
   - If recovery is disabled or `recoveryWindowMinutes <= 0`, archives should not remain recoverable; implementation may either store no archives or immediately prune them on the next state write.
   - Rationale: The state global stays bounded by both time and count, and manual recovery cannot resurrect state that the operator's recovery window says is stale.
   - Alternative considered: Prune only when a fresh session starts. That bounds archives by count but leaves expired archives in the global until the next reset.

9. **Add a manual recovery action, with an optional disabled moderator command, to restore the latest archived session.**
   - Action: `FCS - Recover Stream State`.
   - Suggested aliases: `!sorecover` and `!shoutoutrecover`.
   - The command should be disabled by default and restricted to moderators, matching the other moderator command imports.
   - The action should restore the newest archived session that is within the recovery window, update `lastRecoveredAtUtc`, set `firstChatShoutouts.streamSessionId`, persist, and send a concise chat confirmation.
   - Rationale: If automatic recovery is disabled, missed, or the operator intentionally forced a reset too early, there is still a bounded recovery path.
   - Alternative considered: No manual recovery. That leaves the operator with only direct global JSON editing after a bad reset.

10. **Fail closed on malformed state JSON.**
   - Rationale: Overwriting a malformed persisted global with blank state would destroy the only recovery source. Actions should log the parse error and stop the state mutation except for an explicit operator reset action.
   - Alternative considered: Automatically recreate blank state whenever parse fails. That is convenient but unsafe for crash recovery.

11. **Do not delete or enumerate legacy globals.**
   - Rationale: Streamer.bot C# does not provide a safe, obvious module-scoped global enumeration path in the current code. Attempting cleanup by guessed names would miss data or risk deleting unrelated operator state. The new implementation should stop writing legacy `entered.*` and `sent.*` keys; docs can tell operators old keys are stale.
   - Alternative considered: Add best-effort cleanup of known current-session keys. That still cannot remove high-cardinality historical `sent` keys and creates false confidence.

## Risks / Trade-offs

- **Short intentional second stream inside the recovery window** -> The module may preserve old state. Mitigation: document `streamState.recoveryWindowMinutes`, allow setting it to `0` or disabling `recoveryEnabled`, and provide a force-new reset path through the reset action if implemented.
- **Concurrent First Words events** -> Two actions can load the same JSON and race. Mitigation: keep each mutation small, write immediately after each change, and rely on Streamer.bot action queue serialization. Do not batch multiple independent state changes in memory.
- **Large target/login state** -> One JSON global grows during very active streams. Mitigation: only configured people are recorded as entered, only shouted-out logins are recorded as sent, and archived sessions are bounded by recovery age plus `maxArchivedSessions`.
- **Malformed JSON** -> State mutation actions stop rather than overwriting. Mitigation: log the global name and parse error, document manual repair/reset.
- **Operator confusion from stale old globals** -> Old `entered.*` and `sent.*` keys remain visible. Mitigation: update README/manual checklist to identify `firstChatShoutouts.streamState` as authoritative and old keys as legacy.
- **Recovery window chosen poorly** -> Too short misses outages; too long can merge separate streams. Mitigation: keep the value configurable and document the trade-off.

## Migration Plan

1. Add `firstChatShoutouts.streamState` initialization to `FCS - Configure Defaults` without overwriting existing non-empty state.
2. Add `streamState` recovery defaults to `src/config/default-config.json`.
3. Replace old entered/sent global reads and writes in action code with helpers that load, validate, mutate, prune archives, and persist `firstChatShoutouts.streamState`.
4. Keep `firstChatShoutouts.streamSessionId` in sync with `streamState.activeSessionId`.
5. Stop writing `firstChatShoutouts.entered.*` and `firstChatShoutouts.sent.*`.
6. Update tests to assert the new global names and to assert old global prefixes are no longer present in C# action sources except in migration/docs text if intentionally documented.
7. Update docs and manual test checklist with recovery scenarios.

Rollback is source-level: revert the action changes to the previous release and keep the old globals untouched. State written only to `firstChatShoutouts.streamState` will not be visible to the old code, so rollback during a live stream should be treated as a fresh stream-state reset.

## Open Questions

- Default recovery window is proposed as 30 minutes. Implementation can change this before coding if a different operator default is preferred.
- The proposal includes a manual recovery action and disabled command because it makes recovery safer. If command surface area should stay unchanged, implement the action without a command and document manual action execution instead.
