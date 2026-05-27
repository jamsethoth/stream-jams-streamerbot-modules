# C# Actions

Source docs:
- https://docs.streamer.bot/api/csharp/guide/intro
- https://docs.streamer.bot/api/csharp/guide/variables
- https://docs.streamer.bot/api/csharp/methods
- https://docs.streamer.bot/api/csharp/methods/twitch/chat/send-message

## Minimum Shape

Every Execute C# Code sub-action needs a `CPHInline` class and an `Execute()` method.

```csharp
using System;

public class CPHInline
{
    public bool Execute()
    {
        CPH.LogInfo("Hello from Streamer.bot C#");
        return true;
    }
}
```

Return `true` to continue subsequent sub-actions. Return `false` to stop the remaining sub-actions intentionally.

## Arguments

Prefer `CPH.TryGetArg<T>()`. Avoid direct `args["name"]` access because missing or mismatched args can crash the code instance.

```csharp
CPH.TryGetArg("userName", out string userName);
CPH.TryGetArg("rawInput", out string rawInput);

userName = string.IsNullOrWhiteSpace(userName) ? "there" : userName;
rawInput = rawInput?.Trim() ?? "";
```

## Globals

```csharp
string value = CPH.GetGlobalVar<string>("my.global", true) ?? "default";
CPH.SetGlobalVar("my.global", value, true);

int count = CPH.GetGlobalVar<int?>("counter.example", true) ?? 0;
CPH.SetGlobalVar("counter.example", count + 1, true);
```

The `persisted` argument defaults to `true`. Pass `false` for temporary non-persisted globals.

## Twitch Chat Reply Pattern

```csharp
using System;

public class CPHInline
{
    public bool Execute()
    {
        CPH.TryGetArg("userName", out string userName);
        userName = string.IsNullOrWhiteSpace(userName) ? "there" : userName;

        CPH.SendMessage($"Hey {userName}, thanks for being here!", true, true);
        return true;
    }
}
```

For YouTube, Kick, Discord, OBS, or other APIs, verify the exact C# method in the live method reference before finalizing.

## Review Checklist

- Include all needed `using` directives.
- Use `TryGetArg` or `TryGetValue`, not unsafe direct indexing.
- Trim and validate command input before using it.
- Add cooldowns or permission checks at the command/trigger level for chat-emitting scripts.
- Log useful internal diagnostics with `CPH.LogInfo`, but do not log secrets.
- Use null-coalescing defaults for globals.
- Tell the user to click Compile or Save and Compile in Streamer.bot after pasting code.
