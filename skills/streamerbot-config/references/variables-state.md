# Variables And State

Source docs:
- https://docs.streamer.bot/guide/core/variables
- https://docs.streamer.bot/api/csharp/guide/variables
- https://docs.streamer.bot/api/sub-actions/core/globals

## Argument Stack

Triggers create variables for the current action. Sub-actions can add more variables. These are local arguments and disappear after the action finishes.

Use `%variableName%` in most sub-action text fields, for example `%userName%` or `%rawInput%`.

If a variable is blank:

1. Confirm the trigger actually supplies it.
2. Confirm a previous sub-action created it.
3. Confirm the producing sub-action runs before the consuming sub-action.
4. Inspect Action History arguments.

Common generic arguments include `%date%`, `%time%`, `%longtime%`, `%unixtime%`, `%actionId%`, `%actionName%`, `%runningActionId%`, `%eventSource%`, and `%__source%`.

## Globals

Use global variables to share state between actions or persist it across Streamer.bot restarts.

- Set Global Variable stores a value but does not automatically add it to the current argument stack.
- Get Global Variable loads a global into a local argument for the current action.
- Persisted globals can be referenced inline with `~globalName~`.
- User global variables store per-user state and are useful for per-user counters, opt-ins, cooldown-like flags, and lightweight leaderboards.

Use namespaced globals:

```text
social.discordUrl
counter.deaths.total
user.points
quote.lastId
```

## Inline Formatting And Functions

- Numeric/date formatting can use C#-style format strings, e.g. `%tipAmount:c2%` or `%time:t%`.
- `$math(...)$` evaluates simple math.
- `$length(...)$` returns text length.
- `$parse(...)$` can "double parse" a computed variable name, useful in loops.

## C# State Pattern

Use this pattern for persisted counters:

```csharp
int count = CPH.GetGlobalVar<int?>("counter.example.total", true) ?? 0;
count++;
CPH.SetGlobalVar("counter.example.total", count, true);
```

For complex objects, prefer simple serializable types first. If needed, Streamer.bot includes Newtonsoft.Json for manual JSON serialization.
