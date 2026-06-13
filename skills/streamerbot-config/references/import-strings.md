# Streamer.bot `.sb` Imports

Use this reference when a task involves inspecting, generating, modifying, or debugging Streamer.bot imports, extension `.sb` files, disabled Import buttons, command wiring, trigger wiring, or built-in sub-actions.

## Table Of Contents

- [Core Rule](#core-rule)
- [Current `.sb` Format](#current-sb-format)
- [Helper Script](#helper-script)
- [What Imports Can Contain](#what-imports-can-contain)
- [Generation Strategy](#generation-strategy)
- [Useful Stub Requests](#useful-stub-requests)
- [Action And Sub-Action Records](#action-and-sub-action-records)
- [Commands And Command Triggers](#commands-and-command-triggers)
- [Observed Trigger Shapes](#observed-trigger-shapes)
- [Built-In Sub-Actions](#built-in-sub-actions)
- [IDs And Internal References](#ids-and-internal-references)
- [Import Procedure](#import-procedure)
- [Debugging](#debugging)
- [Review Checklist](#review-checklist)

## Core Rule

Generate imports conservatively from a known-good export from the user's current Streamer.bot version. Do not handwrite a full import schema from memory when a fixture or user stub can preserve Streamer.bot's own record shape.

Generated imports are experimental until they are imported into a disposable Streamer.bot profile and any C# sub-actions compile there.

## Current `.sb` Format

Streamer.bot docs call the import code a UUEncoded string. Current sampled `.sb` files are ASCII text with this layout:

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
    "name": "Extension Or Module Name",
    "author": "Author",
    "version": "0.1.0",
    "description": "...",
    "autoRunAction": null,
    "minimumVersion": null
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

Observed version examples:

- Streamer.bot 1.0.4 export: `version: 23`, `minimumVersion: "1.0.0-alpha.1"`, `exportedFrom: "1.0.4"`.
- Streamer.bot 0.2.8 export: `version: 11`, `minimumVersion: "0.2.4-beta.6"`, `exportedFrom: "0.2.8"`.

Older approved extensions may use legacy magic headers, such as strings beginning with `TlM0RR` (`NS4E` after base64). Do not generate legacy formats unless explicitly targeting an old Streamer.bot version and after testing in that version.

## Helper Script

Use the bundled script from the skill directory:

```bash
python3 scripts/sb_import_string.py inspect extension.sb
python3 scripts/sb_import_string.py decode extension.sb decoded.json
python3 scripts/sb_import_string.py encode decoded.json generated.sb
```

`inspect` shows metadata and top-level item counts. `decode` writes readable JSON. `encode` reads JSON and writes a modern `SBAE` `.sb`.

The script is intentionally narrow: it is for modern `SBAE` imports. If it rejects an import, inspect the first decoded bytes or use another decoder before changing generation logic.

## Generation Script

The skill also includes `scripts/streamerbot_sb_import_gen.py` for fixture-driven module imports. Use it when a repository has a module manifest with Streamer.bot action source files.

Bundled fixtures:

- `scripts/fixtures/streamerbot-1.0.4-csharp-stub.json`: minimal current C# action fixture for pure C# action cloning.
- `scripts/fixtures/streamerbot-import-stub.sb`: same-version Streamer.bot 1.0.4 export reference containing multiple actions, command wiring, triggers, and a built-in reset sub-action.

Build from a manifest `importStub` or the bundled generic C# stub:

```bash
python3 scripts/streamerbot_sb_import_gen.py modules/my-module build/my-module.sb
```

Build from an explicit same-version exported stub:

```bash
python3 scripts/streamerbot_sb_import_gen.py \
  modules/my-module \
  build/my-module.sb \
  --stub exports/my-current-streamerbot-stub.sb
```

The generator expects `module.json` with `id`, `name`, `version`, `description`, `group`, and `actions`. Each action needs a `name` and C# `source`. Optional manifest fields include `references`, `commands`, `importStub`, and action `triggers`.

Supported generated trigger manifest types are deliberately narrow: `twitch-first-words` and `twitch-stream-online`, plus command triggers from `commands[].action`. Add new trigger types only after inspecting a same-version export and updating the generator/tests.

## What Imports Can Contain

Streamer.bot imports can include actions, triggers, action queues, commands, timed actions, WebSocket clients, and WebSocket servers. In the decoded JSON, triggers are stored on action records; commands are stored as top-level records in `data.commands`.

Approved extension pages commonly provide:

- an inline import code
- a `.sb` file attachment
- setup notes, version requirements, and post-import enable/configuration steps

Approved does not mean current. If Streamer.bot says an export is too old, do not use that extension as a base fixture for a current generated import.

## Generation Strategy

Prefer this workflow:

1. Ask the user to export a minimal similar setup from their current Streamer.bot version.
2. Decode it.
3. Preserve Streamer.bot-owned record shapes from that export.
4. Change only owned fields: names, groups, IDs, command aliases, trigger targets, C# code, config values, and metadata.
5. Regenerate IDs that must not collide while preserving internal references.
6. Encode the generated import.
7. Import into a disposable profile, inspect the preview, and compile C# sub-actions.

This replaces the older loose guidance that Codex can safely generate an import by constructing JSON directly. Codex can encode JSON into a valid modern `.sb`, but the JSON schema should come from an inspected current export or a proven fixture.

When building a reusable generator, make it fixture-driven:

- Clone an exported action or module fixture.
- Match action-specific templates by name or another stable key.
- Preserve built-in sub-actions from the template.
- Preserve trigger field shapes from a same-version export.
- Add regression tests against decoded JSON for every generated command, trigger, reference, and built-in sub-action.

## Useful Stub Requests

Ask for a user stub when you need Streamer.bot-owned shapes that are not already proven.

Good request:

```text
In your current Streamer.bot version, create a temporary group with one action per desired final action. Give each action a name that ends with the same suffix as the intended final action, such as "FCS Stub - Reset Stream State". Add a tiny compiling C# sub-action to every action. Attach any real triggers, commands, or built-in sub-actions to the exact action that should own them. Export those actions and related commands/triggers as one .sb file.
```

Do not ask only for "any action with a C# block" when the import must preserve command wiring, event trigger fields, or built-in sub-actions. A one-action C# stub is enough for pure C# action cloning only.

## Action And Sub-Action Records

Streamer.bot is action-first. An action owns:

- ordered `subActions`
- `triggers`
- group/name/enabled settings

Observed C# sub-actions in Streamer.bot 1.0.4 use `type: 99999` and store source in a base64 `byteCode` field. Older or hand-made fixtures may expose a plain `code` field. Preserve the surrounding sub-action fields and replace only the code-bearing field.

When replacing IDs, also remap any `parentId` values that point to regenerated sub-actions.

C# references matter. The bundled generator scans C# `using` directives, keeps
manifest-provided references, and adds known framework references automatically.
For example, source that uses `System.Text.RegularExpressions` receives:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
```

It also adds the baseline `mscorlib.dll` reference that defines core types such
as `System.Object`, `System.String`, and `System.Boolean`. If a source imports an
unknown namespace, update the generator's namespace mapping or host-provided
namespace list before building. If Streamer.bot still reports missing namespaces
during compile, inspect the generated action's `references` list and add a
regression check to future generators.

## Commands And Command Triggers

Streamer.bot imports can include commands as top-level records in `data.commands`. Multiple aliases are stored as line-separated command text, observed as CRLF-separated strings:

```json
{
  "caseSensitive": false,
  "command": "!so\r\n!shoutout",
  "enabled": false,
  "globalCooldown": 0,
  "grantType": 0,
  "group": "First Chat Shoutouts",
  "ignoreBotAccount": true,
  "ignoreInternal": true,
  "include": true,
  "location": 0,
  "mode": 0,
  "name": "First Chat Shoutout",
  "permittedGroups": ["Moderators"],
  "permittedUsers": [],
  "persistCounter": false,
  "persistUserCounter": false,
  "regexExplicitCapture": false,
  "sources": 1,
  "userCooldown": 0
}
```

The command record does not own the action link. The target action owns a command trigger with `type: 401` and a `commandId` pointing to the command:

```json
{
  "commandId": "512a73a4-22d3-4df2-a7a9-e3714928b087",
  "enabled": true,
  "exclusions": [],
  "id": "generated-trigger-id",
  "type": 401
}
```

This is the important ownership rule: to wire `!so` to an action, create the command record and attach a `type: 401` trigger to the action. Do not infer command-action wiring from the command object itself.

Imported commands should normally start disabled so the user can inspect permissions, aliases, cooldowns, and target action wiring before enabling them.

## Observed Trigger Shapes

Use only trigger shapes backed by current docs or inspected exports.

Command trigger:

```json
{
  "commandId": "<command id>",
  "enabled": true,
  "exclusions": [],
  "id": "<trigger id>",
  "type": 401
}
```

Twitch First Words:

```json
{
  "enabled": true,
  "exclusions": [],
  "id": "<trigger id>",
  "isUserId": false,
  "type": 120,
  "username": ""
}
```

Twitch Stream Online:

```json
{
  "enabled": true,
  "exclusions": [],
  "id": "<trigger id>",
  "obsId": null,
  "type": 14005
}
```

If a new trigger type is needed, obtain a same-version export containing that trigger, decode it, document its fields, then generate from that observed shape.

## Built-In Sub-Actions

Built-in sub-actions should be preserved from a same-version fixture unless their exact schema is already proven. Do not synthesize them from a name alone.

Observed `Reset First Words` sub-action:

```json
{
  "enabled": true,
  "id": "<sub-action id>",
  "index": 0,
  "parentId": null,
  "type": 1026,
  "weight": 0.0
}
```

Attach built-in sub-actions to the exact action that should own them before exporting the stub. For example, a stream reset action may own both a Stream Online trigger and a `Reset First Words` sub-action. That reset sub-action is not represented by a command and should not be attached to a manual shoutout action.

## IDs And Internal References

Streamer.bot exports use UUID-shaped IDs. Current sampled 1.0.4 exports used UUIDv4-shaped IDs.

When generating IDs:

- regenerate action, command, trigger, and sub-action IDs to avoid collisions
- preserve or rewrite internal references consistently
- keep `commandId` pointing to the generated command ID
- keep `parentId` pointing to the generated parent sub-action ID
- keep deterministic IDs when reproducible artifacts matter

## Import Procedure

In a disposable Streamer.bot profile:

1. Click `Import`.
2. Load the `.sb` file, drag it into the import field, or paste the import string.
3. Confirm the Import button becomes enabled.
4. Review the preview for expected actions, commands, triggers, queues, timers, and WebSocket records.
5. Complete the import.
6. Open and compile each C# sub-action.
7. Run any configure/defaults action if it did not auto-run.
8. Inspect triggers on the target actions.
9. Inspect imported commands and enable them only after confirming permissions and cooldowns.
10. Run a manual test path before using the import live.

## Debugging

Import button disabled:

- inspect/decode the `.sb`
- confirm it is a modern `SBAE` payload, or intentionally handle its legacy format
- verify top-level `version`, `minimumVersion`, `exportedFrom`, `meta`, and `data`
- compare against a same-version export from the user's Streamer.bot
- rebuild from a current stub if the base extension is too old

Streamer.bot says the export is too old:

- do not use that file as the generation base for current Streamer.bot
- use it only as conceptual documentation if still useful
- ask for a current-version stub

Imported action compiles with missing namespace errors:

- inspect the C# `using` directives
- inspect sub-action `references`
- add required DLL references
- rerun compilation in Streamer.bot

Trigger attached to the wrong action:

- inspect decoded JSON
- remember triggers live on action records
- check command triggers by `commandId`
- verify action names and template matching

Built-in sub-action missing:

- the selected template did not include it
- ask for a stub with that built-in sub-action attached to the intended action

## Review Checklist

- Top-level version metadata matches the intended Streamer.bot generation.
- `meta.name`, `meta.author`, `meta.version`, `meta.description`, and `meta.autoRunAction` are intentional.
- Every imported command starts disabled unless there is a clear reason to enable it.
- Command aliases, permissions, cooldowns, and source bitmasks are reviewed.
- Command triggers live on the target actions and point to generated command IDs.
- Event triggers live on the intended actions.
- Built-in sub-actions are preserved from same-version exports.
- C# code has required references and no unsafe file, process, URL, secret, moderation, or spam behavior.
- OBS or integration sub-actions reference user-available scenes, sources, or connections.
- Queues and concurrency behavior are understood.
- Existing names will not accidentally overwrite live actions, commands, or triggers.
