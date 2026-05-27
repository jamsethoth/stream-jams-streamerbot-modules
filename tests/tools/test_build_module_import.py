import base64
import gzip
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MODULE_ROOT = ROOT / "modules" / "activity-gated-chat-announcements"
FIRST_CHAT_MODULE_ROOT = ROOT / "modules" / "first-chat-shoutouts"
SCRIPT_PATH = ROOT / "tools" / "streamerbot_import" / "build_module_import.py"
SCHEDULER_CODE_PATH = (
    MODULE_ROOT / "src" / "actions" / "run-announcement-scheduler.cs"
)
EXPECTED_ACTION_NAMES = {
    "AGA - Configure Defaults",
    "AGA - Track Chat Activity",
    "AGA - Track Twitch Main",
    "AGA - Track YouTube Main",
    "AGA - Run Announcement Scheduler",
    "AGA - Send Twitch Message",
    "AGA - Send YouTube Message",
}
SYSTEM_CORE_REFERENCE = (
    "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll"
)


def load_script():
    spec = importlib.util.spec_from_file_location("build_module_import", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def encode_payload(payload):
    raw_json = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    return base64.b64encode(b"SBAE" + gzip.compress(raw_json, mtime=0))


def action_by_name(payload, name):
    for action in payload["data"]["actions"]:
        if action["name"] == name:
            return action

    raise AssertionError(f"Missing action {name}")


def action_code(action):
    sub_action = action["subActions"][0]
    if "byteCode" in sub_action:
        return base64.b64decode(sub_action["byteCode"]).decode("utf-8")

    return sub_action["code"]


def action_references(action):
    return action["subActions"][0].get("references", [])


class SchedulerImportPrepTest(unittest.TestCase):
    def test_prepares_scheduler_import_from_known_good_export(self):
        module = load_script()
        original_code = "public class CPHInline { public bool Execute() { return true; } }"
        payload = {
            "version": 23,
            "minimumVersion": "1.0.0-alpha.1",
            "exportedFrom": "1.0.4",
            "meta": {
                "name": "Minimal Scheduler Stub",
                "author": "Local",
                "version": "0.0.1",
                "description": "Known-good export from local Streamer.bot",
            },
            "data": {
                "actions": [
                    {
                        "name": "Temporary Scheduler Stub",
                        "subActions": [
                            {
                                "type": "Execute C# Code",
                                "code": original_code,
                            }
                        ],
                    }
                ],
                "commands": [],
                "queues": [],
                "timers": [],
                "websocketServers": [],
                "websocketClients": [],
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            input_path = Path(tmp_dir) / "known-good-scheduler.sb"
            output_path = Path(tmp_dir) / "activity-gated-chat-announcements.sb"
            input_path.write_bytes(encode_payload(payload))

            result = module.prepare_module_import(
                module_dir=MODULE_ROOT,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        scheduler_code = SCHEDULER_CODE_PATH.read_text(encoding="utf-8")
        action = prepared["data"]["actions"][0]

        action_names = {action["name"] for action in prepared["data"]["actions"]}

        self.assertEqual(result.replaced_code_blocks, len(EXPECTED_ACTION_NAMES))
        self.assertEqual(
            prepared["meta"]["name"],
            "Activity-Gated Chat Announcements",
        )
        self.assertIn("experimental", prepared["meta"]["description"].lower())
        self.assertEqual(action_names, EXPECTED_ACTION_NAMES)
        self.assertEqual(
            prepared["meta"]["autoRunAction"],
            action_by_name(prepared, "AGA - Configure Defaults")["id"],
        )
        self.assertEqual(
            action_by_name(prepared, "AGA - Run Announcement Scheduler")["subActions"][0][
                "code"
            ],
            scheduler_code,
        )
        self.assertNotIn(original_code, json.dumps(prepared))

    def test_rejects_exports_without_csharp_code_block(self):
        module = load_script()
        payload = {
            "version": 23,
            "meta": {"name": "No Code"},
            "data": {"actions": [{"name": "No C# Here", "subActions": []}]},
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            input_path = Path(tmp_dir) / "no-code.json"
            output_path = Path(tmp_dir) / "prepared.json"
            input_path.write_text(json.dumps(payload), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "C# code block"):
                module.prepare_module_import(
                    module_dir=MODULE_ROOT,
                    input_path=input_path,
                    output_path=output_path,
                )

    def test_prepares_scheduler_import_with_base64_bytecode_field(self):
        module = load_script()
        original_code = "public class CPHInline { public bool Execute() { return true; } }"
        payload = {
            "version": 23,
            "minimumVersion": "1.0.0-alpha.1",
            "exportedFrom": "1.0.4",
            "meta": {"name": "Scheduler Stub"},
            "data": {
                "actions": [
                    {
                        "name": "Scheduler Stub",
                        "subActions": [
                            {
                                "type": 99999,
                                "byteCode": base64.b64encode(
                                    original_code.encode("utf-8")
                                ).decode("ascii"),
                            }
                        ],
                    }
                ]
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            input_path = Path(tmp_dir) / "bytecode-stub.sb"
            output_path = Path(tmp_dir) / "prepared.sb"
            input_path.write_bytes(encode_payload(payload))

            result = module.prepare_module_import(
                module_dir=MODULE_ROOT,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        scheduler = action_by_name(prepared, "AGA - Run Announcement Scheduler")

        self.assertEqual(result.replaced_code_blocks, len(EXPECTED_ACTION_NAMES))
        self.assertEqual(action_code(scheduler), SCHEDULER_CODE_PATH.read_text(encoding="utf-8"))

    def test_full_bundle_contains_config_globals_and_sender_actions(self):
        module = load_script()
        payload = {
            "version": 23,
            "minimumVersion": "1.0.0-alpha.1",
            "exportedFrom": "1.0.4",
            "meta": {"name": "Scheduler Stub"},
            "data": {
                "actions": [
                    {
                        "name": "Scheduler Stub",
                        "group": "Existing Group",
                        "subActions": [
                            {
                                "type": 99999,
                                "byteCode": base64.b64encode(
                                    (
                                        "public class CPHInline { public bool Execute() "
                                        "{ return true; } }"
                                    ).encode("utf-8")
                                ).decode("ascii"),
                            }
                        ],
                    }
                ],
                "commands": [],
                "queues": [],
                "timers": [],
                "websocketServers": [],
                "websocketClients": [],
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            input_path = Path(tmp_dir) / "stub.sb"
            output_path = Path(tmp_dir) / "prepared.sb"
            input_path.write_bytes(encode_payload(payload))

            module.prepare_module_import(
                module_dir=MODULE_ROOT,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        configure_code = action_code(action_by_name(prepared, "AGA - Configure Defaults"))
        twitch_code = action_code(action_by_name(prepared, "AGA - Send Twitch Message"))
        youtube_code = action_code(action_by_name(prepared, "AGA - Send YouTube Message"))

        self.assertIn("activityGatedAnnouncements.discordInviteUrl", configure_code)
        self.assertIn("activityGatedAnnouncements.twitchChannelName", configure_code)
        self.assertIn("activityGatedAnnouncements.youtubeChannelName", configure_code)
        self.assertIn("{discordInviteUrl}", configure_code)
        self.assertIn("{twitchChannelName}", configure_code)
        self.assertIn("CPH.SendMessage", twitch_code)
        self.assertIn("CPH.SendYouTubeMessageToLatestMonitored", youtube_code)
        self.assertIn(
            SYSTEM_CORE_REFERENCE,
            action_references(action_by_name(prepared, "AGA - Track Chat Activity")),
        )
        for action in prepared["data"]["actions"]:
            self.assertEqual(action["group"], "Activity-Gated Announcements")

    def test_first_chat_shoutouts_import_contains_command_and_first_words_trigger(self):
        module = load_script()
        payload = {
            "version": 23,
            "minimumVersion": "1.0.0-alpha.1",
            "exportedFrom": "1.0.4",
            "meta": {"name": "C# Stub"},
            "data": {
                "actions": [
                    {
                        "name": "C# Stub",
                        "group": "Existing Group",
                        "subActions": [
                            {
                                "type": 99999,
                                "byteCode": base64.b64encode(
                                    (
                                        "public class CPHInline { public bool Execute() "
                                        "{ return true; } }"
                                    ).encode("utf-8")
                                ).decode("ascii"),
                            }
                        ],
                    }
                ],
                "commands": [],
                "queues": [],
                "timers": [],
                "websocketServers": [],
                "websocketClients": [],
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            input_path = Path(tmp_dir) / "stub.sb"
            output_path = Path(tmp_dir) / "first-chat-shoutouts.sb"
            input_path.write_bytes(encode_payload(payload))

            module.prepare_module_import(
                module_dir=FIRST_CHAT_MODULE_ROOT,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        command = prepared["data"]["commands"][0]
        manual_action = action_by_name(prepared, "FCS - Handle Manual Twitch Shoutout")
        first_words_action = action_by_name(
            prepared, "FCS - Handle Twitch First Words"
        )

        self.assertEqual(command["name"], "First Chat Shoutout")
        self.assertEqual(command["command"], "!so\r\n!shoutout")
        self.assertFalse(command["enabled"])
        self.assertEqual(command["group"], "First Chat Shoutouts")
        self.assertEqual(command["permittedGroups"], ["Moderators"])
        self.assertEqual(command["sources"], 1)

        self.assertEqual(
            manual_action["triggers"],
            [
                {
                    "commandId": command["id"],
                    "enabled": True,
                    "exclusions": [],
                    "id": module.deterministic_id(
                        "first-chat-shoutouts",
                        "trigger:FCS - Handle Manual Twitch Shoutout:command:First Chat Shoutout",
                    ),
                    "type": 401,
                }
            ],
        )
        self.assertEqual(
            first_words_action["triggers"],
            [
                {
                    "enabled": True,
                    "exclusions": [],
                    "id": module.deterministic_id(
                        "first-chat-shoutouts",
                        "trigger:FCS - Handle Twitch First Words:twitch-first-words",
                    ),
                    "isUserId": False,
                    "type": 120,
                    "username": "",
                }
            ],
        )

    def test_cli_can_build_from_exports_dir(self):
        payload = {
            "version": 23,
            "minimumVersion": "1.0.0-alpha.1",
            "exportedFrom": "1.0.4",
            "meta": {"name": "Scheduler Stub"},
            "data": {
                "actions": [
                    {
                        "name": "Scheduler Stub",
                        "subActions": [
                            {
                                "type": "Execute C# Code",
                                "code": (
                                    "public class CPHInline { public bool Execute() "
                                    "{ return true; } }"
                                ),
                            }
                        ],
                    }
                ]
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            temp_root = Path(tmp_dir)
            exports_dir = temp_root / "exports"
            build_dir = temp_root / "build"
            exports_dir.mkdir()
            build_dir.mkdir()
            input_path = exports_dir / "csharp-stub.sb"
            output_path = build_dir / "activity-gated-chat-announcements.sb"
            input_path.write_bytes(encode_payload(payload))

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    str(MODULE_ROOT),
                    "csharp-stub.sb",
                    "../build/activity-gated-chat-announcements.sb",
                ],
                cwd=exports_dir,
                check=False,
                text=True,
                capture_output=True,
            )

            self.assertEqual(result.returncode, 0, result.stderr)
            prepared = load_script().read_payload(output_path)
            action_names = {action["name"] for action in prepared["data"]["actions"]}
            self.assertEqual(action_names, EXPECTED_ACTION_NAMES)
            self.assertIn(
                "activityGatedAnnouncements.config",
                action_code(action_by_name(prepared, "AGA - Run Announcement Scheduler")),
            )

    def test_cli_uses_committed_default_stub_when_input_is_omitted(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            output_path = Path(tmp_dir) / "activity-gated-chat-announcements.sb"

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_PATH),
                    str(MODULE_ROOT),
                    str(output_path),
                ],
                check=False,
                text=True,
                capture_output=True,
            )

            self.assertEqual(result.returncode, 0, result.stderr)
            prepared = load_script().read_payload(output_path)
            action_names = {action["name"] for action in prepared["data"]["actions"]}
            self.assertEqual(action_names, EXPECTED_ACTION_NAMES)
            self.assertEqual(
                prepared["meta"]["description"],
                (
                    "Experimental Activity-Gated Chat Announcements import prepared "
                    "from the repository Streamer.bot C# action fixture. Import into "
                    "a disposable profile first, then inspect before live use."
                ),
            )


if __name__ == "__main__":
    unittest.main()
