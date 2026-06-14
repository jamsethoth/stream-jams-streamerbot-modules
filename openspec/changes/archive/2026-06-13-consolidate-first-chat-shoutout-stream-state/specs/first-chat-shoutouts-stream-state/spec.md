## ADDED Requirements

### Requirement: Consolidated Stream State Persistence
First Chat Shoutouts SHALL store active stream runtime state in one persisted Streamer.bot global variable named `firstChatShoutouts.streamState`.

#### Scenario: Defaults initialize missing state
- **WHEN** `FCS - Configure Defaults` runs and `firstChatShoutouts.streamState` is missing or blank
- **THEN** the module writes a valid JSON state object to `firstChatShoutouts.streamState` with `schemaVersion`, `activeSessionId`, `activeStartedAtUtc`, `lastUpdatedAtUtc`, `targets`, and `archivedSessions`
- **AND** the write uses `CPH.SetGlobalVar("firstChatShoutouts.streamState", value, true)`

#### Scenario: Defaults preserve existing state
- **WHEN** `FCS - Configure Defaults` runs and `firstChatShoutouts.streamState` already contains non-blank JSON
- **THEN** the module does not overwrite the existing value

#### Scenario: State remains human-inspectable
- **WHEN** an operator views `firstChatShoutouts.streamState` in Streamer.bot's global variable viewer
- **THEN** the value is JSON with target IDs under `targets`
- **AND** each target has an `enteredOrder` array and a `logins` object keyed by normalized Twitch login

### Requirement: Target Login State Shape
For each target/login in active stream state, First Chat Shoutouts SHALL store entered and sent flags plus UTC timestamps in the target's `logins` object.

#### Scenario: Login record contains entered and sent fields
- **WHEN** a login record is created or updated for target `twitch_main`
- **THEN** `firstChatShoutouts.streamState.targets.twitch_main.logins.<login>` contains `login`, `entered`, `enteredTimeUtc`, `sent`, `sentTimeUtc`, and `sentSource`

#### Scenario: UTC timestamps are recorded
- **WHEN** the module records an entry or a sent shoutout
- **THEN** the corresponding `enteredTimeUtc` or `sentTimeUtc` value is an ISO 8601 UTC timestamp string

### Requirement: First Words Entry Tracking
First Chat Shoutouts SHALL record enabled configured Twitch First Words chatters in `firstChatShoutouts.streamState` while preserving first-entry order per target.

#### Scenario: Configured chatter enters active state
- **WHEN** Twitch First Words fires for an enabled configured login on enabled target `twitch_main`
- **THEN** the module creates or updates `targets.twitch_main.logins.<login>` with `entered: true`
- **AND** the module sets `enteredTimeUtc` if the login was not already entered
- **AND** the module appends the login once to `targets.twitch_main.enteredOrder`
- **AND** the module persists the updated state before running `FCS - Run Shoutout`

#### Scenario: Duplicate First Words does not duplicate order
- **WHEN** Twitch First Words is processed for a login already present in `targets.twitch_main.enteredOrder`
- **THEN** the login appears only once in `enteredOrder`

#### Scenario: Tracking continues while automatic shoutouts are disabled
- **WHEN** `automatic.enabled` is `false` and Twitch First Words fires for an enabled configured login
- **THEN** the module still records the login as entered in `firstChatShoutouts.streamState`

### Requirement: Shoutout Sent Tracking
First Chat Shoutouts SHALL mark successful announcement attempts in `firstChatShoutouts.streamState` instead of writing per-login sent globals.

#### Scenario: Automatic shoutout is marked sent
- **WHEN** `FCS - Run Shoutout` processes source `automatic` and `CPH.TwitchAnnounce` succeeds
- **THEN** the module sets `targets.<targetId>.logins.<login>.sent` to `true`
- **AND** the module sets `sentTimeUtc` to the current UTC timestamp
- **AND** the module sets `sentSource` to `automatic`
- **AND** the module persists the updated state

#### Scenario: Manual shoutout suppresses later automatic shoutout
- **WHEN** `FCS - Run Shoutout` processes source `manual` for a valid login and the announcement succeeds
- **THEN** the module creates or updates the login record with `sent: true`
- **AND** a later source `automatic` invocation for the same target/login in the same active session is skipped as already handled

#### Scenario: Failed announcement does not mark sent
- **WHEN** `FCS - Run Shoutout` attempts a shoutout but the Twitch announcement path fails
- **THEN** the module does not set `sent: true` for that target/login

### Requirement: Automatic Duplicate Suppression
First Chat Shoutouts SHALL use `firstChatShoutouts.streamState` to skip automatic shoutouts already handled in the active stream session.

#### Scenario: Already sent automatic login is skipped
- **WHEN** source `automatic` is invoked for a target/login whose active state record has `sent: true`
- **THEN** the module does not attempt the native shoutout
- **AND** the module does not send the Twitch announcement

#### Scenario: New automatic login can shout out
- **WHEN** source `automatic` is invoked for an enabled configured target/login whose active state record is missing or has `sent: false`
- **THEN** the module continues through normal eligibility, native shoutout, and announcement processing

### Requirement: Shoutout All Uses Consolidated Entry Order
First Chat Shoutouts SHALL use `firstChatShoutouts.streamState.targets.<targetId>.enteredOrder` as the source of truth for `!soall` processing.

#### Scenario: Shoutout all follows first-entry order
- **WHEN** a moderator invokes `!soall`
- **THEN** `FCS - Handle Manual Twitch Shoutout All` reads the active target's `enteredOrder`
- **AND** it invokes `FCS - Run Shoutout` for enabled configured logins in the order stored there

#### Scenario: Empty entry order is handled
- **WHEN** a moderator invokes `!soall` and the active target has no entered logins
- **THEN** the module logs that no configured chatters were found
- **AND** the module does not mutate `firstChatShoutouts.streamState`

### Requirement: Stream Start Recovery
First Chat Shoutouts SHALL support recovery from a short stream outage without losing active entered and sent state.

#### Scenario: Recoverable stream online preserves active state
- **WHEN** `FCS - Reset Stream State` runs from a Stream Online trigger and `streamState.recoveryEnabled` is `true`
- **AND** the existing active state has `lastUpdatedAtUtc` or `activeStartedAtUtc` within `streamState.recoveryWindowMinutes`
- **THEN** the module keeps the existing active `targets` state
- **AND** it keeps the same `activeSessionId`
- **AND** it updates `lastRecoveredAtUtc`
- **AND** it persists `firstChatShoutouts.streamState`

#### Scenario: Non-recoverable stream online starts fresh
- **WHEN** `FCS - Reset Stream State` runs and the existing active state is outside the configured recovery window
- **THEN** the module archives the previous active session in `archivedSessions`
- **AND** it creates a new active session with empty target state
- **AND** it persists `firstChatShoutouts.streamState`
- **AND** it writes `firstChatShoutouts.streamSessionId` to match the new `activeSessionId`

#### Scenario: Archived sessions are pruned by age and count
- **WHEN** the module writes `firstChatShoutouts.streamState`
- **THEN** it removes archived sessions outside `streamState.recoveryWindowMinutes`
- **AND** it keeps only the newest archived sessions up to `streamState.maxArchivedSessions`

#### Scenario: Recovery disabled purges recoverable archives
- **WHEN** the module writes `firstChatShoutouts.streamState` and recovery is disabled by config
- **THEN** archived sessions are not kept as recoverable state

### Requirement: Manual State Recovery
First Chat Shoutouts SHALL provide a moderator-only recovery path for restoring the newest recoverable archived session.

#### Scenario: Moderator recovers archived state
- **WHEN** a moderator invokes the recovery action and the newest archived session is inside the configured recovery window
- **THEN** the module restores that archived session as the active session
- **AND** it updates `lastRecoveredAtUtc`
- **AND** it writes `firstChatShoutouts.streamSessionId` to match the restored `activeSessionId`
- **AND** it persists `firstChatShoutouts.streamState`

#### Scenario: Non-moderator recovery is rejected
- **WHEN** a viewer who is not the broadcaster or a moderator invokes the recovery action
- **THEN** the module does not change `firstChatShoutouts.streamState`

#### Scenario: No recoverable archive is reported
- **WHEN** a moderator invokes the recovery action and no archived session is inside the configured recovery window
- **THEN** the module does not change `firstChatShoutouts.streamState`
- **AND** the module sends or logs a clear no-recoverable-session result

### Requirement: Legacy Runtime Globals Are No Longer Written
First Chat Shoutouts SHALL stop writing legacy runtime state to `firstChatShoutouts.entered.*` and `firstChatShoutouts.sent.*` globals.

#### Scenario: First Words does not write entered global
- **WHEN** Twitch First Words is processed
- **THEN** the module does not call `CPH.SetGlobalVar` with a name beginning `firstChatShoutouts.entered.`

#### Scenario: Shoutout does not write sent global
- **WHEN** `FCS - Run Shoutout` marks a login as handled
- **THEN** the module does not call `CPH.SetGlobalVar` with a name beginning `firstChatShoutouts.sent.`

### Requirement: Crash-Safe State Mutation
First Chat Shoutouts SHALL persist every state mutation immediately and fail closed on malformed state JSON.

#### Scenario: Mutation is persisted immediately
- **WHEN** the module records entered state, sent state, recovery state, or a fresh stream session
- **THEN** the module writes the full updated JSON state back to `firstChatShoutouts.streamState` with persistence enabled before returning from the action

#### Scenario: Malformed state is not overwritten by normal actions
- **WHEN** a normal runtime action cannot parse `firstChatShoutouts.streamState`
- **THEN** the action logs the parse failure
- **AND** it does not overwrite the malformed state with blank JSON

### Requirement: Documentation And Tests Cover Stream State
The First Chat Shoutouts documentation and artifact tests SHALL describe and verify the consolidated stream state model.

#### Scenario: Documentation identifies authoritative state global
- **WHEN** an operator reads the First Chat Shoutouts README or state docs
- **THEN** the docs identify `firstChatShoutouts.streamState` as the authoritative runtime state global
- **AND** the docs explain that old `firstChatShoutouts.entered.*` and `firstChatShoutouts.sent.*` globals are legacy stale data after this change

#### Scenario: Tests reject legacy state writes
- **WHEN** the module artifact tests inspect First Chat Shoutouts C# action sources
- **THEN** they assert the presence of `firstChatShoutouts.streamState`
- **AND** they assert no action source writes legacy `firstChatShoutouts.entered.*` or `firstChatShoutouts.sent.*` state

#### Scenario: Recovery test checklist exists
- **WHEN** an operator reads the manual test checklist
- **THEN** the checklist includes a stream outage recovery case and a fresh stream reset case
