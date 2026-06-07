# Command Setup

The generated import creates disabled moderator-only commands for single, batch, automatic-toggle, and automatic-add shoutout paths.

Imported command settings:

```text
Name: First Chat Shoutout
Aliases:
!so
!shoutout
Location: Start
Permissions: Moderators and Broadcaster
Action: FCS - Handle Manual Twitch Shoutout

Name: First Chat Shoutout All
Aliases:
!soall
!shoutoutall
Location: Start
Permissions: Moderators and Broadcaster
Action: FCS - Handle Manual Twitch Shoutout All

Name: First Chat Shoutout Auto Toggle
Aliases:
!soauto
!shoutoutauto
Location: Start
Permissions: Moderators and Broadcaster
Action: FCS - Handle Auto Shoutout Toggle

Name: First Chat Shoutout Auto Add
Aliases:
!soautoadd
!addsoauto
!shoutoutautoadd
Location: Start
Permissions: Moderators and Broadcaster
Action: FCS - Handle Auto Shoutout Add
```

Streamer.bot imports commands disabled by default for safety. After importing into a disposable profile, inspect the command and enable it when ready.

Usage:

```text
!so somecreator
!shoutout @somecreator
!soall
!shoutoutall
!soauto on
!soauto off
!shoutoutauto enable
!shoutoutauto disable
!soautoadd somecreator
!soautoadd @somecreator Go follow @{login}; they last streamed {lastGame}!
!addsoauto somecreator Please show @{displayName} some love at https://twitch.tv/{login}
!shoutoutautoadd somecreator They were last streaming {lastGame}.
```

The manual command can shout out any Twitch login. If the login also appears in `firstChatShoutouts.config`, the module uses that person's `announcementTemplate`; otherwise it uses `defaultAnnouncementTemplate`.

The shoutout-all command shouts out enabled configured people who have spoken in chat so far this stream, in the order Streamer.bot saw their First Words event. It ignores whether those people were already automatically shouted out. First-chat tracking still runs when `automatic.enabled` is `false`, so shoutout-all continues to work with automatic shoutouts disabled.

The auto-toggle command updates `automatic.enabled` in `firstChatShoutouts.config`. It controls automatic first-chat shoutouts only; stream-entry tracking and `!soall` continue to work while automatic shoutouts are off.

The auto-add command updates `people` in `firstChatShoutouts.config`. It treats the first typed argument as the Twitch login and the remaining text as an optional `announcementTemplate`. If the login already exists, it is set back to `enabled: true`; a provided custom message replaces that person's previous template, while omitting the custom message preserves the existing template or falls back to `defaultAnnouncementTemplate`.

Custom auto-add messages support the same tokens as other shoutout templates:

```text
{login}
{displayName}
{lastGame}
{channelTitle}
{targetId}
{platform}
```

Use `{lastGame}` when you want the announcement to mention the user's last streamed Twitch game.

The command action sets:

```text
targetId=twitch_main
shoutoutLogin=<first typed login>
shoutoutSource=manual
```

The shoutout-all command action sets:

```text
targetId=twitch_main
shoutoutSource=manual_all
```

The auto-toggle command accepts:

```text
on, enable, enabled, true, 1
off, disable, disabled, false, 0
```

The auto-add command accepts:

```text
<login> [custom announcement template...]
```

`FCS - Run Shoutout` still checks that the caller is a moderator or broadcaster when `manual.moderatorOnly` or `manualAll.moderatorOnly` is `true`; the toggle action checks `autoToggle.moderatorOnly`, and the add action checks `autoAdd.moderatorOnly`. Keep the Streamer.bot command permissions restricted too, because that gives immediate feedback and avoids unnecessary action executions.
