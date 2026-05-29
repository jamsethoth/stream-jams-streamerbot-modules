# Configurable Game Counters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Streamer.bot module that lets chat increment configured counter types, tracking independent global and per-current-game totals.

**Architecture:** Create a new `configurable-game-counters` module using the repository's existing module manifest, C# action source, config, docs, and colocated artifact tests pattern. The canonical current game is stored in Streamer.bot globals; Twitch category updates can sync those globals through a guarded action, but counter state is never reset by category changes.

**Tech Stack:** Streamer.bot C# inline actions, persisted Streamer.bot globals, JSON config through Newtonsoft.Json, Python `unittest` artifact/import tests, existing deterministic `.sb` import builder.

---

### Task 1: Red Tests For The New Module Contract

**Files:**
- Create: `modules/configurable-game-counters/tests/test_artifacts.py`
- Modify: `tests/tools/test_build_all_modules.py`

- [ ] **Step 1: Add artifact tests for required files, manifest, config, docs, and source contracts**

```python
class ConfigurableGameCountersArtifactsTest(unittest.TestCase):
    def test_expected_extension_files_exist(self):
        ...

    def test_default_config_defines_counter_and_game_contracts(self):
        ...

    def test_module_manifest_describes_generated_actions(self):
        ...

    def test_action_sources_contain_required_state_contracts(self):
        ...

    def test_docs_describe_setup_sync_state_and_testing(self):
        ...
```

- [ ] **Step 2: Update build-all expectation**

```python
self.assertEqual(
    [module.module_id for module in result.modules],
    [
        "activity-gated-chat-announcements",
        "configurable-game-counters",
        "first-chat-shoutouts",
    ],
)
```

- [ ] **Step 3: Run tests and verify they fail because the module is missing**

Run: `python3 -B tools/run_tests.py`
Expected: FAIL with missing `modules/configurable-game-counters` files.

### Task 2: Add Module Manifest, Config, Docs, And Actions

**Files:**
- Create: `modules/configurable-game-counters/module.json`
- Create: `modules/configurable-game-counters/README.md`
- Create: `modules/configurable-game-counters/src/config/default-config.json`
- Create: `modules/configurable-game-counters/src/actions/*.cs`
- Create: `modules/configurable-game-counters/docs/*.md`

- [ ] **Step 1: Add manifest with action list**

Actions:
- `CGC - Configure Defaults`
- `CGC - Track Chat Counter Callout`
- `CGC - Set Current Game`
- `CGC - Sync Current Game From Twitch`
- `CGC - Report Counter`
- `CGC - Adjust Counter`
- `CGC - Reset Counter`

- [ ] **Step 2: Add default config**

Config defines `greed`, `death`, and `level_up` counters, command aliases, cooldowns, templates, manual/Twitch current-game sync behavior, and reset confirmation requirements.

- [ ] **Step 3: Add C# actions**

Use persisted globals:
- `gameCounters.config`
- `gameCounters.currentGame.key`
- `gameCounters.currentGame.name`
- `gameCounters.currentGame.source`
- `gameCounters.currentGame.updatedUtc`
- `gameCounters.currentGame.twitchGameId`
- `gameCounters.counts.global.<counterId>`
- `gameCounters.counts.byGame.<gameKey>.<counterId>`

- [ ] **Step 4: Add docs**

Document installation, trigger setup, current-game management, reset/adjust safety, generated actions, and manual test cases.

- [ ] **Step 5: Run tests and verify green**

Run: `python3 -B tools/run_tests.py`
Expected: PASS.

### Task 3: Verify Import Generation And Finish

**Files:**
- Generated only under temporary build directories.

- [ ] **Step 1: Run full test suite**

Run: `python3 -B tools/run_tests.py`
Expected: PASS.

- [ ] **Step 2: Build all module artifacts**

Run: `python3 -m tools.streamerbot_import.build_all_modules --output /tmp/cgc-modules`
Expected: output includes `configurable-game-counters/configurable-game-counters.sb` and `manifest.json`.

- [ ] **Step 3: Review git diff**

Run: `git diff --stat` and `git diff --check`
Expected: only intended module/test/doc files changed, no whitespace errors.

- [ ] **Step 4: Commit and push**

Run:
```bash
git add docs/superpowers/plans/2026-05-29-configurable-game-counters.md tests/tools/test_build_all_modules.py modules/configurable-game-counters
git commit -m "Add configurable game counters module"
git push -u origin codex/configurable-game-counters
```
