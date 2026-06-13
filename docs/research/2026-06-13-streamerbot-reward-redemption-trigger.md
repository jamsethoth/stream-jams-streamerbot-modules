# Streamer.bot Reward Redemption Trigger Notes

Research date: 2026-06-13

## Sources Checked

- Streamer.bot Twitch trigger index: https://docs.streamer.bot/api/triggers/twitch
- Streamer.bot Twitch Channel Reward Redemption trigger: https://docs.streamer.bot/api/triggers/twitch/channel-reward/reward-redemption

## Official Docs Findings

The Streamer.bot Twitch trigger index lists three Channel Reward triggers: Automatic Reward Redemption, Reward Redemption, and Reward Redemption Updated. The Reward Redemption page describes the trigger as "Trigger for a Twitch Reward Redemption" and exposes a required Reward parameter whose default is Any.

The Reward Redemption trigger variables used by Giveaway Module are:

```text
%redemptionId%
%rewardId%
%rewardName%
%rawInput%
%counter%
%userCounter%
```

The same page also includes Twitch User Variables, which provide the viewer identity context used by the module through Streamer.bot action arguments such as `userId`, `userName`, and login-like fields.

## Import Builder Decision

The official docs confirm the runtime trigger and variables, but they do not document the decoded `.sb` import trigger record type or field shape. This repository's import builder only emits trigger records backed by observed same-version exports.

For the first Giveaway Module implementation:

- The generated import includes `GWM - Handle Twitch Reward Entry`.
- Operators manually attach Streamer.bot's Twitch > Channel Reward > Reward Redemption trigger to that action.
- The action filters by `giveawayModule.config.rewardEntry.rewardIds` and `rewardNames`.
- A future fixture-backed change can add generated reward trigger wiring after a current Streamer.bot export containing that trigger is available.
