# Streamer.bot Imports Agent Guide

This document is written for future agents that need to generate, inspect, or explain Streamer.bot imports from this repository, and for updating the local `streamerbot-config` skill. It captures the repo-specific implementation and the import schema lessons learned from inspected Streamer.bot exports.

## Source Of Truth

Use these local files before relying on memory:

- `tools/streamerbot_import/sb_import_string.py`: decodes, inspects, and encodes modern `.sb` import files.
- `tools/streamerbot_import/build_module_import.py`: builds one module import from a manifest and a Streamer.bot export fixture.
- `tools/streamerbot_import/build_all_modules.py`: builds release artifacts for all modules.
- `tools/streamerbot_import/fixtures/streamerbot-1.0.4-csharp-stub.json`: generic Streamer.bot 1.0.4 C# action fixture.
- `tools/streamerbot_import/fixtures/streamerbot-1.0.4-first-chat-shoutouts-stub.json`: module-specific fixture that preserves First Chat Shoutouts trigger and reset-action shapes.
- `skills/streamerbot-config/`: installable Codex skill bundle containing the reconciled import reference and bundled helper scripts.
- `skills/streamerbot-config/scripts/streamerbot_sb_import_gen.py`: installable fixture-driven import generator for module manifests.
- `skills/streamerbot-config/scripts/fixtures/streamerbot-import-stub.sb`: user-provided Streamer.bot 1.0.4 export preserved as the single `.sb` reference fixture.
- `docs/research/2026-05-27-streamerbot-import-command-trigger-shapes.md`: research notes from official docs, approved extensions, and the user-provided Streamer.bot 1.0.4 stub.
- `tests/tools/test_build_module_import.py`: executable specification for generated command, trigger, reference, and template behavior.

Treat generated imports as experimental until they are imported into a disposable Streamer.bot profile and the C# sub-actions compile there.

## Modern `.sb` File Format

Observed current Streamer.bot `.sb` files are ASCII import strings with this layout:

```text
base64("SBAE" + gzip(json))
```

The decoded JSON has this high-level shape:

```json
{
  "version": 23,
  "minimumVersion": "1.0.0-alpha.1",
  "exportedFrom": "1.0.4",
  "meta": {
    "name": "Module Name",
    "author": "Author",
    "version": "0.1.0",
    "description": "...",
    "autoRunAction": null
  },
  "data": {
    "actions": [],
    "commands": [],
    "queues": [],
    "timers": [],
    "websocketServers": [],
    "websocketClients": []
  }
}
```

The helper only supports the modern `SBAE` format. Older approved extensions may use legacy headers such as `NS4E` after base64 decoding. Do not use those as generation bases for current Streamer.bot imports unless targeting and testing against that old Streamer.bot version.

## Inspect, Decode, Encode

From the repository root:

```bash
python3 -m tools.streamerbot_import.sb_import_string inspect path/to/import.sb
python3 -m tools.streamerbot_import.sb_import_string decode path/to/import.sb path/to/import.json
python3 -m tools.streamerbot_import.sb_import_string encode path/to/import.json path/to/import.sb
```

`inspect` reports top-level version metadata and counts. `decode` writes readable JSON. `encode` accepts JSON or `.sb` input and writes the requested output format based on the output suffix.

If a file cannot be decoded, do not infer the schema from the UI error alone. Decode or inspect it first. A disabled Streamer.bot Import button commonly means the payload is not a valid modern import string, is malformed, or targets an incompatible export format.

## Module Manifest Contract

Each module lives under `modules/<module-id>/` and owns a `module.json`. The builder requires:

```json
{
  "id": "first-chat-shoutouts",
  "name": "First Chat Shoutouts",
  "version": "0.1.0",
  "description": "Twitch-first shoutouts...",
  "group": "First Chat Shoutouts",
  "actions": []
}
```

Optional but commonly used fields:

- `license`: copied into release metadata/docs, not used by the import builder.
- `defaultConfig`: validated by the all-modules builder.
- `importStub`: repo-relative path to a module-specific Streamer.bot fixture. If omitted, the generic 1.0.4 C# stub is used.
- `references`: framework DLLs to add to each generated C# sub-action.
- `commands`: command records to generate under top-level `data.commands`.

Action entries:

```json
{
  "name": "FCS - Handle Twitch First Words",
  "source": "src/actions/handle-twitch-first-words.cs",
  "autoRun": false,
  "triggers": [
    {
      "type": "twitch-first-words"
    }
  ]
}
```

Command entries:

```json
{
  "name": "First Chat Shoutout",
  "action": "FCS - Handle Manual Twitch Shoutout",
  "aliases": ["!so", "!shoutout"],
  "enabled": false,
  "group": "First Chat Shoutouts",
  "permittedGroups": ["Moderators"],
  "sources": 1
}
```

`aliases` become one CRLF-separated `command` string. A literal `command` field can be supplied instead. The builder also supports Streamer.bot command fields such as `caseSensitive`, `globalCooldown`, `grantType`, `ignoreBotAccount`, `ignoreInternal`, `include`, `location`, `mode`, `permittedUsers`, `persistCounter`, `persistUserCounter`, `regexExplicitCapture`, and `userCooldown`.

## Builder Workflow

`build_module_import.prepare_module_import()` does this:

1. Load the module manifest.
2. Resolve the input fixture. Use the module's `importStub` when present; otherwise use the generic C# stub.
3. Decode the fixture with `read_payload()`.
4. Select action templates from the fixture.
5. Collect trigger templates by numeric trigger type from the fixture.
6. Build a reference list from the manifest and always include `System.Core.dll`.
7. Validate known C# `using` directives against required references.
8. Build generated actions from action templates and C# source files.
9. Build generated command records from manifest commands.
10. Attach command triggers and configured action triggers to generated actions.
11. Update import metadata and `meta.autoRunAction`.
12. Replace top-level `data.actions` and `data.commands`.
13. Encode the payload back to `.sb` or JSON.

Build one module:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/first-chat-shoutouts \
  build/first-chat-shoutouts.sb
```

Build with an explicit stub:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/first-chat-shoutouts \
  build/first-chat-shoutouts.sb \
  --stub exports/fcs-stub.sb
```

Build all release artifacts:

```bash
python3 -m tools.streamerbot_import.build_all_modules --output dist/modules
```

For every module, `build_all_modules` writes:

```text
<module-id>.sb
<module-id>.import.txt
README.md
module.json
manifest.json
```

The `.import.txt` file is byte-for-byte the same import string as the `.sb` file, just with a paste-friendly extension.

## Fixture-Driven Generation

The builder does not invent a complete Streamer.bot schema. It clones known-good Streamer.bot export records and mutates only the parts this repo owns.

Template selection is important:

- The builder finds the first exported action containing C# code and uses it as a fallback template.
- For action-specific shapes, it matches exported C# actions by normalized suffix.
- `FCS Stub - Reset Stream State` and `FCS - Reset Stream State` both normalize to `reset stream state`, so the reset action gets the reset-specific template.
- If no matching action template exists, the first C# action template is used.

This is why module-specific fixtures matter. A plain one-action C# stub can generate pure C# actions, but it cannot preserve built-in sub-actions or specialized trigger fields for a specific action. The First Chat Shoutouts fixture preserves the reset action's built-in `Reset First Words` sub-action and stream-online trigger because those are present on the matching reset action in the fixture.

Current builder limitation: every generated action template must contain at least one C# code block. If an action should be built-in-only, either include a no-op C# block in the stub for now or extend the builder and tests deliberately.

## C# Sub-Actions And References

Observed Streamer.bot 1.0.4 C# sub-actions use `type: 99999` and store source in a base64 `byteCode` field. The builder also supports older/plain `code` fields when they contain `public class CPHInline` and `public bool Execute()`.

For each generated C# sub-action, the builder:

- assigns a deterministic UUIDv4-shaped ID
- keeps sub-action order
- remaps `parentId` when the original parent sub-action was regenerated
- appends required references
- replaces C# source with the corresponding file under `src/actions/`

Reference validation is intentionally conservative. It currently maps:

```text
using System.Linq; -> System.Core.dll
using System.Text.RegularExpressions; -> System.dll
```

If Streamer.bot compilation fails with a missing namespace, add a required-reference mapping in `build_module_import.py`, add the framework DLL to the module manifest if needed, and add an import-builder test. Streamer.bot compilation remains the final authority because the validator only knows mappings that have been explicitly encoded.

## Commands And Command Triggers

Streamer.bot imports can include commands. In the observed current shape, commands are top-level records in `data.commands`.

The command object does not own the action link. The action owns a trigger with `type: 401` and a `commandId` pointing to the command record:

```json
{
  "commandId": "512a73a4-22d3-4df2-a7a9-e3714928b087",
  "enabled": true,
  "exclusions": [],
  "id": "generated-trigger-id",
  "type": 401
}
```

This distinction matters. To attach `!so` to `FCS - Handle Manual Twitch Shoutout`, generate a command record named `First Chat Shoutout`, then add the `type: 401` trigger to the `FCS - Handle Manual Twitch Shoutout` action. Do not expect the command object itself to contain the action pointer.

Imported commands should usually be disabled by default. Let the user inspect permissions, cooldowns, command aliases, and target actions before enabling them in Streamer.bot.

## Supported Trigger Generation

The builder currently supports only trigger shapes backed by inspected exports.

Command trigger:

```json
{
  "type": 401,
  "commandId": "<generated command id>",
  "enabled": true,
  "exclusions": [],
  "id": "<generated trigger id>"
}
```

Twitch First Words:

```json
{
  "type": 120,
  "enabled": true,
  "exclusions": [],
  "id": "<generated trigger id>",
  "isUserId": false,
  "username": ""
}
```

Twitch Stream Online:

```json
{
  "type": 14005,
  "enabled": true,
  "exclusions": [],
  "id": "<generated trigger id>",
  "obsId": null
}
```

Unsupported trigger manifest types raise `ValueError`. To add another trigger type, first obtain a current Streamer.bot export or approved extension that contains the exact trigger, decode it, document the observed fields, implement the builder branch, and add tests that assert the generated shape.

## Built-In Sub-Actions

Built-in sub-actions are preserved from templates, not synthesized from scratch. The important current example is Streamer.bot's `Reset First Words` sub-action:

```json
{
  "enabled": true,
  "id": "<regenerated id>",
  "index": 0,
  "parentId": null,
  "type": 1026,
  "weight": 0.0
}
```

For First Chat Shoutouts, this sub-action belongs to `FCS - Reset Stream State`, before that action's C# sub-action. The Stream Online trigger also belongs to `FCS - Reset Stream State`. It is not attached to the manual shoutout action and it is not represented by a command record.

When a user supplies a stub, ask them to attach each trigger and built-in sub-action to the exact action that should own it before exporting. The builder can preserve and mutate known action shapes, but it cannot infer the intended owner of a built-in component from a loose description.

## Deterministic IDs

The builder uses deterministic IDs so builds are reproducible. It derives UUIDs from:

```text
streamerbot-module:<module-id>
```

and record-specific names such as:

```text
action:FCS - Run Shoutout
command:First Chat Shoutout
trigger:FCS - Handle Manual Twitch Shoutout:command:First Chat Shoutout
subaction:FCS - Reset Stream State:0:1026
```

The generated UUIDs are deterministic but shaped as UUIDv4 because inspected Streamer.bot 1.0.4 exports used UUIDv4-shaped IDs. Preserve internal references when IDs change, especially `commandId` and `parentId`.

## Importing Into Streamer.bot

Use a disposable Streamer.bot profile first.

1. Build or obtain the `.sb` file.
2. In Streamer.bot, open `Import`.
3. Load the `.sb` file, drag it into the import field, or paste the contents of the `.import.txt` file.
4. Confirm the Import button becomes enabled.
5. Review the import preview for expected actions, commands, and triggers.
6. Complete the import.
7. Open each imported C# action and compile it.
8. Run the configure/defaults action if `meta.autoRunAction` did not run or if defaults are missing.
9. Inspect triggers on the target actions.
10. Inspect imported commands and enable them only when permissions and cooldowns are correct.
11. Run the module's manual test checklist before using it live.

If the Import button is disabled:

- run `sb_import_string.py inspect` to confirm the file decodes
- decode to JSON and verify `version`, `minimumVersion`, `exportedFrom`, `meta`, and `data`
- confirm the file is modern `SBAE`, not a legacy payload
- rebuild from a fixture exported by the user's current Streamer.bot version
- compare a user-created stub import against the generated output

If Streamer.bot reports "export is too old", do not use that extension as a base fixture for current imports. It may still be useful as a conceptual reference, but generation should use a current export.

## How To Request A Useful Stub From A User

Ask for a stub when the import must preserve action-specific UI-only or built-in schema that is not already covered by tests.

Good stub request:

```text
In your current Streamer.bot version, create a temporary group with one action per desired final action. Give each action a name that ends with the same suffix as the intended final action, such as "FCS Stub - Reset Stream State". Add a tiny compiling C# sub-action to every action. Attach any real triggers, commands, or built-in sub-actions to the exact action that should own them. Export those actions and related commands/triggers as one .sb file.
```

Bad stub request:

```text
Export any action with a C# block.
```

That is only enough for pure C# action cloning. It is not enough for preserving built-in reset actions, event trigger field shapes, or command trigger examples.

## Agent Checklist For Adding Import Features

1. Read `module.json`, existing docs, and relevant tests.
2. Decode the fixture or user stub that contains the new shape.
3. Record the observed Streamer.bot version and exact JSON shape in `docs/research/`.
4. Add or update manifest syntax only for fields the builder can emit.
5. Extend `build_module_import.py` for the new shape.
6. Add tests in `tests/tools/test_build_module_import.py` that verify decoded JSON, not just command success.
7. Add module artifact tests when module docs or config change.
8. Build the module import.
9. Inspect the built import with `sb_import_string.py inspect`.
10. Decode the built import when checking action/trigger ownership.
11. Run `python3 -B tools/run_tests.py`.
12. Ask the user to import into a disposable Streamer.bot profile and compile the C# actions before treating it as live-ready.

## Common Failure Modes

No C# code block found:
The fixture does not contain a recognizable C# sub-action. Export a stub action with a compiling `CPHInline` class, or fix the fixture.

Missing C# reference:
The source imports a namespace that needs a framework DLL not listed in the manifest or known validation map. Add the reference and a regression test.

Import button disabled:
The import string is malformed, legacy, unsupported by the current Streamer.bot version, or not an actual Streamer.bot import payload. Decode and inspect before changing generation logic.

Imported action compiles with missing namespace errors:
Inspect the generated sub-action `references` list. Streamer.bot may need an explicit framework DLL. The repo validator only catches namespaces it knows about.

Trigger attached to the wrong action:
Remember that triggers live on action records. Command triggers point from an action to a command via `commandId`. Re-check the manifest `commands[].action`, action `triggers`, and decoded generated JSON.

Built-in sub-action missing:
The selected template did not contain it. Use a module-specific stub with a matching action suffix and the built-in sub-action attached to that action.

Old approved extension cannot import:
Do not assume approved extension examples are current. Some are useful for conceptual behavior only. Use current Streamer.bot exports for generation.

Name collisions or overwrites:
Streamer.bot may overwrite or merge by name during import. Use disposable profiles and clear names/groups before live import.

## Skill Update Notes

For the `streamerbot-config` skill, the import-string reference should teach these rules:

- Prefer a known-good export from the user's current Streamer.bot version.
- Modern imports are `base64("SBAE" + gzip(json))`; inspect/decode before editing.
- Imports can include actions, commands, triggers, queues, timers, and WebSocket clients/servers.
- Commands are top-level records; command-to-action wiring is an action trigger with `type: 401` and `commandId`.
- Triggers live on actions. Do not infer ownership from command names or docs text.
- Built-in sub-actions should be preserved from user-created stubs unless the exact schema is already proven.
- Import generation should be fixture-driven and conservative, not handwritten from memory.
- Generated imports remain experimental until imported into a disposable profile and C# actions compile.
- When Streamer.bot rejects an import or disables the Import button, decode/inspect the payload and compare it with a same-version export.
- Add reference validation for known C# namespaces, but still treat Streamer.bot compilation output as authoritative.
