# Manual Test Checklist

Use a disposable Streamer.bot profile before live use.

## Import And Defaults

- Import `giveaway-module.sb`.
- Compile all `GWM - ...` C# actions.
- Run `GWM - Configure Defaults`.
- Confirm `giveawayModule.config` exists.
- Confirm `giveawayModule.state` exists with empty `entries` and `winners` arrays.

## Command Entry

- Enable `!giveaway enter`.
- Send `!giveaway enter` as a normal Twitch viewer and confirm the viewer is added to `giveawayModule.state.entries`.
- Send `!giveaway enter` again as the same viewer and confirm no second entry is added.
- Confirm chat says the viewer is already entered.

## Reward Entry

- Attach Twitch > Channel Reward > Reward Redemption to `GWM - Handle Twitch Reward Entry`.
- Configure `giveawayModule.config.rewardEntry.rewardIds` or `rewardNames` for the test reward.
- Redeem the matching reward and confirm the viewer is added with `source` set to `reward`.
- Redeem a non-matching reward and confirm `giveawayModule.state` does not change.
- Trigger the reward without a Twitch user ID in Action History or manual testing and confirm no entry is added.

## Management

- Enable `!giveaway draw` and `!giveaway clear` for moderators.
- Run `!giveaway draw` as a moderator with at least one entrant.
- Confirm the selected viewer moves from `entries` to `winners`.
- Confirm the winner announcement is sent after state persistence.
- Try `!giveaway enter` as the winner and confirm they cannot re-enter.
- Run `!giveaway draw` with no entries and confirm chat says there are no entries to draw.
- Run `!giveaway clear` as a moderator and confirm both `entries` and `winners` are empty.
- Confirm the previous winner can enter again after clear.

## Permission Checks

- Try `!giveaway draw` as a non-moderator and confirm state does not change.
- Try `!giveaway clear` as a non-moderator and confirm state does not change.
