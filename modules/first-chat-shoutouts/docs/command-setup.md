# Command Setup

The generated import creates a disabled moderator-only command that runs `FCS - Handle Manual Twitch Shoutout`.

Imported command settings:

```text
Name: First Chat Shoutout
Aliases:
!so
!shoutout
Location: Start
Permissions: Moderators and Broadcaster
Action: FCS - Handle Manual Twitch Shoutout
```

Streamer.bot imports commands disabled by default for safety. After importing into a disposable profile, inspect the command and enable it when ready.

Usage:

```text
!so somecreator
!shoutout @somecreator
```

The manual command can shout out any Twitch login. If the login also appears in `firstChatShoutouts.config`, the module uses that person's `announcementTemplate`; otherwise it uses `defaultAnnouncementTemplate`.

The command action sets:

```text
targetId=twitch_main
shoutoutLogin=<first typed login>
shoutoutSource=manual
```

`FCS - Run Shoutout` still checks that the caller is a moderator or broadcaster when `manual.moderatorOnly` is `true`. Keep the Streamer.bot command permissions restricted too, because that gives immediate feedback and avoids unnecessary action executions.
