# Import Preparation

The repository build uses the committed Streamer.bot 1.0.4 C# action fixture to generate `giveaway-module.sb`.

```bash
python3 -m tools.streamerbot_import.build_module_import \
  modules/giveaway-module \
  build/giveaway-module.sb
```

The generated import includes actions and command wiring. It does not generate the Twitch Reward Redemption event trigger because this repository does not yet have a same-version `.sb` fixture with that trigger record shape.

After import into a disposable Streamer.bot profile:

1. Compile all `GWM - ...` C# actions.
2. Run `GWM - Configure Defaults`.
3. Enable the commands you want to expose.
4. Attach the Twitch Reward Redemption trigger manually if you want channel point entries.
5. Run the manual checklist before live use.

Generated imports remain experimental until they are imported and compiled in Streamer.bot.
