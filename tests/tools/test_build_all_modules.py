import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

from tools.streamerbot_import import build_all_modules, sb_import_string


ROOT = Path(__file__).resolve().parents[2]
FIXTURE_PATH = (
    ROOT
    / "tools"
    / "streamerbot_import"
    / "fixtures"
    / "streamerbot-1.0.4-csharp-stub.json"
)


def file_digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def tree_digests(root):
    root = Path(root)
    return {
        str(path.relative_to(root)).replace("\\", "/"): file_digest(path)
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


class BuildAllModulesTest(unittest.TestCase):
    def test_build_all_modules_creates_import_text_and_docs_for_each_module(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            output_root = Path(tmp_dir) / "dist"

            result = build_all_modules.build_all_modules(
                repo_root=ROOT,
                output_root=output_root,
                stub_path=FIXTURE_PATH,
            )

            module_output = output_root / "activity-gated-chat-announcements"
            sb_path = module_output / "activity-gated-chat-announcements.sb"
            import_text_path = (
                module_output / "activity-gated-chat-announcements.import.txt"
            )
            readme_path = module_output / "README.md"
            manifest_path = module_output / "manifest.json"

            self.assertEqual(
                [module.module_id for module in result.modules],
                ["activity-gated-chat-announcements", "first-chat-shoutouts"],
            )
            self.assertTrue(sb_path.is_file())
            self.assertTrue(import_text_path.is_file())
            self.assertTrue(readme_path.is_file())
            self.assertTrue((module_output / "module.json").is_file())
            self.assertTrue(manifest_path.is_file())
            self.assertEqual(import_text_path.read_bytes(), sb_path.read_bytes())

            payload = sb_import_string.read_payload(sb_path)
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            first_chat_payload = sb_import_string.read_payload(
                output_root
                / "first-chat-shoutouts"
                / "first-chat-shoutouts.sb"
            )
            first_chat_reset_action = next(
                action
                for action in first_chat_payload["data"]["actions"]
                if action["name"] == "FCS - Reset Stream State"
            )

            self.assertEqual(
                payload["meta"]["name"],
                "Activity-Gated Chat Announcements",
            )
            self.assertEqual(manifest["moduleId"], "activity-gated-chat-announcements")
            self.assertEqual(manifest["version"], "0.1.0")
            self.assertEqual(manifest["actionCount"], 7)
            self.assertEqual(manifest["importSha256"], file_digest(sb_path))
            self.assertIn("## Installation", readme_path.read_text(encoding="utf-8"))
            self.assertEqual(
                [sub_action["type"] for sub_action in first_chat_reset_action["subActions"]],
                [1026, 99999],
            )
            self.assertEqual(first_chat_reset_action["triggers"][0]["type"], 14005)
            first_chat_manifest = json.loads(
                (
                    output_root
                    / "first-chat-shoutouts"
                    / "manifest.json"
                ).read_text(encoding="utf-8")
            )
            self.assertEqual(first_chat_manifest["actionCount"], 7)

    def test_build_all_modules_is_deterministic(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            first_output = Path(tmp_dir) / "first"
            second_output = Path(tmp_dir) / "second"

            build_all_modules.build_all_modules(ROOT, first_output, FIXTURE_PATH)
            build_all_modules.build_all_modules(ROOT, second_output, FIXTURE_PATH)

            self.assertEqual(tree_digests(first_output), tree_digests(second_output))

    def test_release_archive_is_deterministic_and_contains_module_files(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            tmp_root = Path(tmp_dir)
            first_dist = tmp_root / "first-dist"
            second_dist = tmp_root / "second-dist"
            first_zip = tmp_root / "first.zip"
            second_zip = tmp_root / "second.zip"

            build_all_modules.build_all_modules(ROOT, first_dist, FIXTURE_PATH)
            build_all_modules.build_all_modules(ROOT, second_dist, FIXTURE_PATH)
            build_all_modules.create_release_archive(first_dist, first_zip)
            build_all_modules.create_release_archive(second_dist, second_zip)

            self.assertEqual(file_digest(first_zip), file_digest(second_zip))

            with zipfile.ZipFile(first_zip) as archive:
                names = archive.namelist()

            self.assertEqual(names, sorted(names))
            self.assertIn("SHA256SUMS", names)
            self.assertIn(
                "activity-gated-chat-announcements/activity-gated-chat-announcements.sb",
                names,
            )
            self.assertIn(
                "activity-gated-chat-announcements/README.md",
                names,
            )
            self.assertIn(
                "first-chat-shoutouts/first-chat-shoutouts.sb",
                names,
            )
            self.assertIn(
                "first-chat-shoutouts/README.md",
                names,
            )

    def test_readme_validation_requires_installation_and_behavior_sections(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            module_dir = Path(tmp_dir) / "sample-module"
            module_dir.mkdir()
            (module_dir / "README.md").write_text(
                "# Sample Module\n\n## Installation\n\nInstall it.\n",
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "What It Does"):
                build_all_modules.validate_module_readme(module_dir)

    def test_output_safety_rejects_repo_root_as_output_directory(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            repo_root = Path(tmp_dir)

            with self.assertRaisesRegex(ValueError, "Refusing to clean output path"):
                build_all_modules.ensure_safe_output_root(repo_root, repo_root)


if __name__ == "__main__":
    unittest.main()
