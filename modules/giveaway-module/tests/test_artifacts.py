import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_ROOT = ROOT / "modules" / "giveaway-module"


class GiveawayModuleArtifactsTest(unittest.TestCase):
    def test_expected_extension_files_exist(self):
        expected_files = [
            "README.md",
            "module.json",
            "src/config/default-config.json",
            "src/actions/configure-defaults.cs",
            "src/actions/handle-command-entry.cs",
            "src/actions/handle-twitch-reward-entry.cs",
            "src/actions/enter-giveaway.cs",
            "src/actions/clear-giveaway.cs",
            "src/actions/draw-giveaway.cs",
            "docs/import-prep.md",
            "docs/command-setup.md",
            "docs/trigger-setup.md",
            "docs/manual-test-checklist.md",
        ]

        for relative_path in expected_files:
            with self.subTest(path=relative_path):
                self.assertTrue((MODULE_ROOT / relative_path).is_file())

    def test_default_config_matches_giveaway_contract(self):
        config = json.loads(
            (MODULE_ROOT / "src" / "config" / "default-config.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(config["schemaVersion"], 1)
        self.assertIn("rewardEntry", config)
        self.assertIn("permissions", config)
        self.assertIn("responses", config)
        self.assertTrue(config["rewardEntry"]["enabled"])
        self.assertIn("rewardIds", config["rewardEntry"])
        self.assertIn("rewardNames", config["rewardEntry"])
        self.assertTrue(config["rewardEntry"]["matchAnyWhenUnconfigured"])
        self.assertEqual(config["permissions"]["manage"], "moderator")

        for response_key in (
            "entered",
            "alreadyEntered",
            "alreadyWon",
            "cleared",
            "winner",
            "noEntries",
            "entryFailed",
        ):
            with self.subTest(response=response_key):
                self.assertIn(response_key, config["responses"])
                self.assertTrue(config["responses"][response_key].strip())

    def test_module_manifest_describes_generated_actions_and_commands(self):
        manifest = json.loads((MODULE_ROOT / "module.json").read_text(encoding="utf-8"))

        self.assertEqual(manifest["id"], "giveaway-module")
        self.assertEqual(manifest["license"], "AGPL-3.0-or-later")
        self.assertEqual(manifest["group"], "Giveaway Module")
        self.assertEqual(
            [action["name"] for action in manifest["actions"]],
            [
                "GWM - Configure Defaults",
                "GWM - Handle Command Entry",
                "GWM - Handle Twitch Reward Entry",
                "GWM - Enter Giveaway",
                "GWM - Clear Giveaway",
                "GWM - Draw Giveaway",
            ],
        )
        self.assertTrue(manifest["actions"][0]["autoRun"])
        self.assertEqual(manifest["defaultConfig"], "src/config/default-config.json")
        self.assertEqual(
            [command["name"] for command in manifest["commands"]],
            ["Giveaway Enter", "Giveaway Clear", "Giveaway Draw"],
        )
        self.assertEqual(manifest["commands"][0]["aliases"], ["!giveaway enter"])
        self.assertEqual(manifest["commands"][1]["aliases"], ["!giveaway clear"])
        self.assertEqual(manifest["commands"][2]["aliases"], ["!giveaway draw"])
        self.assertEqual(manifest["commands"][1]["permittedGroups"], ["Moderators"])
        self.assertEqual(manifest["commands"][2]["permittedGroups"], ["Moderators"])
        for command in manifest["commands"]:
            with self.subTest(command=command["name"]):
                self.assertFalse(command["enabled"])

    def test_action_sources_contain_required_state_and_entry_contracts(self):
        sources = {
            path.name: path.read_text(encoding="utf-8")
            for path in (MODULE_ROOT / "src" / "actions").glob("*.cs")
        }
        combined = "\n".join(sources.values())

        required_fragments = [
            "public class CPHInline",
            "public bool Execute()",
            "giveawayModule.config",
            "giveawayModule.state",
            "CPH.SetGlobalVar(StateGlobal",
            "EnsureGlobal(ConfigGlobal",
            "CPH.SetArgument(\"entrySource\", \"command\")",
            "CPH.SetArgument(\"entrySource\", \"reward\")",
            "GWM - Enter Giveaway",
            "RewardMatches",
            "rewardIds",
            "rewardNames",
            "FindByUserId",
            "alreadyEntered",
            "alreadyWon",
            "winners",
            "drawnAtUtc",
            "CallerIsAllowed",
        ]

        for fragment in required_fragments:
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, combined)

        for source_name, source in sources.items():
            with self.subTest(source=source_name):
                self.assertIn("public class CPHInline", source)
                self.assertIn("public bool Execute()", source)
                self.assertNotRegex(source, r"args\s*\[")

    def test_docs_describe_setup_rewards_state_and_testing(self):
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

        self.assertIn("giveaway-module.sb", readme)
        self.assertIn("giveawayModule.state", readme)
        self.assertRegex(command_setup, re.compile(r"!giveaway enter", re.IGNORECASE))
        self.assertRegex(command_setup, re.compile(r"!giveaway clear", re.IGNORECASE))
        self.assertRegex(command_setup, re.compile(r"!giveaway draw", re.IGNORECASE))
        self.assertIn("Reward Redemption", trigger_setup)
        self.assertIn("rewardId", trigger_setup)
        self.assertIn("rewardName", trigger_setup)
        self.assertIn("userId", trigger_setup)

        expected_cases = [
            "Command Entry",
            "Reward Entry",
            "non-matching reward",
            "moves from `entries` to `winners`",
            "cannot re-enter",
            "previous winner can enter again after clear",
            "non-moderator",
        ]
        for case in expected_cases:
            with self.subTest(case=case):
                self.assertRegex(checklist, re.compile(re.escape(case), re.IGNORECASE))


if __name__ == "__main__":
    unittest.main()
