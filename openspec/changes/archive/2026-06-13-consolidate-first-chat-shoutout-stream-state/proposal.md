## Why

First Chat Shoutouts currently stores stream-entry state in one per-session JSON array and shoutout completion state as one persisted global per target/session/login. That works for correctness, but it creates high-cardinality stale globals and makes stream recovery harder to inspect because the active session state is split across many global variable names.

This change consolidates active stream state into one persisted JSON global so stream start reset, crash recovery, manual shoutout-all, and automatic duplicate suppression all operate from a single durable state document.

## What Changes

- Add a single persisted state global for First Chat Shoutouts runtime state, `firstChatShoutouts.streamState`.
- Store active stream session metadata, target-specific entered order, and per-login entered/sent flags in that JSON object.
- Replace `firstChatShoutouts.entered.<targetId>.<streamSessionId>` with target state inside `firstChatShoutouts.streamState`.
- Replace `firstChatShoutouts.sent.<targetId>.<streamSessionId>.<login>` with per-login sent state inside `firstChatShoutouts.streamState`.
- Add sent timestamps and entered timestamps using ISO 8601 UTC strings.
- On stream start reset, archive the previous active session inside the same state document before starting a fresh active session so a short stream outage can be recovered.
- Add a bounded recovery mechanism that can restore the most recent archived active session when the stream dies and comes back quickly.
- Prune archived sessions on each stream-state write by removing archives outside the recovery window, then enforcing the configured maximum archive count.
- Keep `firstChatShoutouts.config` as the operator-editable configuration global.
- Keep existing command and trigger behavior for automatic shoutouts, manual single shoutouts, shoutout-all, auto-toggle, and auto-add.
- **BREAKING**: New runtime state will no longer be written to the old `firstChatShoutouts.entered.*` and `firstChatShoutouts.sent.*` global families. Existing stale globals are ignored after migration unless the implementation chooses to import the current-session values once.

## Capabilities

### New Capabilities

- `first-chat-shoutouts-stream-state`: Defines the consolidated persisted stream-state contract, stream reset behavior, automatic/manual shoutout state transitions, and recovery behavior for First Chat Shoutouts.

### Modified Capabilities

None.

## Impact

- Updates `modules/first-chat-shoutouts/src/actions/configure-defaults.cs` to initialize the new state global without overwriting existing state.
- Updates `modules/first-chat-shoutouts/src/actions/reset-stream-state.cs` to rotate active session state, archive the previous active session, and support recovery windows.
- Updates `modules/first-chat-shoutouts/src/actions/handle-twitch-first-words.cs` to record entered chatters in the consolidated state object.
- Updates `modules/first-chat-shoutouts/src/actions/handle-manual-twitch-shoutout-all.cs` to read entered order from the consolidated state object.
- Updates `modules/first-chat-shoutouts/src/actions/run-shoutout.cs` to check and mark sent state inside the consolidated state object.
- Updates `modules/first-chat-shoutouts/README.md` and module docs to document the new global and recovery behavior.
- Updates `modules/first-chat-shoutouts/tests/test_artifacts.py` and any generated import expectations that assert the old global names.
- Preserves Streamer.bot crash durability by writing every state mutation with `CPH.SetGlobalVar(..., true)`.
