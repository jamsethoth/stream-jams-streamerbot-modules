## 1. Module Scaffold

- [x] 1.1 Create `modules/giveaway-module` with `module.json`, `README.md`, `src/actions/`, `src/config/`, `docs/`, and `tests/`.
- [x] 1.2 Add `src/config/default-config.json` with `giveawayModule.config` defaults, including enabled reward matching, configured reward IDs, configured reward names, command metadata, and fixed chat response text.
- [x] 1.3 Add `GWM - Configure Defaults` to write `giveawayModule.config` and initialize `giveawayModule.state` only when missing.
- [x] 1.4 Add manifest command records for `!giveaway enter`, `!giveaway clear`, and `!giveaway draw`, with management commands restricted to moderators.

## 2. Giveaway Actions

- [x] 2.1 Implement C# state helpers for loading, validating, and saving `giveawayModule.state` as persisted JSON with `entries`, `winners`, and `updatedAtUtc`.
- [x] 2.2 Implement `GWM - Handle Command Entry` to normalize Twitch command args into shared entry arguments.
- [x] 2.3 Implement `GWM - Handle Twitch Reward Entry` to match configured reward IDs or names and normalize matching redemption args into shared entry arguments.
- [x] 2.4 Implement `GWM - Enter Giveaway` to add new entrants, reject duplicate entrants, reject previous winners, persist every mutation, and send fixed chat acknowledgements.
- [x] 2.5 Implement `GWM - Clear Giveaway` with broadcaster/moderator authorization, state clearing, persistence, and chat acknowledgement.
- [x] 2.6 Implement `GWM - Draw Giveaway` with broadcaster/moderator authorization, random entrant selection, winner movement, persistence before announcement, empty-list handling, and chat acknowledgement.

## 3. Reward Trigger Setup

- [x] 3.1 Document the Streamer.bot Reward Redemption trigger variables needed by the reward entry action.
- [x] 3.2 Document manual Twitch Reward Redemption trigger setup because this repo has no same-version `.sb` reward-trigger fixture.
- [x] 3.3 Keep generated imports conservative by shipping `GWM - Handle Twitch Reward Entry` without an unverified event trigger record.
- [x] 3.4 Add import-builder tests that decode a generated giveaway import and verify command triggers, deterministic IDs, and the reward entry action is present for manual trigger attachment.

## 4. Documentation And Artifact Tests

- [x] 4.1 Add module artifact tests that assert expected files, manifest actions, command records, config fields, state global names, duplicate-prevention code paths, winner-lockout code paths, and no unsafe `args[...]` access.
- [x] 4.2 Add giveaway docs for command setup, Twitch reward setup, persisted global inspection, import preparation, and manual test checklist.
- [x] 4.3 Update the top-level README module list to include `modules/giveaway-module`.
- [x] 4.4 Update repository-wide build tests to expect the giveaway module in deterministic module discovery and release artifacts.

## 5. Verification

- [x] 5.1 Run `python3 -B tools/run_tests.py` and fix failures.
- [x] 5.2 Build all module artifacts with `python3 -m tools.streamerbot_import.build_all_modules --output dist/modules`.
- [x] 5.3 Inspect or decode the generated `giveaway-module.sb` to verify actions, commands, reward handler presence, and embedded default config.
- [x] 5.4 Confirm the module documentation instructs operators to import into a disposable Streamer.bot profile, compile all `GWM - ...` C# actions, attach the Reward Redemption trigger, and run the manual test checklist before live use.
