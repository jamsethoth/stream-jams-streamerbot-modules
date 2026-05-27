# Streamer.bot Import Command And Trigger Shapes

Research date: 2026-05-27

## Sources Checked

- Streamer.bot Import & Export docs: https://docs.streamer.bot/guide/core/import-export
- Streamer.bot Commands docs: https://docs.streamer.bot/guide/core/commands
- Streamer.bot Twitch First Words docs: https://docs.streamer.bot/api/triggers/twitch/chat/first-words
- Streamer.bot Twitch Stream Online docs: https://docs.streamer.bot/api/triggers/twitch/channel/stream-online
- Auto Shoutout for Twitch: https://extensions.streamer.bot/t/auto-shoutout-for-twitch/3361
- Active Chatter List: https://extensions.streamer.bot/t/active-chatter-list/142
- Shoutout Clip Playing: searched and downloaded by known attachment URL from the extension search result
- Custom Commands: https://extensions.streamer.bot/t/custom-commands/1036
- Mega Shoutout Extension: https://extensions.streamer.bot/t/mega-shoutout-extension/103

Downloaded files for local inspection:

```text
build/research/extensions/autoso.sb
build/research/extensions/active-chatter-list.sb
build/research/extensions/shoutout-clip-playing.sb
build/research/extensions/custom-commands-0.4.0.sb
build/research/extensions/mega-shoutout.json
```

The Mega Shoutout attachment downloaded during this pass was an OBS JSON file, not a Streamer.bot import payload. The page also includes an inline legacy Streamer.bot import code, but that legacy payload does not include command or trigger records relevant to this builder change.

## Official Docs Findings

The Import & Export docs say an export may include actions, triggers, action queues, commands, timed actions, WebSocket clients, and WebSocket servers. They also state exported actions retain their triggers and commands when re-imported.

The Commands docs say chat commands are platform-agnostic and support Twitch, YouTube, and Kick. The same page documents multiple aliases as line-separated command text, matching the decoded command objects.

The Twitch First Words docs say the trigger fires when someone sends their first message of the stream. They also note First Words reset timing defaults to 12 hours and recommend a `Settings -> Reset First Words` sub-action assigned to a Twitch `Stream Online` trigger when you want the reset to happen whenever the stream goes live.

The Twitch Stream Online docs confirm the trigger exists and fires when the Twitch stream starts. I did not find a current Streamer.bot 1.0.4 extension export that exposed the numeric import-schema type for this trigger, so this change intentionally does not generate it.

## Observed Current Import Shapes

AutoSO was the strongest source because it is a Streamer.bot 1.0.4 export and its page describes the same core behavior: when someone on the auto shoutout list sends their first chat of the stream, the extension sends a chat message and triggers a Twitch shoutout.

AutoSO decoded metadata:

```json
{
  "version": 23,
  "minimumVersion": "1.0.0-alpha.1",
  "exportedFrom": "1.0.4",
  "meta": {
    "name": "Auto Twitch Shoutouts",
    "author": "PeterTTX",
    "version": "1.0.0"
  }
}
```

AutoSO command trigger shape:

```json
{
  "commandId": "14d40192-1c7c-415b-8f6a-6144bf548a8d",
  "enabled": true,
  "exclusions": [],
  "id": "7464175e-eb71-4e41-8f83-7e9aa7bce711",
  "type": 401
}
```

AutoSO Twitch First Words trigger shape:

```json
{
  "enabled": true,
  "exclusions": [],
  "id": "49782c0a-bb0b-4129-bac2-06f764678678",
  "isUserId": false,
  "type": 120,
  "username": ""
}
```

AutoSO command object shape:

```json
{
  "caseSensitive": false,
  "command": "!autoso add\r\n!autoso remove\r\n!autoso list\r\n!autoso help\r\n!autoso upgrade",
  "enabled": false,
  "globalCooldown": 0,
  "grantType": 0,
  "group": "Mod Commands",
  "ignoreBotAccount": true,
  "ignoreInternal": true,
  "include": true,
  "location": 0,
  "mode": 0,
  "permittedGroups": ["Moderators"],
  "permittedUsers": [],
  "persistCounter": false,
  "persistUserCounter": false,
  "regexExplicitCapture": false,
  "sources": 1,
  "userCooldown": 0
}
```

Custom Commands 0.4.0 was also a Streamer.bot 1.0.4 export. Its command triggers used the same `type: 401` shape. Its command objects used the same fields, with `mode: 1` for regex commands and a large integer `sources` bitmask for cross-platform command listening.

Shoutout Clip Playing was an older Streamer.bot 0.2.4 export. Its command triggers also used `type: 401`, which increases confidence that command triggers are stable across the observed modern and older import shapes. It also showed older exports may use `sources` as an array such as `[0]`, while current 1.0.4 examples use integer source bitmasks.

Active Chatter List was a legacy `NS4E` import. It did not include triggers; its page instructs users to tie the action to the First Words event manually.

## Builder Decision

Implemented:

- Top-level `data.commands` generation from module manifests.
- Command trigger generation on target actions using observed `type: 401`.
- Twitch First Words trigger generation using observed AutoSO `type: 120`, `username`, and `isUserId` fields.

Not implemented:

- Twitch Stream Online trigger generation. The official docs confirm the trigger exists, but I did not find a trusted current export payload showing its numeric import type or parameters. The module still documents the Stream Online reset wiring as a manual post-import step.

This is deliberately conservative: command and First Words shapes are backed by inspected imports, while Stream Online remains manual until we have a known-good export or another current extension payload to clone.
