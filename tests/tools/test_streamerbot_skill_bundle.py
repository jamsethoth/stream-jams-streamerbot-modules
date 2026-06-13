import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from tools.streamerbot_import.sb_import_string import read_payload


ROOT = Path(__file__).resolve().parents[2]
SKILL_ROOT = ROOT / "skills" / "streamerbot-config"
MSCORLIB_REFERENCE = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\mscorlib.dll"


class StreamerbotSkillBundleTest(unittest.TestCase):
    def test_streamerbot_config_skill_bundle_is_installable(self):
        expected_files = [
            "SKILL.md",
            "agents/openai.yaml",
            "references/actions-commands.md",
            "references/csharp-actions.md",
            "references/import-strings.md",
            "references/local-api.md",
            "references/variables-state.md",
            "scripts/fixtures/streamerbot-1.0.4-csharp-stub.json",
            "scripts/fixtures/streamerbot-import-stub.sb",
            "scripts/sb_import_string.py",
            "scripts/streamerbot_sb_import_gen.py",
        ]

        for relative_path in expected_files:
            self.assertTrue(
                (SKILL_ROOT / relative_path).is_file(),
                f"Missing skill bundle file: {relative_path}",
            )

        skill = (SKILL_ROOT / "SKILL.md").read_text(encoding="utf-8")
        import_reference = (
            SKILL_ROOT / "references" / "import-strings.md"
        ).read_text(encoding="utf-8")

        self.assertIn("name: streamerbot-config", skill)
        self.assertIn("streamerbot_sb_import_gen.py", skill)
        self.assertIn("streamerbot-import-stub.sb", import_reference)
        self.assertIn("type: 401", import_reference)
        self.assertIn("Reset First Words", import_reference)

    def test_bundled_generator_builds_first_chat_import(self):
        env = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}

        with tempfile.TemporaryDirectory() as tmp_dir:
            output_path = Path(tmp_dir) / "first-chat-shoutouts.sb"
            generate_result = subprocess.run(
                [
                    sys.executable,
                    str(SKILL_ROOT / "scripts" / "streamerbot_sb_import_gen.py"),
                    "modules/first-chat-shoutouts",
                    str(output_path),
                ],
                cwd=ROOT,
                env=env,
                check=False,
                text=True,
                capture_output=True,
            )

            self.assertEqual(generate_result.returncode, 0, generate_result.stderr)

            inspect_result = subprocess.run(
                [
                    sys.executable,
                    str(SKILL_ROOT / "scripts" / "sb_import_string.py"),
                    "inspect",
                    str(output_path),
                ],
                cwd=ROOT,
                env=env,
                check=False,
                text=True,
                capture_output=True,
            )

        self.assertEqual(inspect_result.returncode, 0, inspect_result.stderr)
        inspected = json.loads(inspect_result.stdout)

        self.assertEqual(inspected["meta"]["name"], "First Chat Shoutouts")
        self.assertEqual(inspected["counts"]["actions"], 8)
        self.assertEqual(inspected["counts"]["commands"], 4)

    def test_bundled_generator_builds_giveaway_import_with_core_references(self):
        env = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}

        with tempfile.TemporaryDirectory() as tmp_dir:
            output_path = Path(tmp_dir) / "giveaway-module.sb"
            generate_result = subprocess.run(
                [
                    sys.executable,
                    str(SKILL_ROOT / "scripts" / "streamerbot_sb_import_gen.py"),
                    "modules/giveaway-module",
                    str(output_path),
                ],
                cwd=ROOT,
                env=env,
                check=False,
                text=True,
                capture_output=True,
            )

            self.assertEqual(generate_result.returncode, 0, generate_result.stderr)
            payload = read_payload(output_path)

        for action in payload["data"]["actions"]:
            references = action["subActions"][0].get("references", [])
            with self.subTest(action=action["name"]):
                self.assertIn(MSCORLIB_REFERENCE, references)

    def test_bundled_reference_stub_decodes(self):
        env = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}

        result = subprocess.run(
            [
                sys.executable,
                str(SKILL_ROOT / "scripts" / "sb_import_string.py"),
                "inspect",
                str(
                    SKILL_ROOT
                    / "scripts"
                    / "fixtures"
                    / "streamerbot-import-stub.sb"
                ),
            ],
            cwd=ROOT,
            env=env,
            check=False,
            text=True,
            capture_output=True,
        )

        self.assertEqual(result.returncode, 0, result.stderr)
        inspected = json.loads(result.stdout)

        self.assertEqual(inspected["version"], 23)
        self.assertEqual(inspected["exportedFrom"], "1.0.4")
        self.assertEqual(inspected["counts"]["actions"], 5)
        self.assertEqual(inspected["counts"]["commands"], 1)


if __name__ == "__main__":
    unittest.main()
