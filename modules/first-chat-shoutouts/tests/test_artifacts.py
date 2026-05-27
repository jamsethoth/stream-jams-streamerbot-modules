import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_ROOT = ROOT / "modules" / "first-chat-shoutouts"


class FirstChatShoutoutsArtifactsTest(unittest.TestCase):
    def test_expected_extension_files_exist(self):
        expected_files = [
            "README.md",
            "module.json",
            "src/config/default-config.json",
            "src/actions/configure-defaults.cs",
            "src/actions/handle-twitch-first-words.cs",
            "src/actions/handle-manual-twitch-shoutout.cs",
            "src/actions/run-shoutout.cs",
            "src/actions/reset-stream-state.cs",
            "docs/import-prep.md",
            "docs/command-setup.md",
            "docs/trigger-setup.md",
            "docs/manual-test-checklist.md",
        ]

        for relative_path in expected_files:
            with self.subTest(path=relative_path):
                self.assertTrue((MODULE_ROOT / relative_path).is_file())

    def test_default_config_matches_first_chat_shoutout_contract(self):
        config_path = MODULE_ROOT / "src" / "config" / "default-config.json"
        config = json.loads(config_path.read_text(encoding="utf-8"))

        self.assertIn("debugLogging", config)
        self.assertIn("targets", config)
        self.assertIn("automatic", config)
        self.assertIn("manual", config)
        self.assertIn("people", config)
        self.assertIn("defaultAnnouncementTemplate", config)
        self.assertIn("lastGameFallback", config)
        self.assertIn("{lastGame}", config["defaultAnnouncementTemplate"])
        self.assertTrue(config["lastGameFallback"].strip())

        self.assertEqual(config["automatic"]["targetIds"], ["twitch_main"])
        self.assertEqual(config["manual"]["targetIds"], ["twitch_main"])
        self.assertTrue(config["manual"]["allowAnyLogin"])
        self.assertTrue(config["manual"]["moderatorOnly"])
        self.assertIn("!so", config["manual"]["aliases"])
        self.assertIn("!shoutout", config["manual"]["aliases"])

        twitch_target = config["targets"]["twitch_main"]
        self.assertEqual(twitch_target["platform"], "twitch")
        self.assertTrue(twitch_target["enabled"])
        self.assertTrue(twitch_target["nativeShoutoutEnabled"])
        self.assertTrue(twitch_target["announcementEnabled"])
        self.assertIn(
            twitch_target["announcementColor"],
            ["default", "blue", "green", "orange", "purple"],
        )

        self.assertGreater(len(config["people"]), 0)
        for person in config["people"]:
            with self.subTest(login=person["login"]):
                self.assertIn("login", person)
                self.assertNotIn("userId", person)
                self.assertEqual(person["login"], person["login"].lower())
                self.assertTrue(person["enabled"])

    def test_module_manifest_describes_generated_actions(self):
        manifest = json.loads((MODULE_ROOT / "module.json").read_text(encoding="utf-8"))

        self.assertEqual(manifest["id"], "first-chat-shoutouts")
        self.assertEqual(manifest["license"], "AGPL-3.0-or-later")
        self.assertEqual(manifest["group"], "First Chat Shoutouts")
        self.assertEqual(
            [action["name"] for action in manifest["actions"]],
            [
                "FCS - Configure Defaults",
                "FCS - Handle Twitch First Words",
                "FCS - Handle Manual Twitch Shoutout",
                "FCS - Run Shoutout",
                "FCS - Reset Stream State",
            ],
        )
        self.assertTrue(manifest["actions"][0]["autoRun"])
        self.assertEqual(manifest["defaultConfig"], "src/config/default-config.json")
        self.assertIn(
            "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll",
            manifest["references"],
        )

    def test_run_shoutout_action_contains_required_twitch_and_state_contracts(self):
        run_action = (MODULE_ROOT / "src" / "actions" / "run-shoutout.cs").read_text(
            encoding="utf-8"
        )

        required_fragments = [
            "public class CPHInline",
            "public bool Execute()",
            "firstChatShoutouts.config",
            "firstChatShoutouts.streamSessionId",
            "firstChatShoutouts.sent.",
            "CPH.TryGetArg(\"targetId\"",
            "CPH.TryGetArg(\"shoutoutLogin\"",
            "CPH.TryGetArg(\"shoutoutSource\"",
            "TwitchGetExtendedUserInfoByLogin",
            "TwitchSendShoutoutByLogin",
            "TwitchAnnounce",
            "lastGameFallback",
            "ResolveTemplate",
            "IsAutomaticSource",
            "allowAnyLogin",
        ]

        for fragment in required_fragments:
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, run_action)

        self.assertNotIn("userId", run_action)
        self.assertNotRegex(run_action, r"args\s*\[")

    def test_trigger_and_command_wrapper_actions_set_core_arguments(self):
        first_words = (
            MODULE_ROOT / "src" / "actions" / "handle-twitch-first-words.cs"
        ).read_text(encoding="utf-8")
        manual = (
            MODULE_ROOT / "src" / "actions" / "handle-manual-twitch-shoutout.cs"
        ).read_text(encoding="utf-8")
        reset = (MODULE_ROOT / "src" / "actions" / "reset-stream-state.cs").read_text(
            encoding="utf-8"
        )

        self.assertIn("CPH.SetArgument(\"targetId\", \"twitch_main\")", first_words)
        self.assertIn("CPH.SetArgument(\"shoutoutSource\", \"automatic\")", first_words)
        self.assertIn("FCS - Run Shoutout", first_words)
        self.assertRegex(first_words, r"GetFirstStringArg\([^)]*userName")

        self.assertIn("rawInput", manual)
        self.assertIn("CPH.SetArgument(\"targetId\", \"twitch_main\")", manual)
        self.assertIn("CPH.SetArgument(\"shoutoutSource\", \"manual\")", manual)
        self.assertIn("FCS - Run Shoutout", manual)

        self.assertIn("firstChatShoutouts.streamSessionId", reset)
        self.assertIn("DateTime.UtcNow.Ticks", reset)
        self.assertNotRegex(manual, r"args\s*\[")
        self.assertNotRegex(first_words, r"args\s*\[")

    def test_docs_describe_import_triggers_commands_and_testing(self):
        readme = (MODULE_ROOT / "README.md").read_text(encoding="utf-8")
        command_setup = (MODULE_ROOT / "docs" / "command-setup.md").read_text(
            encoding="utf-8"
        )
        trigger_setup = (MODULE_ROOT / "docs" / "trigger-setup.md").read_text(
            encoding="utf-8"
        )
        checklist = (
            MODULE_ROOT / "docs" / "manual-test-checklist.md"
        ).read_text(encoding="utf-8")

        for section in (
            "## What It Does",
            "## Installation",
            "## Configuration",
            "## Generated Actions",
        ):
            with self.subTest(section=section):
                self.assertIn(section, readme)

        self.assertIn("first-chat-shoutouts.sb", readme)
        self.assertIn("Twitch First Words", trigger_setup)
        self.assertIn("Stream Online", trigger_setup)
        self.assertIn("Reset First Words", trigger_setup)
        self.assertRegex(command_setup, re.compile(r"!so", re.IGNORECASE))
        self.assertRegex(command_setup, re.compile(r"!shoutout", re.IGNORECASE))
        self.assertRegex(command_setup, re.compile(r"mod", re.IGNORECASE))
        self.assertIn("any Twitch login", command_setup)

        expected_cases = [
            "configured automatic user",
            "unconfigured automatic user",
            "manual command",
            "last game fallback",
            "native shoutout cooldown",
            "per-person template",
            "stream reset",
        ]
        for case in expected_cases:
            with self.subTest(case=case):
                self.assertRegex(checklist, re.compile(re.escape(case), re.IGNORECASE))


if __name__ == "__main__":
    unittest.main()
