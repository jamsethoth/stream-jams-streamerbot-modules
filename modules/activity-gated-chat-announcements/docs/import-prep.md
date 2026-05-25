# Import Prep

Use this workflow to prepare Activity-Gated Chat Announcements as a Streamer.bot import without inventing an untested C# action schema.

The repository includes a committed Streamer.bot 1.0.4 C# action fixture. Normal builds use that same fixture in CI, releases, and local development, so you do not need to create `exports/csharp-stub.sb` yourself.

This produces an experimental `.sb` file by cloning the committed C# action fixture and replacing its code and metadata with this module's actions. Import it into a disposable profile first, inspect the actions, then copy it into your live profile only after they compile and behave correctly.

## 1. Build The Import

From the repository root:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/activity-gated-chat-announcements \
  build/activity-gated-chat-announcements.sb
```

The script:

- decodes the committed Streamer.bot C# action fixture
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

## 2. Import And Inspect

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

## Optional Custom Stub

Use a custom exported C# action stub only when intentionally testing a new or incompatible Streamer.bot import schema.

In Streamer.bot:

1. Create any temporary action, such as `AGA - C# Stub`.
2. Add one `Execute C# Code` sub-action.
3. Paste any tiny compiling `CPHInline` class into it.
4. Save and compile the action.
5. Export only that action to a file, for example `exports/csharp-stub.sb`.

Then build with:

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/activity-gated-chat-announcements \
  build/activity-gated-chat-announcements.sb \
  --stub exports/csharp-stub.sb
```

## Failure Modes

- If the script says no C# code block was found, the committed fixture or custom stub does not contain a recognizable Streamer.bot C# action shape.
- If a custom stub file accidentally contains the same import string twice, the decoder will use the duplicated payload once. If it contains multiple different import strings, the script will stop and ask for one export at a time.
- If Streamer.bot rejects the prepared `.sb`, decode the stub and prepared files to JSON and compare the action shape.
- If an imported action does not compile, inspect that action's C# source and Streamer.bot's compile output before using the module live.
