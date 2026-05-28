# Actions, Commands, And Triggers

Source docs:
- https://docs.streamer.bot/guide/core/actions
- https://docs.streamer.bot/guide/core/commands
- https://docs.streamer.bot/guide/core/triggers
- https://docs.streamer.bot/api/sub-actions
- https://docs.streamer.bot/api/triggers

## Mental Model

- Action: named automation containing ordered sub-actions.
- Sub-action: one step inside an action, such as send chat message, set variable, OBS operation, execute C#, delay, logic, file/network action.
- Trigger: event that executes an action, such as command, Twitch event, YouTube event, OBS event, hotkey, timer, file watcher, custom event.
- Command: platform-agnostic chat command entity. It supports Twitch, YouTube, and Trovo.

Sub-actions execute sequentially. If a later sub-action needs a variable from an earlier one, put the producing sub-action first.

## Command Setup Pattern

Use this for commands like `!discord`, `!lurk`, `!clip`, `!quote`, counters, or simple chat tools.

1. Create an Action with a clear name, e.g. `Chat / Discord Command`.
2. Add sub-actions. Common first pass:
   - Twitch/YouTube chat message sub-action for simple replies.
   - Get/Set Global Variable for counters.
   - Execute C# Code for parsing, branching, or external calls.
3. Create a Command:
   - Name: human label.
   - Enabled: on.
   - Include: on if it should appear in generated command lists.
   - Mode: Basic unless regex is genuinely needed.
   - Location: Start for normal prefix commands; Exact for commands without arguments; Anywhere only for deliberate keyword reactions.
   - Commands: one alias per line, e.g. `!discord` and `!disc`.
   - Permissions: set broadcaster/mod/VIP/sub/everyone to match the risk.
   - Cooldowns: add global and/or per-user cooldowns for chat-emitting commands.
4. Add a Command trigger to the Action and select the Command.
5. Test in chat or by manually executing the Action if command args are not required.

## Design Heuristics

- Use separate Commands if aliases need separate cooldowns; aliases on one command share cooldowns.
- Keep "exact" commands simple; use "start" commands when users may supply text after the command.
- Name global variables with a namespace, e.g. `quote.lastId`, `lurk.count`, `social.discordUrl`.
- For cross-platform commands, be explicit which chat send sub-action or C# method targets which platform.
- Prefer UI sub-actions for simple built-in operations; switch to C# when logic spans parsing, multiple branches, custom formatting, or external APIs.

## Testing Checklist

- Confirm trigger fired in Action History.
- Inspect action arguments after execution when variables are missing.
- Check permissions and source filters if a command does not fire.
- Check cooldown state if a command fires once and then stops.
- Check sub-action order if `%variable%` substitutions are blank.
