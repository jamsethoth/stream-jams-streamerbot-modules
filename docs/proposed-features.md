# Proposed Streamer.bot Extension Features

Date: 2026-05-28

This document collects proposed Streamer.bot module ideas derived from common Streamlabs, StreamElements, Nightbot, and Fossabot chatbot features. It is a requirements document, not an implementation plan.

The goal is to avoid proposing modules that Streamer.bot already provides as native, ready-made features. Streamer.bot is action-first, so many ideas can be built manually with actions, triggers, variables, and C#; this document only treats a feature as "native" when the official docs show a user-facing built-in feature or complete packaged behavior for that use case.

## Research Sources

Native Streamer.bot coverage was checked against these official docs:

- [Core features](https://docs.streamer.bot/guide/core): lists native Actions, Triggers, Variables, Commands, Import & Export, Credits, Quotes, and Timers.
- [Actions](https://docs.streamer.bot/guide/core/actions): describes actions, sub-actions, action queues, and the argument stack.
- [Commands](https://docs.streamer.bot/guide/commands): documents platform-agnostic chat commands, aliases, matching modes, cooldowns, permissions, and counters.
- [Variables](https://docs.streamer.bot/guide/variables): documents arguments, global variables, user globals, persistence, and the global variable viewer.
- [Timers](https://docs.streamer.bot/guide/core/timers): documents enabled/repeat timers, interval, random interval, and line-count requirements, including the documented limitation that line counts do not work with multiple connected streaming platforms.
- [Twitch sub-actions](https://docs.streamer.bot/api/sub-actions/twitch): lists native Twitch chat, moderation, polls, predictions, rewards, user groups, and shoutout controls.
- [Twitch moderation triggers](https://docs.streamer.bot/api/triggers/twitch/moderation): lists native Twitch moderation and Automod-related trigger events.
- [YouTube moderation sub-actions](https://docs.streamer.bot/api/sub-actions/youtube/moderation): lists native YouTube ban and timeout actions.
- [Kick moderation sub-actions](https://docs.streamer.bot/api/sub-actions/kick/moderation): lists native Kick ban, timeout, unban, and untimeout actions.
- [Streamer.bot quotes](https://docs.streamer.bot/guide/settings/quotes): documents the built-in quote system and built-in quote commands.
- [Simple counter example](https://docs.streamer.bot/get-started/examples/counter): documents command/channel-point counters and a global-variable counter pattern.
- [Credits](https://docs.streamer.bot/guide/core/credits): documents built-in end-stream credits, which are not a loyalty currency.
- [Add Random Users](https://docs.streamer.bot/api/sub-actions/twitch/user/add-random-users): documents a primitive for selecting random users, but not a complete raffle/giveaway system.
- [Twitch Pyramid Success](https://docs.streamer.bot/api/triggers/twitch/pyramid/success): documents the native Twitch pyramid success trigger.

External feature references behind the original idea list:

- [Streamlabs Cloudbot](https://streamlabs.com/cloudbot), [Streamlabs custom commands](https://support.streamlabs.com/hc/en-us/articles/44209470975515-Cloudbot-101-Custom-Commands-and-Variables-Part-One), and [Streamlabs Cloudbot mod tools](https://support.streamlabs.com/hc/en-us/articles/17170178743707-Mod-Tools-Cloudbot-101).
- [StreamElements Chatbot Overview](https://support.streamelements.com/hc/en-us/articles/10474423416722-Chatbot-Overview), [StreamElements timers](https://docs.streamelements.com/chatbot/timers), and [StreamElements spam filters](https://support.streamelements.com/hc/en-us/articles/18750955133458-Chatbot-Spam-Filters).
- [Nightbot commands](https://docs.nightbot.tv/control-panel/commands), [Nightbot timers](https://docs.nightbot.tv/control-panel/timers), [Nightbot spam protection](https://docs.nightbot.tv/control-panel/spam-protection), and [Nightbot giveaways](https://docs.nightbot.tv/control-panel/giveaways).
- [Fossabot features](https://fossabot.com/), [Fossabot keyword rules](https://docs.fossabot.com/keywords/), [Fossabot lookalikes](https://docs.fossabot.com/lookalikes/), and [Fossabot nukes](https://docs.fossabot.com/nukes/).

## Native Coverage Screening

| Original idea | Native Streamer.bot coverage | Decision |
| --- | --- | --- |
| Moderation Policy Engine | Streamer.bot has moderation primitives, including Twitch ban, timeout, warn, shield, chat modes, delete/clear chat actions, YouTube moderation actions, Kick moderation actions, and Twitch moderation/Automod triggers. The official docs do not show a native spam-filter policy engine with caps, links, symbols, repetition, phrase groups, role bypass, permit windows, escalation, and audit rules. | Include |
| Anti-Evasion Blocklist Plus | Streamer.bot exposes message triggers and moderation actions, but the official docs do not show a native lookalike/confusable-character normalization layer, obfuscated-link detector, or evasion-aware blocked-term system. | Include |
| Command Hub With Variables, Aliases, Cooldowns | Streamer.bot natively supports platform-agnostic commands, multiple trigger strings/aliases, Basic and Regex matching, Start/Exact/Anywhere locations, cooldowns, permissions, persistent counters, user counters, command groups, and command listing. | Exclude as natively covered |
| Conditional Timer Orchestrator | Streamer.bot natively supports timers with enabled/repeat state, fixed or random intervals, and optional chat-line requirements. It does not appear to provide a native multi-message rotator with per-platform targets, live/offline rules, category/title conditions, activity gates that work across multiple streaming platforms, or timer-driven action campaigns. | Include |
| Viewer Queue Manager | Streamer.bot has action queues, but those are execution queues for actions. The official docs do not show a native viewer-facing request queue with join/leave/next/pick/moderation behavior. | Include |
| Raffle And Giveaway Toolkit | Streamer.bot has present viewers and an Add Random Users sub-action, plus polls. The official docs do not show a native giveaway/raffle workflow with entrant collection, eligibility rules, winner history, rerolls, keyword entry, or bonus luck. | Include |
| Media Request Moderation Queue | Streamer.bot can control OBS media sources and run actions from commands/rewards, but the official docs do not show a native viewer media/song request queue with URL validation, approval, queue state, skip/veto, or moderation workflow. | Include |
| Loyalty Points Lite | Streamer.bot has Twitch Channel Point reward controls, command/reward counters, user globals, and built-in end-stream Credits. The Credits feature is for stream event credits, not a bot-managed loyalty currency, store, ledger, or point economy. | Include |
| Quotes, Counters, And Stream Memory | Streamer.bot has a native quote system with built-in quote commands, quote IDs, timestamps, platform, category, and permissions. Streamer.bot also documents command/channel-point counters and global-variable counters. | Exclude as natively covered |
| Emote Combo And Pyramid Games | Streamer.bot has native Twitch pyramid success/broken triggers and emote-related events. The official docs do not show a full emote game pack with combo scoring, bingo prompts, cross-platform emote streaks, cooldowns, rewards, or leaderboards. | Include |

## Proposed Features

The following ideas survived the native coverage screen.

### 1. Moderation Policy Engine

#### Summary

A packaged chat moderation system that lets a streamer define moderation rules in one place and apply them consistently across supported chat platforms. The feature should behave more like Streamlabs, StreamElements, Nightbot, or Fossabot moderation than a collection of disconnected Streamer.bot actions.

#### Feature Requirements

- Support common rule types: caps, symbols, repeated characters, repeated messages, emote spam, long messages, links, banned words, banned phrases, regular expressions, and username patterns.
- Support rule severity levels, such as log only, delete, warn, timeout, ban, and custom action.
- Support configurable punishments per rule, including timeout duration, chat response, silent enforcement, and whether the user receives an explanation.
- Support role and identity exemptions for broadcaster, moderators, VIPs, subscribers, regulars, trusted users, bots, and named users.
- Support temporary permit windows so a moderator can allow a user to post one or more links for a short period.
- Support allowlists and blocklists for links, domains, phrases, and users.
- Support dry-run mode so a streamer can observe what would have been moderated before enabling enforcement.
- Support an audit trail that records matched rule, source platform, message excerpt, normalized message, user, action taken, and whether enforcement succeeded.
- Support per-platform configuration because Twitch, YouTube, and Kick moderation capabilities are not identical.
- Support a safe default policy that is useful but conservative enough to avoid punishing normal chat behavior on import.

#### Important Edge Cases

- A platform may expose a chat message but not enough information to delete exactly that message.
- Moderation actions can fail because the bot account lacks moderator permissions, the target user outranks the bot, the message is already deleted, or the platform API is unavailable.
- A broadcaster or moderator may manually delete, timeout, or ban the same user while the policy engine is evaluating the message.
- Shared-chat or multi-platform setups can cause the same user-visible message to appear in more than one source.
- Role data may be stale or unavailable on a given event, so the feature should define how to behave when exemption status is unknown.
- Aggressive phrase matching can punish legitimate discussion, quoted text, reclaimed terms, non-English words, or words embedded inside safe words.
- Regex rules can be expensive or unsafe if they are poorly written.
- The bot's own messages and other known bots should not trigger punishment loops.
- Repeated-message detection should account for whitespace, casing, punctuation, emote-only messages, and copy-pasted raid messages.

### 2. Anti-Evasion Blocklist Plus

#### Summary

An evasion-aware matching layer for blocked terms, blocked links, suspicious usernames, and harassment phrases. This should complement the Moderation Policy Engine, but it can also be useful as a standalone message classifier.

#### Feature Requirements

- Normalize common evasion tactics before matching: mixed case, repeated letters, inserted spaces, punctuation between letters, zero-width characters, combining marks, homoglyphs, and common leetspeak substitutions.
- Detect lookalike words and domains that visually resemble blocked terms or trusted domains.
- Detect obfuscated URLs, including spaces around dots, bracketed dots, mixed Unicode domains, repeated punctuation, and disguised top-level domains.
- Expose a "why matched" explanation so streamers can understand which normalized form or rule caused the match.
- Allow per-rule sensitivity levels so streamers can choose between strict matching and conservative matching.
- Allow exceptions for words, names, emotes, or communities where normalization would create too many false positives.
- Support test cases in configuration so streamers can verify that a blocked phrase is caught and that known-safe phrases are allowed.
- Support separate handling for message text and username/display-name checks.

#### Important Edge Cases

- Unicode normalization can damage legitimate non-English languages if it assumes English-only matching.
- Some emote names, usernames, and community memes intentionally use unusual casing, repeated letters, or stylized characters.
- Overly aggressive leetspeak handling can match innocent short words.
- Domain lookalike detection can create false positives for unrelated domains that happen to be visually similar.
- Emoji, skin-tone modifiers, surrogate pairs, and combining marks should not crash matching or split strings incorrectly.
- A blocked term may appear inside a URL, quoted article title, song title, or game title.
- Users may post screenshots or images containing banned content, which this feature cannot inspect unless paired with a separate image moderation system.

### 3. Conditional Timer Orchestrator

#### Summary

A campaign-style timer system for announcements and recurring actions that goes beyond native Streamer.bot timers. Native timers already cover interval, random interval, repeat, and single-platform line-count gating; this feature should focus on multi-message campaigns, multi-platform targeting, and richer conditions.

#### Feature Requirements

- Support announcement campaigns with multiple rotating messages.
- Support platform-specific target lists, so one campaign can post to Twitch, YouTube, Kick, or selected targets only.
- Support per-target message overrides for wording, URL format, platform-specific length, or audience context.
- Support conditions such as live/offline state, stream title, category/game, viewer count range, time of day, day of week, and minimum chat activity.
- Support multi-platform chat activity gates that do not depend on Streamer.bot's native timer line-count limitation.
- Support "run action" campaigns, not only "send message" campaigns, so timers can trigger OBS changes, sound cues, reminders, or integrations.
- Support campaign priority and collision rules when multiple timers become eligible at the same time.
- Support quiet-chat behavior, such as skipping, delaying, or requiring fresh human chat before posting.
- Support temporary pause/resume and per-campaign enable/disable controls.
- Support last-sent tracking per campaign and target.

#### Important Edge Cases

- Native timer line counts do not work when multiple streaming platforms are connected, so multi-platform activity tracking must define its own behavior.
- A timer may become eligible while the stream is offline, reconnecting, or switching categories.
- The resolved message may exceed a platform's chat message length limit.
- Platform send actions can fail because the broadcaster account, bot account, or chat connection is unavailable.
- Multiple timers can become eligible at the same moment and create chat spam unless collision rules exist.
- A campaign may target a platform that is not connected in the user's Streamer.bot profile.
- A bot message from a timer should not count as human activity for another timer.
- A campaign may include stale URLs, expired invite links, or placeholders that were never configured.

### 4. Viewer Queue Manager

#### Summary

A general-purpose viewer request queue for activities where chat participants need to be collected, ordered, selected, and managed. Examples include community games, level requests, song-performance requests, co-op slots, VOD reviews, and "play with viewers" nights.

#### Feature Requirements

- Support user commands for join, leave, position, queue list, and status.
- Support moderator commands for next, pick random, move, remove, clear, pause, resume, close, open, and add note.
- Support queue modes: first-in-first-out, random pick, priority pick, manual pick, and round-robin.
- Support per-user limits, duplicate prevention, cooldowns, and maximum queue length.
- Support optional request text, such as a game code, level ID, lobby name, URL, or short note.
- Support role priority for subscribers, VIPs, regulars, channel members, or named groups.
- Support no-show handling, such as skip, requeue, remove, or mark absent.
- Support queue announcements that are rate-limited and activity-gated.
- Support persistence across Streamer.bot restarts, with an option to clear automatically when the stream starts or ends.
- Support a compact moderator-facing summary for current queue state.

#### Important Edge Cases

- The same person may join from multiple platforms or with multiple accounts.
- A user may rename their account after joining.
- A request may contain unsafe text, private information, malicious links, or content that violates stream rules.
- The queue may fill while a viewer is typing a join command.
- A moderator may remove a user at the same time the streamer picks the next user.
- A user may leave chat but still be in queue.
- The queue should define whether offline joins are accepted.
- Priority handling must not starve non-priority users indefinitely.

### 5. Raffle And Giveaway Toolkit

#### Summary

A giveaway workflow for collecting eligible entrants, drawing winners, rerolling, and recording results. Streamer.bot has useful primitives such as present viewers and random user selection, but not a complete giveaway system.

#### Feature Requirements

- Support entry methods: keyword in chat, active chatters, present viewers, manual add, role-based add, and command-based add.
- Support eligibility filters for broadcaster, moderators, bots, subscribers, VIPs, followers, channel members, named users, and ignored users.
- Support one-entry-per-user rules and optional bonus luck multipliers.
- Support multi-winner drawings, rerolls, alternates, and winner confirmation.
- Support giveaway states: draft, open, locked, drawing, completed, and cancelled.
- Support visible chat announcements for opening, closing, winner selection, reroll, and completion.
- Support a winner history and audit log that records entrants, eligibility filter, draw time, winner, rerolls, and moderator actions.
- Support cooldowns and minimum active-chat thresholds to avoid repeated giveaway spam.
- Support a mode that excludes recent winners.

#### Important Edge Cases

- There may be zero eligible entrants when the draw occurs.
- Users may enter multiple times with different casing, display names, or platforms.
- Bots, moderators, or the broadcaster may accidentally satisfy the entry condition.
- A winner may leave chat before claiming the prize.
- Bonus luck can be perceived as unfair unless it is transparent.
- Multi-platform giveaways need a clear identity model: one entry per platform account or one entry per person.
- Local laws, platform rules, and sponsor terms may restrict paid-entry or purchase-related giveaways.
- A giveaway should remain auditable if Streamer.bot restarts mid-event.

### 6. Media Request Moderation Queue

#### Summary

A moderated queue for viewer-submitted media links, such as songs, videos, clips, or short audio/visual requests. Streamer.bot can control OBS media sources, but this feature would provide the request intake, safety checks, approval workflow, and queue state that streamer bots commonly offer.

#### Feature Requirements

- Support request submission from chat commands, channel point rewards, or manual moderator entry.
- Support URL parsing and provider allowlists, such as YouTube, Twitch clips, SoundCloud, or streamer-defined domains.
- Support metadata preview before approval, including title, channel/uploader, duration, thumbnail when available, and original requester.
- Support automatic rejection rules for duration, duplicate URLs, unsupported providers, blocked channels, blocked keywords, and malformed URLs.
- Support moderator states: pending, approved, rejected, playing, skipped, completed, and failed.
- Support per-user queue limits, global queue length, cooldowns, and optional request costs.
- Support controls for next, skip, pause queue, clear queue, remove requester, and ban media item.
- Support chat notifications that do not leak unsafe titles unless approved.
- Support history for played, skipped, rejected, and failed items.

#### Important Edge Cases

- A URL can become private, deleted, age-restricted, geo-blocked, or unavailable after approval.
- A user may submit a playlist, livestream, redirect, short URL, or malicious URL instead of a single item.
- Metadata can contain offensive text even if the media itself is never played.
- Copyright, DMCA, platform rules, and sponsor requirements may make some requests unsafe.
- Two moderators may approve or reject the same item concurrently.
- A requester may leave chat before their item plays.
- A media item may be much louder or quieter than expected.
- The queue must define what happens if playback fails midway.

### 7. Loyalty Points Lite

#### Summary

A lightweight local loyalty currency for Streamer.bot. This is distinct from Twitch Channel Points and Streamer.bot Credits: the proposed feature is a bot-managed points economy with earning, spending, balances, redemptions, and administrative controls.

#### Feature Requirements

- Support earning points from chat activity, present-viewer ticks, stream events, manual grants, command use, and optional streak bonuses.
- Support spending points on commands, redeems, raffles, queue priority, sound effects, OBS actions, or streamer-defined rewards.
- Support balances per user, with optional per-platform or merged identity behavior.
- Support a transaction ledger with reason, amount, source, timestamp, and moderator/admin actor.
- Support admin commands for add, remove, set, reset, refund, inspect, and transfer if enabled.
- Support leaderboards, ranks, and opt-out or hidden-user handling.
- Support earning cooldowns to reduce spam farming.
- Support configurable caps, daily bonuses, multipliers, decay, and seasonal resets.
- Support safe import defaults that do not create runaway inflation.

#### Important Edge Cases

- Users may spam low-effort messages to farm points.
- Lurkers, active chatters, subscribers, and donors may have different fairness expectations.
- Multi-platform identity merging can accidentally combine unrelated users with the same display name.
- Refunds must be clear when a redemption fails after points are deducted.
- Negative balances should be either forbidden or explicitly supported.
- Point inflation can make rewards meaningless over time.
- Bot accounts and known spam accounts should not earn points.
- A corrupted or edited balance should be detectable through the ledger.

### 8. Emote Combo And Pyramid Game Pack

#### Summary

A chat engagement pack that rewards emote activity beyond Streamer.bot's native Twitch pyramid triggers. Native pyramid success should be treated as an input, not something to duplicate unnecessarily.

#### Feature Requirements

- Support Twitch pyramid success rewards using the native pyramid trigger.
- Support optional pyramid-broken responses, such as playful messages, counters, or cooldowns.
- Support emote combo detection across consecutive chat messages, such as "five users used the same emote in a row."
- Support emote bingo prompts, where the streamer or bot announces target emotes and chat tries to complete them.
- Support milestone rewards for combo length, unique participants, first combo of stream, and personal bests.
- Support cooldowns to avoid encouraging constant emote spam.
- Support configurable reward actions, such as chat messages, sound cues, OBS effects, counters, or loyalty-point bonuses.
- Support leaderboards and per-stream reset behavior.
- Support platform-specific behavior, since native pyramid detection is Twitch-specific.

#### Important Edge Cases

- Third-party emote names from 7TV, BTTV, and FFZ can change during a stream.
- A plain word can match an emote name accidentally.
- Some platforms may not expose emote data consistently.
- Emote games can conflict with moderation rules for caps, repetition, or emote spam.
- Encouraging combos can make chat less readable during serious moments.
- Users may intentionally break pyramids or spam invalid emotes for attention.
- Shared-chat and multi-platform setups may interleave messages in ways that make combo ordering ambiguous.
- A high-traffic chat may trigger rewards too frequently unless cooldowns and thresholds are conservative.

## Excluded Or Parked Ideas

### Command Hub With Variables, Aliases, Cooldowns

This idea is parked because Streamer.bot natively covers the core behavior: platform-agnostic commands, aliases, Basic/Regex matching, location modes, global cooldowns, user cooldowns, permissions, persistent counters, user counters, groups, and command listing.

A future idea may still be worthwhile if it is reframed as a "portable command pack manager" rather than a command system. That would be a packaging, documentation, and configuration feature, not a replacement for native commands.

### Quotes, Counters, And Stream Memory

This idea is parked because Streamer.bot already has a built-in quote system with quote commands, quote IDs, timestamps, platform/category metadata, and permissions. Streamer.bot also supports command/reward counters and global-variable counters.

A future idea may still be worthwhile if it is reframed as an "advanced stream memory library" with searchable tagged memories, clip references, cross-platform notes, import/export, and moderation review. That would need a narrower scope than the original quote/counter suggestion.
