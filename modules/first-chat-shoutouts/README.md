# First Chat Shoutouts

## What It Does

First Chat Shoutouts watches Twitch first-chat events and automatically shouts out a configured list of people once per stream. It also provides a moderator command path, such as `!so <login>` or `!shoutout <login>`, that can shout out any Twitch login without adding that person to the automatic list.

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
9. Enable the imported `!so` / `!shoutout` command if you want the manual moderator command live.
10. Confirm `FCS - Reset Stream State` has the Stream Online trigger and `Reset First Words` sub-action.

Use a disposable Streamer.bot profile first. The generated `.sb` imports actions, the manual command, the Twitch First Words trigger, and the stream-start reset flow, but all imported wiring should still be inspected before live use.

## C# References

The generated C# sub-actions include a `System.Core.dll` reference because the module uses framework types. The default import reference is:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll
```

If Streamer.bot reports a missing metadata/reference file while compiling the imported C# actions, edit each imported C# sub-action's references to point at the `System.Core.dll` path on that machine.

## Configuration

The generated import includes `FCS - Configure Defaults`, which initializes:

```text
firstChatShoutouts.config
firstChatShoutouts.streamSessionId
```

Edit `firstChatShoutouts.config` in Streamer.bot's global variable viewer.

Important fields:

```json
{
  "lastGameFallback": "something excellent",
  "defaultAnnouncementTemplate": "Go follow @{login} at https://twitch.tv/{login}! They were last streaming {lastGame}.",
  "people": [
    {
      "login": "examplecreator",
      "enabled": true,
      "announcementTemplate": "Please show @{login} some love at https://twitch.tv/{login}! They were last streaming {lastGame}."
    }
  ]
}
```

Automatic shoutouts only run for enabled logins in `people`. Manual commands can shout out any Twitch login by default because `manual.allowAnyLogin` is `true`.

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
FCS - Run Shoutout
FCS - Reset Stream State
```

`FCS - Handle Twitch First Words` reads the Twitch chatter login, sets `targetId=twitch_main`, marks the source as `automatic`, and calls `FCS - Run Shoutout`.

`FCS - Handle Manual Twitch Shoutout` parses the first typed command argument, sets `targetId=twitch_main`, marks the source as `manual`, and calls `FCS - Run Shoutout`.

`FCS - Run Shoutout` reads config, checks eligibility, fetches Twitch user info, attempts the native Twitch shoutout, sends the Twitch announcement, and records the login as handled for the current stream session.

`FCS - Reset Stream State` resets Streamer.bot's First Words tracking and creates a new `firstChatShoutouts.streamSessionId`. Run it when a stream starts so automatic first-chat shoutouts can happen once per stream.

## Runtime State

The module writes persisted, namespaced globals:

```text
firstChatShoutouts.sent.<targetId>.<streamSessionId>.<login>
firstChatShoutouts.streamSessionId
```

Automatic shoutouts skip logins already marked for the current session. Manual commands bypass that skip so moderators can intentionally shout someone out again, but a manual shoutout still marks that login handled for the automatic path.
