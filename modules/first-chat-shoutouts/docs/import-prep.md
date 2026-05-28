# Import Prep

Use this workflow to prepare First Chat Shoutouts as a Streamer.bot import without inventing an untested C# action schema.

The repository includes a generic Streamer.bot 1.0.4 C# action fixture and a module-specific First Chat Shoutouts fixture. Normal builds use the module-specific fixture so the reset action keeps Streamer.bot's built-in `Reset First Words` sub-action and stream-start trigger shape.

## 1. Build The Import

From the repository root:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/first-chat-shoutouts \
  build/first-chat-shoutouts.sb
```

The script maps the committed fixture's action shapes onto this module, replaces the C# source code, creates the command record, and writes an experimental `.sb` file.

`FCS - Configure Defaults` keeps the default JSON in `src/config/default-config.json` as the source of truth. The checked-in C# action contains a build-time placeholder; `tools/streamerbot_import/build_module_import.py` reads `module.json`'s `defaultConfig` path and replaces that placeholder with the JSON content before embedding the action in the generated import.

## 2. Import And Inspect

In a disposable Streamer.bot profile:

1. Click `Import`.
2. Load or paste `build/first-chat-shoutouts.sb`.
3. Confirm the import contains the `FCS - ...` actions.
4. Open the imported actions and compile the C# sub-actions.
5. Run `FCS - Configure Defaults` if Streamer.bot does not auto-run it after import.
6. Inspect the triggers and command from the setup docs before live use.

## Failure Modes

- If the script says no C# code block was found, the committed fixture or custom stub does not contain a recognizable Streamer.bot C# action shape.
- If the script says a C# reference is missing, add the required framework DLL path to the module manifest's `references` list before rebuilding.
- If Streamer.bot rejects the prepared `.sb`, decode the stub and prepared files to JSON and compare the action shape.
- If an imported action does not compile, inspect that action's C# source and Streamer.bot's compile output before using the module live.
