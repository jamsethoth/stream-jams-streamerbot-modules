# Twitch Reward Trigger Setup

The generated import includes `GWM - Handle Twitch Reward Entry`, but it does not auto-create a Reward Redemption trigger. Attach the trigger manually in Streamer.bot.

In Streamer.bot:

```text
Action: Giveaway Module / GWM - Handle Twitch Reward Entry
Trigger: Twitch > Channel Reward > Reward Redemption
Reward: Any, or the specific reward you want to use for the giveaway
```

The action reads these Reward Redemption variables when Streamer.bot provides them:

```text
rewardId
rewardName
redemptionId
rawInput
counter
userCounter
```

The action also needs Twitch user variables such as `userId` and `userName`. If `userId` is missing, the entry is refused because duplicate prevention depends on Twitch user ID.

Configure reward matching in `giveawayModule.config`:

```json
{
  "rewardEntry": {
    "enabled": true,
    "rewardIds": ["44e86f71-8ace-4739-a123-3ff095489343"],
    "rewardNames": ["Giveaway Entry"],
    "matchAnyWhenUnconfigured": true
  }
}
```

Use reward IDs when possible because names can be changed in Twitch. Reward names are still useful for setup and testing.

If you attach a specific Streamer.bot reward trigger, you can leave `rewardIds` and `rewardNames` empty and keep `matchAnyWhenUnconfigured` as `true`.
