# Command Setup

The generated import creates disabled commands for entry and management.

Imported command settings:

```text
Name: Giveaway Enter
Alias:
!giveaway enter
Location: Start
Permissions: Everyone
Action: GWM - Handle Command Entry
User cooldown: 5 seconds

Name: Giveaway Clear
Alias:
!giveaway clear
Location: Start
Permissions: Moderators and Broadcaster
Action: GWM - Clear Giveaway

Name: Giveaway Draw
Alias:
!giveaway draw
Location: Start
Permissions: Moderators and Broadcaster
Action: GWM - Draw Giveaway
```

Streamer.bot imports commands disabled by default. Inspect the permissions and cooldowns before enabling them.

Usage:

```text
!giveaway enter
!giveaway draw
!giveaway clear
```

`!giveaway draw` randomly selects one current entrant, removes them from `giveawayModule.state.entries`, appends them to `giveawayModule.state.winners`, persists the updated state, then announces the winner.

`!giveaway clear` empties both `entries` and `winners`, which lets previous winners enter again.
