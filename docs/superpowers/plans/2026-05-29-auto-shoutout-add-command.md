# Auto Shoutout Add Command Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a moderator-only chat command that upserts a Twitch login into automatic shoutout config and optionally saves a custom shoutout template.

**Architecture:** Follow the module's current command-wrapper pattern: a new `FCS - Handle Auto Shoutout Add` C# action owns command parsing and config mutation, while `FCS - Run Shoutout` continues to resolve templates at shoutout time. The manifest owns import-time command wiring, and docs/default config describe the new command.

**Tech Stack:** Streamer.bot C# inline actions, Newtonsoft.Json `JObject`/`JArray`, Python `unittest` artifact tests, repository import builder.

---

### Task 1: Contract Tests

**Files:**
- Modify: `modules/first-chat-shoutouts/tests/test_artifacts.py`

- [ ] **Step 1: Write failing artifact tests**

Add expectations for `src/actions/handle-auto-shoutout-add.cs`, `autoAdd`, manifest action and command aliases, docs references, and handler fragments including `announcementTemplate` and `{lastGame}`.

- [ ] **Step 2: Run module tests to verify they fail**

Run: `python3 -B -m unittest discover -s modules/first-chat-shoutouts/tests`

Expected: fail because the new action, manifest entries, config section, and docs do not exist yet.

### Task 2: Command Handler And Manifest

**Files:**
- Create: `modules/first-chat-shoutouts/src/actions/handle-auto-shoutout-add.cs`
- Modify: `modules/first-chat-shoutouts/module.json`
- Modify: `modules/first-chat-shoutouts/src/config/default-config.json`

- [ ] **Step 1: Implement the new C# action**

Create a handler that loads config, checks `autoAdd`, parses `rawInput` as `<login> [template...]`, normalizes the login with the module's strict Twitch login regex, upserts `people`, and saves compact JSON.

- [ ] **Step 2: Wire import metadata**

Add `FCS - Handle Auto Shoutout Add` to `actions` and add a disabled moderator-only command named `First Chat Shoutout Auto Add` with aliases `!soautoadd`, `!addsoauto`, and `!shoutoutautoadd`.

- [ ] **Step 3: Add default config**

Add `autoAdd.enabled`, `autoAdd.moderatorOnly`, and matching aliases to `src/config/default-config.json`.

### Task 3: Documentation And Verification

**Files:**
- Modify: `modules/first-chat-shoutouts/README.md`
- Modify: `modules/first-chat-shoutouts/docs/command-setup.md`
- Modify: `modules/first-chat-shoutouts/docs/manual-test-checklist.md`

- [ ] **Step 1: Document the command**

Describe usage, moderation, upsert behavior, and supported template placeholders including `{lastGame}`.

- [ ] **Step 2: Run focused tests**

Run: `python3 -B -m unittest discover -s modules/first-chat-shoutouts/tests`

Expected: pass.

- [ ] **Step 3: Run full repository tests**

Run: `python3 -B tools/run_tests.py`

Expected: pass.
