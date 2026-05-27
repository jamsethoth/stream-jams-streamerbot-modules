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
