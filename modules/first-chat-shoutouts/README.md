# First Chat Shoutouts

## What It Does

First Chat Shoutouts watches Twitch first-chat events and automatically shouts out a configured list of people once per stream. It also provides moderator command paths: `!so <login>` / `!shoutout <login>` for one Twitch login, `!soall` / `!shoutoutall` for every configured person who has spoken so far this stream in first-entry order, `!soauto on|off` / `!shoutoutauto on|off` to toggle automatic shoutouts, and `!soautoadd <login> [message...]` to add or update automatic shoutout people from chat.

For each shoutout, the module attempts Twitch's native shoutout and always sends a custom Twitch announcement. Announcement text can be customized per configured person and falls back to a default template. Templates can reference the person's latest Twitch game, with a configurable fallback when Twitch has no game available.

The MVP ships with Twitch wiring only. The config keeps target IDs and target platform settings explicit so future targets can be added without changing the automatic/manual invocation contract.

## Installation

1. Download the module artifact or release archive.
2. Extract `first-chat-shoutouts/first-chat-shoutouts.sb`.
3. In Streamer.bot, open `Import`.
4. Load the `.sb` file, or open `first-chat-shoutouts.import.txt` and paste that import text into the import field.
5. Confirm the import contains the `First Chat Shoutouts` actions, then import them.
6. Open the imported actions and compile the C# sub-actions.
7. Run `FCS - Configure Defaults` if Streamer.bot does not auto-run it after import.
8. Confirm the imported Twitch First Words trigger is attached to `FCS - Handle Twitch First Words`.
9. Enable the imported `!so` / `!shoutout`, `!soall` / `!shoutoutall`, `!soauto` / `!shoutoutauto`, `!soautoadd` / `!addsoauto` / `!shoutoutautoadd`, and `!sorecover` / `!shoutoutrecover` commands if you want the manual moderator commands live.
10. Confirm `FCS - Reset Stream State` has the Stream Online trigger and `Reset First Words` sub-action.

Use a disposable Streamer.bot profile first. The generated `.sb` imports actions, the manual command, the Twitch First Words trigger, and the stream-start reset flow, but all imported wiring should still be inspected before live use.

## C# References

The generated C# sub-actions include framework references because the module uses regular expressions and other framework types. The default import references are:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll
```

If Streamer.bot reports a missing metadata/reference file while compiling the imported C# actions, edit each imported C# sub-action's references to point at those framework DLL paths on that machine.

## Configuration

The generated import includes `FCS - Configure Defaults`, which initializes:

```text
firstChatShoutouts.config
firstChatShoutouts.streamSessionId
firstChatShoutouts.streamState
```

Edit `firstChatShoutouts.config` in Streamer.bot's global variable viewer.

Important fields:

```json
{
  "lastGameFallback": "something excellent",
  "defaultAnnouncementTemplate": "Go follow @{login} at https://twitch.tv/{login}! They were last streaming {lastGame}.",
  "automatic": {
    "enabled": true
  },
  "manualAll": {
    "enabled": true
  },
  "autoToggle": {
    "enabled": true
  },
  "autoAdd": {
    "enabled": true
  },
  "streamState": {
    "recoveryEnabled": true,
    "recoveryWindowMinutes": 30,
    "maxArchivedSessions": 3
  },
  "people": [
    {
      "login": "examplecreator",
      "enabled": true,
      "announcementTemplate": "Please show @{login} some love at https://twitch.tv/{login}! They were last streaming {lastGame}."
    }
  ]
}
```

Automatic shoutouts only run for enabled logins in `people` when `automatic.enabled` is `true`. First-chat tracking still happens when automatic shoutouts are disabled, so `!soall` can shout out configured people who have spoken so far. `!soauto on|off` updates `automatic.enabled`; the toggle command itself is controlled separately by `autoToggle.enabled`. `!soautoadd <login> [message...]` upserts a login in `people`, marks it enabled, and stores the optional message as that person's `announcementTemplate`; the add command itself is controlled separately by `autoAdd.enabled`. Manual single-user commands can shout out any Twitch login by default because `manual.allowAnyLogin` is `true`.

`streamState.recoveryEnabled` controls whether a quick second Stream Online event recovers the active shoutout state instead of starting fresh. `streamState.recoveryWindowMinutes` defines the recovery window, and `streamState.maxArchivedSessions` bounds archived sessions after age-based pruning.

Supported template tokens:

```text
{login}
{displayName}
{lastGame}
{channelTitle}
{targetId}
{platform}
```

`{lastGame}` comes from Twitch extended user info. If Twitch returns no game or lookup fails, the module uses `lastGameFallback`.

## Generated Actions

Recommended group:

```text
First Chat Shoutouts
```

The generated import creates these actions:

```text
FCS - Configure Defaults
FCS - Handle Twitch First Words
FCS - Handle Manual Twitch Shoutout
FCS - Handle Manual Twitch Shoutout All
FCS - Handle Auto Shoutout Toggle
FCS - Handle Auto Shoutout Add
FCS - Run Shoutout
FCS - Recover Stream State
FCS - Reset Stream State
```

`FCS - Handle Twitch First Words` reads the Twitch chatter login, records enabled configured people in first-entry order for the stream, sets `targetId=twitch_main`, marks the source as `automatic`, and calls `FCS - Run Shoutout`.

`FCS - Handle Manual Twitch Shoutout` parses the first typed command argument, sets `targetId=twitch_main`, marks the source as `manual`, and calls `FCS - Run Shoutout`.

`FCS - Handle Manual Twitch Shoutout All` reads the stream's entered configured chatter list and calls `FCS - Run Shoutout` for each login in order with `shoutoutSource=manual_all`. It ignores whether the person was already automatically shouted out earlier in the stream.

`FCS - Handle Auto Shoutout Toggle` parses `on` / `off` style command input, updates `automatic.enabled`, and sends a Twitch chat confirmation.

`FCS - Handle Auto Shoutout Add` parses a Twitch login and optional custom message, updates `people`, preserves literal template tokens such as `{lastGame}`, and sends a Twitch chat confirmation.

`FCS - Run Shoutout` reads config, checks eligibility, fetches Twitch user info, attempts the native Twitch shoutout, sends the Twitch announcement, and records the login as handled for the current stream session.

`FCS - Recover Stream State` lets a moderator restore the newest recoverable archived session when recovery is still inside `streamState.recoveryWindowMinutes`.

`FCS - Reset Stream State` resets Streamer.bot's First Words tracking and either recovers the active state during a short outage or creates a new `firstChatShoutouts.streamSessionId`. Run it when a stream starts so automatic first-chat shoutouts can happen once per stream.

## Runtime State

The module writes persisted, namespaced globals:

```text
firstChatShoutouts.streamState
firstChatShoutouts.streamSessionId
```

`firstChatShoutouts.streamState` is the authoritative runtime state global. Shape:

```json
{
  "schemaVersion": 1,
  "activeSessionId": "638854000000000000",
  "activeStartedAtUtc": "2026-06-13T18:00:00.0000000Z",
  "lastUpdatedAtUtc": "2026-06-13T18:31:05.0000000Z",
  "lastRecoveredAtUtc": null,
  "targets": {
    "twitch_main": {
      "enteredOrder": [
        "thenoble1",
        "anothercreator"
      ],
      "logins": {
        "thenoble1": {
          "login": "thenoble1",
          "entered": true,
          "enteredTimeUtc": "2026-06-13T18:30:00.0000000Z",
          "sent": true,
          "sentTimeUtc": "2026-06-13T18:31:05.0000000Z",
          "sentSource": "automatic"
        }
      }
    }
  },
  "archivedSessions": []
}
```

Automatic shoutouts skip logins already marked `sent: true` for the current active session. Manual commands and shoutout-all bypass that skip so moderators can intentionally shout someone out again, but a manual shoutout still marks that login handled for the automatic path.

When a fresh session starts, the previous active state is archived only inside `firstChatShoutouts.streamState`. On each stream-state write, archived sessions are purged if they are outside `streamState.recoveryWindowMinutes`, then trimmed to `streamState.maxArchivedSessions`.

Older globals named like `firstChatShoutouts.entered.<targetId>.<streamSessionId>` and `firstChatShoutouts.sent.<targetId>.<streamSessionId>.<login>` are legacy stale data after this version. The module no longer writes them and does not delete them automatically.
