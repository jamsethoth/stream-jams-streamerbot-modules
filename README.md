# Stream Jams Streamer.bot Modules

Streamer.bot modules, source files, and import tooling for Stream Jams automations.

This repository is organized as a small monorepo. Each module owns its Streamer.bot C# action sources, default config, docs, and module manifest under `modules/<module-id>/`. Shared import-string tooling lives in `tools/streamerbot_import/`, and tests cover both shared tools and module-specific artifacts.

## Modules

- `modules/activity-gated-chat-announcements`: recurring chat announcements that only post after enough real chat activity has occurred.

## Layout

```text
modules/
  <module-id>/
    module.json
    src/actions/
    src/config/
    docs/
tools/
  streamerbot_import/
tests/
  modules/
  tools/
```

## Build A Module Import

First export a known-good Streamer.bot action containing one compiling `Execute C# Code` sub-action from your local Streamer.bot version. Save it under `exports/`, then run:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/activity-gated-chat-announcements \
  exports/csharp-stub.sb \
  build/activity-gated-chat-announcements.sb
```

Generated `.sb` bundles are intentionally ignored by Git. Import generated bundles into a disposable Streamer.bot profile first, compile the imported C# actions, then move them into your live profile.

## Test

```bash
python3 -B -m unittest discover -s tests -t .
```
