# Import Prep

Use this workflow to prepare Activity-Gated Chat Announcements as a Streamer.bot import without inventing an untested C# action schema.

This produces an experimental `.sb` file by cloning a known-good C# action export from your installed Streamer.bot version. Import it into a disposable profile first, inspect the actions, then copy it into your live profile only after they compile and behave correctly.

## 1. Create A Known-Good C# Stub

In Streamer.bot:

1. Create any temporary action, such as `AGA - C# Stub`.
2. Add one `Execute C# Code` sub-action.
3. Paste any tiny compiling `CPHInline` stub into it.
4. Save and compile the action.
5. Export only that action to a file, for example `exports/csharp-stub.sb`.

The stub export gives `tools/streamerbot_import/build_module_import.py` the exact C# action and sub-action schema for your Streamer.bot version.

## 2. Patch The Export

From the repository root:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/activity-gated-chat-announcements \
  exports/csharp-stub.sb \
  build/activity-gated-chat-announcements.sb
```

If you are already inside the `exports/` directory, use this form instead:

```bash
PYTHONPATH=.. python3 -m tools.streamerbot_import.build_module_import \
  ../modules/activity-gated-chat-announcements \
  csharp-stub.sb \
  ../build/activity-gated-chat-announcements.sb
```

The script:

- decodes the known-good `.sb` export
- finds the exported C# code block, including Streamer.bot 1.0.4 `byteCode` fields
- clones that C# action shape into the generated module actions
- includes default configuration, tracker, Twitch/YouTube wrappers, scheduler, and Twitch/YouTube senders
- updates the import metadata to mark it as experimental
- writes a prepared `.sb` file

You can also decode a prepared `.sb` to readable JSON while inspecting the schema:

```bash
python3 -m tools.streamerbot_import.sb_import_string decode \
  build/activity-gated-chat-announcements.sb \
  build/activity-gated-chat-announcements.json
python3 -m tools.streamerbot_import.sb_import_string inspect \
  build/activity-gated-chat-announcements.sb
```

From inside `exports/`, the same inspection commands are:

```bash
PYTHONPATH=.. python3 -m tools.streamerbot_import.sb_import_string decode \
  ../build/activity-gated-chat-announcements.sb \
  ../build/activity-gated-chat-announcements.json
PYTHONPATH=.. python3 -m tools.streamerbot_import.sb_import_string inspect \
  ../build/activity-gated-chat-announcements.sb
```

## 3. Import And Inspect

In a disposable Streamer.bot profile:

1. Click `Import`.
2. Load or paste `build/activity-gated-chat-announcements.sb`.
3. Confirm the import contains the `AGA - ...` actions you expect.
4. Open the imported actions and compile the C# sub-actions.
5. Run `AGA - Configure Defaults` if Streamer.bot does not auto-run it after import.

After that, edit these globals in Streamer.bot if needed:

```text
activityGatedAnnouncements.discordInviteUrl
activityGatedAnnouncements.twitchChannelName
activityGatedAnnouncements.youtubeChannelName
activityGatedAnnouncements.config
```

Most users should only edit `intervalMinutes`, `minChats`, `enabled`, or `targetIds` in `activityGatedAnnouncements.config`.

## Failure Modes

- If the script says no C# code block was found, export a C# stub with one `Execute C# Code` sub-action and try again.
- If the export file accidentally contains the same import string twice, the decoder will use the duplicated payload once. If it contains multiple different import strings, the script will stop and ask for one export at a time.
- If Streamer.bot rejects the prepared `.sb`, decode the stub and prepared files to JSON and compare the action shape.
- If an imported action does not compile, paste that action's source manually into the known-good action and check Streamer.bot's compile output.
