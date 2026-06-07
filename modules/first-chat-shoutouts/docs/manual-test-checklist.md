# Manual Test Checklist

Use a disposable Streamer.bot profile and a low-risk Twitch channel before live use.

## Import And Compile

- Import `first-chat-shoutouts.sb`.
- Confirm the `First Chat Shoutouts` action group exists.
- Compile every imported C# sub-action.
- Run `FCS - Configure Defaults`.
- Confirm `firstChatShoutouts.config` and `firstChatShoutouts.streamSessionId` exist.

## Automatic Path

- Add a test login to `people` and trigger Twitch First Words for that configured automatic user.
- Confirm Streamer.bot attempts the native shoutout.
- Confirm a Twitch announcement is sent.
- Trigger Twitch First Words for the same configured automatic user again in the same session and confirm it is skipped.
- Trigger Twitch First Words for an unconfigured automatic user and confirm it is skipped.

## Manual Command Path

- Create moderator-only `!so` and `!shoutout` commands for `FCS - Handle Manual Twitch Shoutout`.
- Run `!so somecreator` as a mod and confirm the manual command shouts out that Twitch login even when it is not in `people`.
- Run the command as a non-mod if possible and confirm it is denied when `manual.moderatorOnly` is `true`.

## Manual All Command Path

- Create moderator-only `!soall` and `!shoutoutall` commands for `FCS - Handle Manual Twitch Shoutout All`.
- Trigger Twitch First Words for two enabled configured people and confirm `firstChatShoutouts.entered.twitch_main.<session>` stores them in first-entry order.
- Run `!soall` as a mod and confirm each entered configured person is shouted out in order, even if one was already automatically shouted out earlier in the stream.
- Set `automatic.enabled` to `false`, trigger Twitch First Words for another configured person, and confirm `!soall` still includes that person.
- Run the command as a non-mod if possible and confirm it is denied when `manualAll.moderatorOnly` is `true`.

## Auto Toggle Command Path

- Create moderator-only `!soauto` and `!shoutoutauto` commands for `FCS - Handle Auto Shoutout Toggle`.
- Run `!soauto off` as a mod and confirm `automatic.enabled` becomes `false`.
- Trigger Twitch First Words for a configured person and confirm they are tracked for `!soall` but not automatically shouted out.
- Run `!soauto on` as a mod and confirm `automatic.enabled` becomes `true`.
- Run the command as a non-mod if possible and confirm it is denied when `autoToggle.moderatorOnly` is `true`.

## Auto Add Command Path

- Create moderator-only `!soautoadd`, `!addsoauto`, and `!shoutoutautoadd` commands for `FCS - Handle Auto Shoutout Add`.
- Run `!soautoadd somecreator` as a mod and confirm `people` includes `somecreator` with `enabled: true`.
- Run `!soautoadd @somecreator Go follow @{login}; they last streamed {lastGame}!` as a mod and confirm the auto add custom message is saved as `announcementTemplate`.
- Trigger Twitch First Words for that login and confirm the saved custom message is used for the automatic shoutout.
- Run the command as a non-mod if possible and confirm it is denied when `autoAdd.moderatorOnly` is `true`.

## Templates And Fallbacks

- Configure a per-person template and confirm the per-person template overrides the default.
- Remove or blank the person's latest Twitch game in a test scenario if possible, or temporarily use a login with no available game, and confirm the last game fallback appears.
- Confirm `{login}`, `{displayName}`, `{lastGame}`, `{channelTitle}`, `{targetId}`, and `{platform}` resolve without leaving raw tokens in chat.

## Twitch Behavior

- Force or wait for native shoutout cooldown conditions and confirm the native shoutout cooldown does not prevent the Twitch announcement from being sent.
- Confirm the module logs native shoutout failures as warnings, not fatal errors.

## Stream Reset

- Run `FCS - Reset Stream State`.
- Run Streamer.bot's built-in `Reset First Words` sub-action.
- Trigger first words for a configured automatic user and confirm the stream reset allows a fresh automatic shoutout.
