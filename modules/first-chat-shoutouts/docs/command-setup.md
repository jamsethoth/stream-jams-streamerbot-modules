# Command Setup

The generated import creates disabled moderator-only commands for single and batch shoutouts.

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
```

Streamer.bot imports commands disabled by default for safety. After importing into a disposable profile, inspect the command and enable it when ready.

Usage:

```text
!so somecreator
!shoutout @somecreator
!soall
!shoutoutall
```

The manual command can shout out any Twitch login. If the login also appears in `firstChatShoutouts.config`, the module uses that person's `announcementTemplate`; otherwise it uses `defaultAnnouncementTemplate`.

The shoutout-all command shouts out enabled configured people who have spoken in chat so far this stream, in the order Streamer.bot saw their First Words event. It ignores whether those people were already automatically shouted out. First-chat tracking still runs when `automatic.enabled` is `false`, so shoutout-all continues to work with automatic shoutouts disabled.

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

`FCS - Run Shoutout` still checks that the caller is a moderator or broadcaster when `manual.moderatorOnly` or `manualAll.moderatorOnly` is `true`. Keep the Streamer.bot command permissions restricted too, because that gives immediate feedback and avoids unnecessary action executions.
