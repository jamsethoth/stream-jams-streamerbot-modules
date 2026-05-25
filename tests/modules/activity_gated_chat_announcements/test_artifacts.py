import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_ROOT = ROOT / "modules" / "activity-gated-chat-announcements"


class ActivityGatedAnnouncementsArtifactsTest(unittest.TestCase):
    def test_expected_extension_files_exist(self):
        expected_files = [
            "README.md",
            "module.json",
            "src/config/default-config.json",
            "src/actions/track-chat-activity.cs",
            "src/actions/run-announcement-scheduler.cs",
            "src/actions/configure-defaults.cs",
            "src/actions/send-twitch-message.cs",
            "src/actions/send-youtube-message.cs",
            "src/actions/track-twitch-main.cs",
            "src/actions/track-youtube-main.cs",
            "docs/import-prep.md",
            "docs/sender-actions.md",
            "docs/manual-test-checklist.md",
        ]

        for relative_path in expected_files:
            with self.subTest(path=relative_path):
                self.assertTrue((MODULE_ROOT / relative_path).is_file())

    def test_example_config_matches_activity_gated_contract(self):
        config_path = MODULE_ROOT / "src" / "config" / "default-config.json"
        config = json.loads(config_path.read_text(encoding="utf-8"))

        self.assertIn("ignoredUsers", config)
        self.assertIn("variables", config)
        self.assertIn("targets", config)
        self.assertIn("jobs", config)
        self.assertEqual(
            config["variables"]["discordInviteUrl"],
            "activityGatedAnnouncements.discordInviteUrl",
        )
        self.assertEqual(
            config["variables"]["twitchChannelName"],
            "activityGatedAnnouncements.twitchChannelName",
        )
        self.assertEqual(
            config["variables"]["youtubeChannelName"],
            "activityGatedAnnouncements.youtubeChannelName",
        )
        self.assertIn("twitch_main", config["targets"])
        self.assertIn("youtube_main", config["targets"])

        for target_id, target in config["targets"].items():
            with self.subTest(target=target_id):
                self.assertTrue(target["enabled"])
                self.assertTrue(target["platform"])
                self.assertRegex(target["senderAction"], r"^AGA - Send .+ Message$")
                self.assertIsInstance(target["ignoredUsers"], list)

        for job in config["jobs"]:
            with self.subTest(job=job["id"]):
                self.assertTrue(job["enabled"])
                self.assertGreaterEqual(job["intervalMinutes"], 1)
                self.assertGreaterEqual(job["minChats"], 1)
                self.assertTrue(job["defaultMessage"].strip())
                self.assertIn("{discordInviteUrl}", job["defaultMessage"])
                self.assertIsInstance(job["targetIds"], list)
                self.assertGreater(len(job["targetIds"]), 0)
                for target_id in job["targetIds"]:
                    self.assertIn(target_id, config["targets"])

    def test_reference_docs_call_out_system_core_path_variation(self):
        manifest = json.loads(
            (MODULE_ROOT / "module.json").read_text(encoding="utf-8")
        )
        readme = (MODULE_ROOT / "README.md").read_text(encoding="utf-8")

        references = manifest["references"]
        self.assertIn(
            "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll",
            references,
        )
        self.assertFalse(any("Users" in reference for reference in references))
        self.assertFalse(any("/mnt/" in reference for reference in references))
        self.assertIn("## C# References", readme)
        self.assertIn("System.Core.dll", readme)
        self.assertIn("Framework64", readme)
        self.assertIn("Framework\\v4.0.30319", readme)

    def test_tracker_action_contains_required_state_and_guards(self):
        tracker = (MODULE_ROOT / "src" / "actions" / "track-chat-activity.cs").read_text(
            encoding="utf-8"
        )

        required_fragments = [
            "public class CPHInline",
            "public bool Execute()",
            "activityGatedAnnouncements.config",
            "activityGatedAnnouncements.chatCounts.",
            "CPH.TryGetArg(\"targetId\"",
            "CPH.GetGlobalVar<string>",
            "CPH.SetGlobalVar",
            "IsIgnoredChatEvent",
            "IsEnabledJobForTarget",
            "ResolveConfigVariables",
        ]

        for fragment in required_fragments:
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, tracker)

        self.assertRegex(tracker, r"StringComparer\.OrdinalIgnoreCase")
        self.assertNotRegex(tracker, r"args\s*\[")

    def test_scheduler_action_contains_required_gates_and_sender_contract(self):
        scheduler = (
            MODULE_ROOT / "src" / "actions" / "run-announcement-scheduler.cs"
        ).read_text(encoding="utf-8")

        required_fragments = [
            "public class CPHInline",
            "public bool Execute()",
            "activityGatedAnnouncements.config",
            "activityGatedAnnouncements.chatCounts.",
            "activityGatedAnnouncements.lastSentUtc.",
            "CPH.SetArgument(\"message\"",
            "CPH.SetArgument(\"targetId\"",
            "CPH.SetArgument(\"platform\"",
            "CPH.SetArgument(\"jobId\"",
            "CPH.RunAction",
            "ResolveMessage",
            "HasIntervalElapsed",
            "ResolveConfigVariables",
        ]

        for fragment in required_fragments:
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, scheduler)

        self.assertRegex(scheduler, r"DateTime\.UtcNow")
        self.assertNotRegex(scheduler, r"args\s*\[")

    def test_setup_docs_describe_streamerbot_import_usage(self):
        readme = (MODULE_ROOT / "README.md").read_text(encoding="utf-8")
        sender_docs = (MODULE_ROOT / "docs" / "sender-actions.md").read_text(
            encoding="utf-8"
        )
        import_prep = (MODULE_ROOT / "docs" / "import-prep.md").read_text(
            encoding="utf-8"
        )
        checklist = (
            MODULE_ROOT / "docs" / "manual-test-checklist.md"
        ).read_text(encoding="utf-8")

        self.assertIn("activityGatedAnnouncements.config", readme)
        self.assertIn("activity-gated-chat-announcements.sb", readme)
        self.assertIn("Import", readme)
        self.assertNotIn("paste into", readme)
        self.assertNotIn("Execute C# Code sub-action", readme)
        self.assertNotIn("known-good exported C# action stub", readme)
        self.assertIn("committed Streamer.bot 1.0.4 C# action fixture", import_prep)
        self.assertIn("Optional Custom Stub", import_prep)
        self.assertIn("disposable profile", import_prep)
        self.assertIn("build_module_import", import_prep)
        self.assertIn("%message%", sender_docs)
        self.assertIn("Twitch", sender_docs)
        self.assertIn("YouTube", sender_docs)

        expected_cases = [
            "Invalid JSON",
            "Unknown target",
            "Disabled job",
            "Ignored user",
            "Interval",
            "Target-specific message",
            "Failed sender",
        ]

        for case in expected_cases:
            with self.subTest(case=case):
                self.assertTrue(
                    re.search(re.escape(case), checklist, re.IGNORECASE),
                    f"missing manual test case: {case}",
                )


if __name__ == "__main__":
    unittest.main()
