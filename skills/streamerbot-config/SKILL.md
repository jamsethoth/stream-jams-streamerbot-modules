---
name: streamerbot-config
description: Use when helping configure Streamer.bot actions, commands, triggers, variables, OBS/chat integrations, C# code sub-actions, HTTP/WebSocket testing, extension imports, .sb import strings, or import/export planning for a local Streamer.bot streaming automation setup.
---

# Streamer.bot Config

## Overview

Use this skill to turn a streaming automation request into a Streamer.bot configuration plan, exact UI steps, C# code, or a test procedure against a local Streamer.bot instance.

Streamer.bot is action-first: an Action contains ordered Sub-Actions; Triggers and Commands invoke Actions; variables flow through the current action as arguments unless persisted as globals.

## Workflow

1. Clarify the desired stream outcome: chat response, OBS change, reward redemption, sound/media cue, counter/state update, API call, moderation, overlay event, or scripted logic.
2. Map the outcome to one Action with a clear name and group. Prefer built-in sub-actions before C#.
3. Choose the invocation path:
   - Chat command: create a Command, then add a Command trigger to the Action.
   - Platform event: add the matching Twitch/YouTube/Kick/etc. trigger.
   - OBS/app event: add the matching integration trigger.
   - External caller: use HTTP or WebSocket `DoAction` after the action exists.
4. Define the action's argument contract: expected trigger variables, command input, custom HTTP/WebSocket args, and globals.
5. Specify sub-actions in execution order. Mention variable names each step creates or consumes.
6. If the user wants a reusable bundle, decide between UI instructions, C# paste-in code, local API test calls, or an experimental `.sb` import string.
7. Add test steps: use Action History/argument inspection, Global Variables viewer, Streamer.bot logs, and HTTP/WebSocket calls when enabled.

## Reference Selection

Load only the reference needed for the request:

- `references/actions-commands.md`: basic actions, commands, triggers, cooldowns, sub-action ordering, and UI setup.
- `references/variables-state.md`: arguments, `%var%`, globals, user globals, inline functions, counters, and persistent state.
- `references/csharp-actions.md`: C# `CPHInline`, `TryGetArg`, globals, chat replies, JSON/API calls, and script review checklist.
- `references/local-api.md`: HTTP/WebSocket capabilities, direct testing, and the limits around creating/editing actions.
- `references/import-strings.md`: `.sb` import structure, safe fixture-driven generation, action/command/trigger ownership, stub requests, and import debugging.

If a request depends on a precise method, trigger variable, or sub-action option not in the references, check the live docs before finalizing.

## Output Patterns

For UI configuration, give:

```text
Action: <Group> / <Name>
Trigger: <type and exact options>
Command: <aliases, matching mode, permissions, cooldowns>
Sub-actions:
1. <sub-action name> - <key settings>
2. <sub-action name> - <key settings>
Variables used: <inputs and outputs>
Test: <how to trigger and what to inspect>
```

For C# actions, provide a complete `CPHInline` class unless the user asks for a snippet. Include required `using` directives. Use `CPH.TryGetArg<T>()` for action args, null/default handling for globals, and `CPH.LogInfo()` for useful diagnostics. Return `false` only when intentionally stopping later sub-actions.

For local API testing, first state what Streamer.bot must have enabled locally. Do not claim the API can create or edit actions unless current docs prove it. Treat HTTP/WebSocket as runtime control and inspection by default.

For import strings, prefer working from a known-good export from the user's Streamer.bot version. Use `scripts/sb_import_string.py` to inspect/decode/encode `.sb` strings and `scripts/streamerbot_sb_import_gen.py` for fixture-driven module imports. Read `references/import-strings.md` before generating or changing action, command, trigger, or built-in sub-action records. Label generated strings as experimental until imported into a disposable Streamer.bot profile and C# sub-actions compile.

## Safety

- Avoid spam loops: add cooldowns, permission checks, or guard variables to chat commands that emit chat.
- Avoid destructive moderation actions unless the user explicitly asks and confirms scope.
- Avoid leaking secrets in logs, chat messages, global variables, or exported action bundles.
- Prefer exact action IDs over names for remote calls when available, but include the name for human traceability.
- Do not import third-party extension strings blindly; inspect their contents, setup steps, version requirements, external API calls, file paths, and enabled commands first.
