## 1. Test Contract Updates

- [ ] 1.1 Read `proposal.md`, `design.md`, and `specs/first-chat-shoutouts-stream-state/spec.md` before editing implementation files; treat `firstChatShoutouts.streamState` as the authoritative runtime state global and keep `firstChatShoutouts.config` as the operator configuration global.
- [ ] 1.2 Update `modules/first-chat-shoutouts/tests/test_artifacts.py` so the default config test expects `streamState.recoveryEnabled`, `streamState.recoveryWindowMinutes`, and `streamState.maxArchivedSessions` in `src/config/default-config.json`.
- [ ] 1.3 Update the manifest test in `modules/first-chat-shoutouts/tests/test_artifacts.py` to expect the new action `FCS - Recover Stream State` and, if implementing the command, a disabled moderator command with aliases `!sorecover` and `!shoutoutrecover`.
- [ ] 1.4 Update source-fragment tests so `run-shoutout.cs`, `handle-twitch-first-words.cs`, `handle-manual-twitch-shoutout-all.cs`, and `reset-stream-state.cs` are expected to reference `firstChatShoutouts.streamState`.
- [ ] 1.5 Add negative source tests asserting production C# action sources no longer write `firstChatShoutouts.entered.` or `firstChatShoutouts.sent.` with `CPH.SetGlobalVar`; allow those strings only in docs or tests that explicitly describe legacy globals.
- [ ] 1.6 Add artifact tests for recovery behavior fragments: `recoveryWindowMinutes`, `maxArchivedSessions`, `lastRecoveredAtUtc`, `archivedSessions`, `activeSessionId`, and archive-pruning code must appear in the relevant action source.

## 2. Configuration And Manifest

- [ ] 2.1 Update `modules/first-chat-shoutouts/src/config/default-config.json` with a `streamState` object containing `recoveryEnabled: true`, `recoveryWindowMinutes: 30`, and `maxArchivedSessions: 3`.
- [ ] 2.2 Add `src/actions/recover-stream-state.cs` to `modules/first-chat-shoutouts/module.json` as action `FCS - Recover Stream State`.
- [ ] 2.3 Add a disabled moderator-only command record for recovery if keeping the design's command surface: name `First Chat Shoutout Recover Stream State`, action `FCS - Recover Stream State`, aliases `!sorecover` and `!shoutoutrecover`, group `First Chat Shoutouts`, permitted group `Moderators`, and Twitch source `1`.
- [ ] 2.4 Confirm `module.json` still keeps `FCS - Reset Stream State` attached to the `twitch-stream-online` trigger and does not alter existing command aliases for `!so`, `!soall`, `!soauto`, or `!soautoadd`.

## 3. Shared State Helper Pattern

- [ ] 3.1 In each C# action that touches runtime stream state, define constants for `ConfigGlobal = "firstChatShoutouts.config"`, `SessionGlobal = "firstChatShoutouts.streamSessionId"`, and `StateGlobal = "firstChatShoutouts.streamState"` as needed.
- [ ] 3.2 Add helper code to load `firstChatShoutouts.streamState` as a `JObject`, validate `schemaVersion`, and return false without overwriting when non-blank JSON is malformed.
- [ ] 3.3 Add helper code to create a blank state object with `schemaVersion`, `activeSessionId`, `activeStartedAtUtc`, `lastUpdatedAtUtc`, `lastRecoveredAtUtc`, `targets`, and `archivedSessions`.
- [ ] 3.4 Add helper code to create or retrieve target objects with `enteredOrder` and `logins` for normalized target IDs.
- [ ] 3.5 Add helper code to create or retrieve login records keyed by normalized login, storing `login`, `entered`, `enteredTimeUtc`, `sent`, `sentTimeUtc`, and `sentSource`.
- [ ] 3.6 Ensure every helper that mutates state updates `lastUpdatedAtUtc` with `DateTime.UtcNow.ToString("o")`, prunes `archivedSessions` by recovery age and `maxArchivedSessions`, and persists with `CPH.SetGlobalVar(StateGlobal, state.ToString(Newtonsoft.Json.Formatting.None), true)` before the action returns.
- [ ] 3.7 Keep `firstChatShoutouts.streamSessionId` synchronized with `streamState.activeSessionId` wherever a new or restored active session is established.

## 4. Configure Defaults

- [ ] 4.1 Update `modules/first-chat-shoutouts/src/actions/configure-defaults.cs` so `FCS - Configure Defaults` initializes `firstChatShoutouts.streamState` only when missing or blank.
- [ ] 4.2 Preserve the existing behavior that initializes `firstChatShoutouts.config` only when missing or blank.
- [ ] 4.3 Preserve or set `firstChatShoutouts.streamSessionId` so it matches the active session id in `firstChatShoutouts.streamState`.
- [ ] 4.4 Do not attempt to enumerate, delete, or migrate historical `firstChatShoutouts.entered.*` or `firstChatShoutouts.sent.*` globals in configure defaults.

## 5. First Words Entry Tracking

- [ ] 5.1 Update `modules/first-chat-shoutouts/src/actions/handle-twitch-first-words.cs` to remove `EnteredPrefix`, `EnteredGlobal`, and `LoadEnteredLog`.
- [ ] 5.2 Replace `TrackEnteredConfiguredChatter` with logic that loads `firstChatShoutouts.streamState`, gets target `twitch_main`, creates or updates the login record, sets `entered: true`, sets `enteredTimeUtc` only the first time, appends the login once to `enteredOrder`, and persists immediately.
- [ ] 5.3 Preserve current eligibility checks: only enabled configured people on enabled target `twitch_main` are recorded in entry order.
- [ ] 5.4 Preserve current invocation args to `FCS - Run Shoutout`: `targetId=twitch_main`, `shoutoutLogin=<login>`, and `shoutoutSource=automatic`.
- [ ] 5.5 Preserve the behavior that entry tracking still happens even when `automatic.enabled` is false.

## 6. Run Shoutout Sent Tracking

- [ ] 6.1 Update `modules/first-chat-shoutouts/src/actions/run-shoutout.cs` to remove `SentPrefix`, `SentGlobal`, and bool global reads/writes for `firstChatShoutouts.sent.*`.
- [ ] 6.2 Replace `AlreadyHandled(targetId, login)` so it reads `sent` from `firstChatShoutouts.streamState.targets.<targetId>.logins.<login>`.
- [ ] 6.3 Replace `MarkHandled(targetId, login)` so it creates or updates the login record, sets `sent: true`, sets `sentTimeUtc` to current UTC, sets `sentSource` to the normalized `shoutoutSource`, updates `lastUpdatedAtUtc`, and persists immediately.
- [ ] 6.4 Preserve the automatic path's skip behavior when `sent` is already true for the active session.
- [ ] 6.5 Preserve current manual behavior: manual and manual-all paths bypass the automatic duplicate skip but still mark `sent: true` after a successful Twitch announcement.
- [ ] 6.6 Preserve the existing rule that a native Twitch shoutout failure does not block the announcement, and only a successful announcement attempt marks `sent`.

## 7. Manual Shoutout All

- [ ] 7.1 Update `modules/first-chat-shoutouts/src/actions/handle-manual-twitch-shoutout-all.cs` to remove `EnteredPrefix`, `EnteredGlobal`, and `LoadEnteredLog`.
- [ ] 7.2 Load `firstChatShoutouts.streamState`, read `targets.twitch_main.enteredOrder`, and iterate that array in order.
- [ ] 7.3 Preserve current config checks for target enabled, `manualAll.enabled`, target inclusion, moderator/broadcaster authorization, and person enabled state.
- [ ] 7.4 Preserve current invocation args to `FCS - Run Shoutout`: `targetId=twitch_main`, `shoutoutLogin=<login>`, and `shoutoutSource=manual_all`.
- [ ] 7.5 If `enteredOrder` is empty or missing, log the existing no-configured-chatters result and leave state unchanged.

## 8. Stream Reset And Recovery

- [ ] 8.1 Update `modules/first-chat-shoutouts/src/actions/reset-stream-state.cs` to load config and `firstChatShoutouts.streamState` instead of blindly writing a new session id.
- [ ] 8.2 Implement recovery config parsing from `config.streamState.recoveryEnabled`, `config.streamState.recoveryWindowMinutes`, and `config.streamState.maxArchivedSessions`, with safe defaults matching `default-config.json`.
- [ ] 8.3 When reset runs and the active state is recoverable, keep `activeSessionId` and `targets`, set `lastRecoveredAtUtc`, update `lastUpdatedAtUtc`, persist state, sync `firstChatShoutouts.streamSessionId`, and log a recovery message.
- [ ] 8.4 When reset runs and the active state is not recoverable, append the previous active session snapshot to `archivedSessions`, prune archives older than the recovery window, trim archives to `maxArchivedSessions`, create a new blank active session with a fresh tick-based id and empty `targets`, persist state, sync `firstChatShoutouts.streamSessionId`, and log a fresh-session message.
- [ ] 8.5 Treat `recoveryWindowMinutes <= 0` or `recoveryEnabled == false` as recovery disabled, so Stream Online starts a fresh session after archiving any previous active state.
- [ ] 8.6 Do not delete legacy globals from reset; legacy cleanup is out of scope for this change.

## 9. Manual Recovery Action

- [ ] 9.1 Create `modules/first-chat-shoutouts/src/actions/recover-stream-state.cs` with a standalone `CPHInline` class following existing action style and using `CPH.TryGetArg` for authorization args.
- [ ] 9.2 Deny recovery unless the caller is broadcaster or moderator using the same `isModerator`, `moderator`, `isBroadcaster`, and `broadcaster` checks as existing moderator actions.
- [ ] 9.3 Load config and state, prune expired archives, find the newest archived session still inside the configured recovery window, restore it to active state, set `lastRecoveredAtUtc`, update `lastUpdatedAtUtc`, persist, sync `firstChatShoutouts.streamSessionId`, and send a concise chat confirmation.
- [ ] 9.4 If no recoverable archive exists, leave state unchanged and send or log a clear no-recoverable-session message.
- [ ] 9.5 If state JSON is malformed, log the parse failure and do not overwrite state.

## 10. Documentation

- [ ] 10.1 Update `modules/first-chat-shoutouts/README.md` so Configuration initializes `firstChatShoutouts.config`, `firstChatShoutouts.streamSessionId`, and `firstChatShoutouts.streamState`.
- [ ] 10.2 Replace the Runtime State section with the new `firstChatShoutouts.streamState` JSON shape, including `targets`, `enteredOrder`, `logins`, `enteredTimeUtc`, `sentTimeUtc`, `sentSource`, `archivedSessions`, and the archive purge rule.
- [ ] 10.3 Document that old `firstChatShoutouts.entered.*` and `firstChatShoutouts.sent.*` globals are legacy stale data after this change and are not deleted automatically.
- [ ] 10.4 Update `modules/first-chat-shoutouts/docs/trigger-setup.md` to describe Stream Online recovery behavior, the recovery window, and how the built-in Reset First Words sub-action interacts with preserved module state.
- [ ] 10.5 Update `modules/first-chat-shoutouts/docs/command-setup.md` to describe the recovery command if implemented, including moderator-only permissions and disabled-by-default import status.
- [ ] 10.6 Update `modules/first-chat-shoutouts/docs/manual-test-checklist.md` with a short outage recovery test, a fresh stream reset outside the recovery window test, a manual recovery test, and a malformed state fail-closed test.

## 11. Build Artifact Integration

- [ ] 11.1 Regenerate or update any generated import fixtures or expected manifests affected by adding `recover-stream-state.cs` and the recovery command.
- [ ] 11.2 Ensure the module builder includes `recover-stream-state.cs` in the generated `.sb` import and that all generated C# sub-actions compile with existing framework references.
- [ ] 11.3 Decode or inspect the generated first-chat-shoutouts import to verify the new action and optional command are present and existing triggers/commands remain attached correctly.

## 12. Verification

- [ ] 12.1 Run `python -B -m unittest discover -s modules/first-chat-shoutouts/tests` from the repository root and fix failures.
- [ ] 12.2 Run `python -B tools/run_tests.py` from the repository root and fix failures.
- [ ] 12.3 Build all module artifacts with `python -m tools.streamerbot_import.build_all_modules --output dist/modules` and fix build failures.
- [ ] 12.4 Inspect the generated `dist/modules/first-chat-shoutouts` artifact to confirm docs mention `firstChatShoutouts.streamState` and the import contains the expected First Chat Shoutouts actions.
- [ ] 12.5 Run `openspec.cmd status --change "consolidate-first-chat-shoutout-stream-state"` and confirm the change is apply-ready.
