# Import Prep

Use this workflow to prepare First Chat Shoutouts as a Streamer.bot import without inventing an untested C# action schema.

The repository includes a committed Streamer.bot 1.0.4 C# action fixture. Normal builds use that fixture in CI, releases, and local development.

## 1. Build The Import

From the repository root:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/first-chat-shoutouts \
  build/first-chat-shoutouts.sb
```

The script clones the committed C# action shape, replaces the source code with this module's actions, and writes an experimental `.sb` file.

## 2. Import And Inspect

In a disposable Streamer.bot profile:

1. Click `Import`.
2. Load or paste `build/first-chat-shoutouts.sb`.
3. Confirm the import contains the `FCS - ...` actions.
4. Open the imported actions and compile the C# sub-actions.
5. Run `FCS - Configure Defaults` if Streamer.bot does not auto-run it after import.
6. Wire the triggers and commands from the setup docs before live use.

## Failure Modes

- If the script says no C# code block was found, the committed fixture or custom stub does not contain a recognizable Streamer.bot C# action shape.
- If Streamer.bot rejects the prepared `.sb`, decode the stub and prepared files to JSON and compare the action shape.
- If an imported action does not compile, inspect that action's C# source and Streamer.bot's compile output before using the module live.
