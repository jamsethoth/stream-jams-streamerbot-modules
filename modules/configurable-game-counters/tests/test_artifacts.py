import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_ROOT = ROOT / "modules" / "configurable-game-counters"


class ConfigurableGameCountersArtifactsTest(unittest.TestCase):
    def test_expected_extension_files_exist(self):
        expected_files = [
            "README.md",
            "module.json",
            "src/config/default-config.json",
            "src/actions/configure-defaults.cs",
            "src/actions/track-chat-counter-callout.cs",
            "src/actions/set-current-game.cs",
            "src/actions/sync-current-game-from-twitch.cs",
            "src/actions/report-counter.cs",
            "src/actions/adjust-counter.cs",
            "src/actions/reset-counter.cs",
            "docs/import-prep.md",
            "docs/trigger-setup.md",
            "docs/manual-test-checklist.md",
        ]

        for relative_path in expected_files:
            with self.subTest(path=relative_path):
                self.assertTrue((MODULE_ROOT / relative_path).is_file())

    def test_default_config_defines_counter_and_game_contracts(self):
        config = json.loads(
            (MODULE_ROOT / "src" / "config" / "default-config.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertIn("debugLogging", config)
        self.assertIn("chatParser", config)
        self.assertIn("currentGame", config)
        self.assertIn("permissions", config)
        self.assertIn("counters", config)
        self.assertTrue(config["chatParser"]["enabled"])
        self.assertEqual(config["currentGame"]["fallbackKey"], "uncategorized")
        self.assertEqual(config["currentGame"]["fallbackName"], "Uncategorized")
        self.assertTrue(config["currentGame"]["twitchSync"]["enabled"])
        self.assertEqual(
            config["currentGame"]["twitchSync"]["mode"],
            "autoWithManualLock",
        )

        counters = config["counters"]
        for counter_id in ("greed", "death", "level_up"):
            with self.subTest(counter=counter_id):
                counter = counters[counter_id]
                self.assertTrue(counter["enabled"])
                self.assertGreater(len(counter["aliases"]), 0)
                self.assertTrue(counter["label"].strip())
                self.assertIn("{gameCount}", counter["responseTemplate"])
                self.assertIn("{globalCount}", counter["responseTemplate"])

        self.assertIn("!greed", counters["greed"]["aliases"])
        self.assertIn("!death", counters["death"]["aliases"])
        self.assertIn("!levelup", counters["level_up"]["aliases"])

    def test_module_manifest_describes_generated_actions(self):
        manifest = json.loads((MODULE_ROOT / "module.json").read_text(encoding="utf-8"))

        self.assertEqual(manifest["id"], "configurable-game-counters")
        self.assertEqual(manifest["license"], "AGPL-3.0-or-later")
        self.assertEqual(manifest["group"], "Configurable Game Counters")
        self.assertEqual(
            [action["name"] for action in manifest["actions"]],
            [
                "CGC - Configure Defaults",
                "CGC - Track Chat Counter Callout",
                "CGC - Set Current Game",
                "CGC - Sync Current Game From Twitch",
                "CGC - Report Counter",
                "CGC - Adjust Counter",
                "CGC - Reset Counter",
            ],
        )
        self.assertTrue(manifest["actions"][0]["autoRun"])
        self.assertEqual(manifest["defaultConfig"], "src/config/default-config.json")
        self.assertIn(
            "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll",
            manifest["references"],
        )
        self.assertIn(
            "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll",
            manifest["references"],
        )

    def test_action_sources_contain_required_state_contracts(self):
        sources = {
            path.name: path.read_text(encoding="utf-8")
            for path in (MODULE_ROOT / "src" / "actions").glob("*.cs")
        }
        combined = "\n".join(sources.values())

        required_fragments = [
            "gameCounters.config",
            "gameCounters.currentGame.key",
            "gameCounters.currentGame.name",
            "gameCounters.currentGame.source",
            "gameCounters.currentGame.updatedUtc",
            "gameCounters.currentGame.twitchGameId",
            "gameCounters.counts.global.",
            "gameCounters.counts.byGame.",
            "gameCounters.lastIncrementUtc.",
            "autoWithManualLock",
            "manualLockUntilUtc",
            "uncategorized",
            "ResolveCounterFromMessage",
            "IncrementCounter",
            "SanitizeKey",
            "ApplyTwitchSync",
            "ResetCounter",
            "confirm",
            "HasCallerContext",
        ]

        for fragment in required_fragments:
            with self.subTest(fragment=fragment):
                self.assertIn(fragment, combined)

        for source_name, source in sources.items():
            with self.subTest(source=source_name):
                self.assertIn("public class CPHInline", source)
                self.assertIn("public bool Execute()", source)
                self.assertNotRegex(source, r"args\s*\[")

    def test_docs_describe_setup_sync_state_and_testing(self):
        readme = (MODULE_ROOT / "README.md").read_text(encoding="utf-8")
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

        self.assertIn("configurable-game-counters.sb", readme)
        self.assertRegex(readme, re.compile(r"gameCounters\.counts\.global", re.IGNORECASE))
        self.assertRegex(readme, re.compile(r"gameCounters\.counts\.byGame", re.IGNORECASE))
        self.assertRegex(trigger_setup, re.compile(r"chat message", re.IGNORECASE))
        self.assertRegex(trigger_setup, re.compile(r"Stream Update", re.IGNORECASE))
        self.assertRegex(trigger_setup, re.compile(r"Game Only", re.IGNORECASE))
        self.assertRegex(trigger_setup, re.compile(r"manual lock", re.IGNORECASE))

        expected_cases = [
            "greed callout",
            "death callout",
            "level up callout",
            "current game fallback",
            "Twitch category sync",
            "manual lock",
            "report counter",
            "adjust counter",
            "reset confirmation",
            "game change does not reset",
        ]
        for case in expected_cases:
            with self.subTest(case=case):
                self.assertRegex(checklist, re.compile(re.escape(case), re.IGNORECASE))


if __name__ == "__main__":
    unittest.main()
