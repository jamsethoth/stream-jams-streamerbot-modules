# Stream Jams Streamer.bot Modules

Streamer.bot modules, source files, and import tooling for Stream Jams automations.

This repository is organized as a small monorepo. Each module owns its Streamer.bot C# action sources, default config, docs, and module manifest under `modules/<module-id>/`. Shared import-string tooling lives in `tools/streamerbot_import/`, and tests cover both shared tools and module-specific artifacts.

## Modules

- `modules/activity-gated-chat-announcements`: recurring chat announcements that only post after enough real chat activity has occurred.
- `modules/first-chat-shoutouts`: Twitch-first shoutouts for configured first-chat visitors and moderator command invocations.

## Layout

```text
modules/
  <module-id>/
    module.json
    src/actions/
    src/config/
    docs/
    tests/
tools/
  streamerbot_import/
skills/
  streamerbot-config/
tests/
  tools/
```

## Build A Module Import

The repository includes the canonical Streamer.bot C# action fixture used by CI and releases. To build one module from that fixture, run:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/activity-gated-chat-announcements \
  build/activity-gated-chat-announcements.sb
```

Generated `.sb` bundles are intentionally ignored by Git. Import generated bundles into a disposable Streamer.bot profile first, compile the imported C# actions, then move them into your live profile.

For advanced compatibility testing against a different Streamer.bot export shape, pass a custom exported C# action stub with `--stub exports/csharp-stub.sb`.

For implementation details intended for future agents and skill updates, see `docs/streamerbot-imports-agent-guide.md`.

## Installable Codex Skill

This repository includes the Streamer.bot Codex skill at `skills/streamerbot-config/`. The skill bundle includes `SKILL.md`, reference docs, UI metadata, `scripts/sb_import_string.py` for inspect/decode/encode, `scripts/streamerbot_sb_import_gen.py` for fixture-driven `.sb` generation from module manifests, and same-version Streamer.bot import fixtures for future reference.

To install it into a local Codex skills directory, copy or sync `skills/streamerbot-config/` to `$CODEX_HOME/skills/streamerbot-config/`.

## Build All Module Artifacts

CI builds every module from the committed Streamer.bot C# stub fixture:

```bash
python3 -m tools.streamerbot_import.build_all_modules --output dist/modules
```

Each module receives:

```text
<module-id>.sb
<module-id>.import.txt
README.md
module.json
manifest.json
```

Each module README must include `## What It Does`, `## Installation`, `## Configuration`, and `## Generated Actions`. The build fails if those sections are missing or if the generated `.sb` import cannot be decoded.

## Manual Release Archive

Run the `module-release` GitHub Actions workflow manually to produce a compressed release archive. To reproduce that archive locally:

```bash
python3 -m tools.streamerbot_import.build_all_modules \
  --output dist/modules \
  --archive dist/stream-jams-streamerbot-modules.zip
```

The archive uses sorted file order, fixed zip timestamps, stable JSON serialization, deterministic action IDs, and a committed Streamer.bot stub fixture so identical repository content produces identical artifacts.

## Test

```bash
python3 -B tools/run_tests.py
```
