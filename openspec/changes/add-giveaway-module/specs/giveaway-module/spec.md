## ADDED Requirements

### Requirement: Giveaway State Persistence
The module SHALL store the active giveaway's entrants and winners in one persisted Streamer.bot global variable named `giveawayModule.state`.

#### Scenario: State survives restart
- **WHEN** a viewer has entered the giveaway and Streamer.bot restarts
- **THEN** the viewer remains present in `giveawayModule.state.entries` after the module is loaded again

#### Scenario: State remains inspectable
- **WHEN** an operator views the persisted global value
- **THEN** the state is JSON containing `entries` and `winners` arrays with Twitch user IDs and display names

### Requirement: Viewer Command Entry
The module SHALL allow Twitch viewers to enter the active giveaway with `!giveaway enter`.

#### Scenario: First command entry succeeds
- **WHEN** a Twitch viewer sends `!giveaway enter` and their Twitch user ID is not in the entrant or winner list
- **THEN** the module adds one entry for that Twitch user ID to `giveawayModule.state.entries`
- **AND** the module sends a chat acknowledgement using the viewer's display name

#### Scenario: Duplicate command entry is rejected
- **WHEN** a Twitch viewer sends `!giveaway enter` and their Twitch user ID is already in `giveawayModule.state.entries`
- **THEN** the module does not add another entry
- **AND** the module sends a chat acknowledgement saying the viewer is already entered

### Requirement: Twitch Reward Entry
The module SHALL allow Twitch viewers to enter the active giveaway from a configurable Twitch channel point reward redemption event.

#### Scenario: Matching reward entry succeeds
- **WHEN** a Twitch reward redemption matches the configured giveaway reward ID or reward name and the viewer is not already entered or already a winner
- **THEN** the module adds one entry for that Twitch user ID to `giveawayModule.state.entries`
- **AND** the module records the entry source as `reward`
- **AND** the module sends a chat acknowledgement using the viewer's display name

#### Scenario: Non-matching reward is ignored
- **WHEN** a Twitch reward redemption does not match the configured giveaway reward ID or reward name
- **THEN** the module does not change `giveawayModule.state`
- **AND** the module does not send a giveaway entry acknowledgement

### Requirement: Twitch Identity De-Duplication
The module SHALL use Twitch user ID as the unique giveaway identity and SHALL use display names only for operator readability and chat responses.

#### Scenario: Display name change does not create duplicate
- **WHEN** the same Twitch user ID enters with a different display name casing or spelling
- **THEN** the module treats the attempt as the same viewer
- **AND** no second entry is added

#### Scenario: Missing Twitch user ID is refused
- **WHEN** an entry trigger does not provide a Twitch user ID
- **THEN** the module does not add an entry
- **AND** the module logs a warning for diagnosis

### Requirement: Winner Lockout
The module SHALL prevent winners from re-entering the active giveaway until the giveaway is cleared.

#### Scenario: Previous winner attempts entry
- **WHEN** a Twitch viewer whose user ID is in `giveawayModule.state.winners` attempts to enter again
- **THEN** the module does not add the viewer to `giveawayModule.state.entries`
- **AND** the module sends a chat acknowledgement saying the viewer already won and can enter again after the giveaway is cleared

### Requirement: Moderator Clear Command
The module SHALL allow only the broadcaster or moderators to clear the active giveaway with `!giveaway clear`.

#### Scenario: Moderator clears giveaway
- **WHEN** a moderator sends `!giveaway clear`
- **THEN** the module empties both `giveawayModule.state.entries` and `giveawayModule.state.winners`
- **AND** the module persists the cleared state
- **AND** the module announces that the giveaway has been cleared

#### Scenario: Non-moderator clear is rejected
- **WHEN** a viewer who is not the broadcaster or a moderator invokes the clear action
- **THEN** the module does not change `giveawayModule.state`

### Requirement: Moderator Draw Command
The module SHALL allow only the broadcaster or moderators to draw a winner with `!giveaway draw`.

#### Scenario: Moderator draws winner
- **WHEN** a moderator sends `!giveaway draw` and at least one entrant exists
- **THEN** the module randomly selects one entrant
- **AND** the module removes the selected entrant from `giveawayModule.state.entries`
- **AND** the module adds the selected entrant to `giveawayModule.state.winners`
- **AND** the module persists the updated state before announcing the winner in chat

#### Scenario: Draw with no entries
- **WHEN** a moderator sends `!giveaway draw` and no entrants exist
- **THEN** the module does not change `giveawayModule.state`
- **AND** the module sends a chat message saying there are no giveaway entries to draw

#### Scenario: Non-moderator draw is rejected
- **WHEN** a viewer who is not the broadcaster or a moderator invokes the draw action
- **THEN** the module does not change `giveawayModule.state`

### Requirement: Packaged Streamer.bot Module
The giveaway module SHALL be packaged consistently with existing Stream Jams Streamer.bot modules.

#### Scenario: Module release includes giveaway artifacts
- **WHEN** repository module artifacts are built
- **THEN** the output includes a `giveaway-module` artifact directory containing a `.sb` import, `.import.txt`, README, module manifest, and generated artifact manifest

#### Scenario: Module documentation covers setup
- **WHEN** an operator reads the giveaway module documentation
- **THEN** the documentation describes installation, configuration, generated actions, command setup, Twitch reward setup, persisted globals, and manual test steps
