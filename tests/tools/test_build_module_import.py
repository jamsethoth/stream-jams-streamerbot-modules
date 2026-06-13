import base64
import gzip
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MODULE_ROOT = ROOT / "modules" / "activity-gated-chat-announcements"
FIRST_CHAT_MODULE_ROOT = ROOT / "modules" / "first-chat-shoutouts"
GIVEAWAY_MODULE_ROOT = ROOT / "modules" / "giveaway-module"
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
SYSTEM_REFERENCE = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll"
MSCORLIB_REFERENCE = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\mscorlib.dll"


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


def command_by_name(payload, name):
    for command in payload["data"]["commands"]:
        if command["name"] == name:
            return command

    raise AssertionError(f"Missing command {name}")


def action_code(action):
    sub_action = action["subActions"][0]
    if "byteCode" in sub_action:
        return base64.b64decode(sub_action["byteCode"]).decode("utf-8")

    return sub_action["code"]


def action_references(action):
    return action["subActions"][0].get("references", [])


def csharp_sub_action():
    return {
        "byteCode": base64.b64encode(
            (
                "public class CPHInline { public bool Execute() "
                "{ return true; } }"
            ).encode("utf-8")
        ).decode("ascii"),
        "enabled": True,
        "id": "00000000-0000-4000-8000-000000000001",
        "index": 0,
        "parentId": None,
        "references": [
            "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\mscorlib.dll"
        ],
        "type": 99999,
    }


def streamerbot_action(name, action_id, triggers=None, sub_actions=None):
    return {
        "alwaysRun": False,
        "collapsedGroups": [],
        "concurrent": False,
        "enabled": True,
        "excludeFromHistory": False,
        "excludeFromPending": False,
        "group": "FCS Stub",
        "id": action_id,
        "name": name,
        "queue": "00000000-0000-0000-0000-000000000000",
        "randomAction": False,
        "subActions": list(sub_actions or [csharp_sub_action()]),
        "triggers": list(triggers or []),
    }


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

    def test_adds_framework_references_for_using_directives(self):
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
                ]
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            module_dir = Path(tmp_dir) / "regex-module"
            action_dir = module_dir / "src" / "actions"
            action_dir.mkdir(parents=True)
            (action_dir / "uses-regex.cs").write_text(
                (
                    "using System;\n"
                    "using System.Text.RegularExpressions;\n\n"
                    "public class CPHInline { public bool Execute() { return true; } }\n"
                ),
                encoding="utf-8",
            )
            (module_dir / "module.json").write_text(
                json.dumps(
                    {
                        "id": "regex-module",
                        "name": "Regex Module",
                        "version": "0.1.0",
                        "description": "Fixture",
                        "group": "Fixture",
                        "actions": [
                            {
                                "name": "Fixture - Uses Regex",
                                "source": "src/actions/uses-regex.cs",
                            }
                        ],
                        "references": [],
                    }
                ),
                encoding="utf-8",
            )
            input_path = Path(tmp_dir) / "stub.sb"
            output_path = Path(tmp_dir) / "prepared.sb"
            input_path.write_bytes(encode_payload(payload))

            module.prepare_module_import(
                module_dir=module_dir,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        action = action_by_name(prepared, "Fixture - Uses Regex")
        self.assertIn(MSCORLIB_REFERENCE, action_references(action))
        self.assertIn(SYSTEM_REFERENCE, action_references(action))

    def test_rejects_unmapped_using_directive(self):
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
                ]
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            module_dir = Path(tmp_dir) / "unknown-module"
            action_dir = module_dir / "src" / "actions"
            action_dir.mkdir(parents=True)
            (action_dir / "uses-unknown.cs").write_text(
                (
                    "using System;\n"
                    "using Example.External.Package;\n\n"
                    "public class CPHInline { public bool Execute() { return true; } }\n"
                ),
                encoding="utf-8",
            )
            (module_dir / "module.json").write_text(
                json.dumps(
                    {
                        "id": "unknown-module",
                        "name": "Unknown Module",
                        "version": "0.1.0",
                        "description": "Fixture",
                        "group": "Fixture",
                        "actions": [
                            {
                                "name": "Fixture - Uses Unknown",
                                "source": "src/actions/uses-unknown.cs",
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            input_path = Path(tmp_dir) / "stub.sb"
            output_path = Path(tmp_dir) / "prepared.sb"
            input_path.write_bytes(encode_payload(payload))

            with self.assertRaisesRegex(
                ValueError,
                "reference mappings.*Example.External.Package",
            ):
                module.prepare_module_import(
                    module_dir=module_dir,
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

        command = command_by_name(prepared, "First Chat Shoutout")
        all_command = command_by_name(prepared, "First Chat Shoutout All")
        auto_command = command_by_name(prepared, "First Chat Shoutout Auto Toggle")
        auto_add_command = command_by_name(prepared, "First Chat Shoutout Auto Add")
        manual_action = action_by_name(prepared, "FCS - Handle Manual Twitch Shoutout")
        manual_all_action = action_by_name(
            prepared,
            "FCS - Handle Manual Twitch Shoutout All",
        )
        auto_toggle_action = action_by_name(
            prepared,
            "FCS - Handle Auto Shoutout Toggle",
        )
        auto_add_action = action_by_name(
            prepared,
            "FCS - Handle Auto Shoutout Add",
        )
        first_words_action = action_by_name(
            prepared, "FCS - Handle Twitch First Words"
        )

        self.assertEqual(len(prepared["data"]["commands"]), 4)
        self.assertEqual(command["name"], "First Chat Shoutout")
        self.assertEqual(command["command"], "!so\r\n!shoutout")
        self.assertFalse(command["enabled"])
        self.assertEqual(command["group"], "First Chat Shoutouts")
        self.assertEqual(command["permittedGroups"], ["Moderators"])
        self.assertEqual(command["sources"], 1)
        self.assertEqual(all_command["name"], "First Chat Shoutout All")
        self.assertEqual(all_command["command"], "!soall\r\n!shoutoutall")
        self.assertFalse(all_command["enabled"])
        self.assertEqual(all_command["permittedGroups"], ["Moderators"])
        self.assertEqual(auto_command["name"], "First Chat Shoutout Auto Toggle")
        self.assertEqual(auto_command["command"], "!soauto\r\n!shoutoutauto")
        self.assertFalse(auto_command["enabled"])
        self.assertEqual(auto_command["permittedGroups"], ["Moderators"])
        self.assertEqual(auto_add_command["name"], "First Chat Shoutout Auto Add")
        self.assertEqual(
            auto_add_command["command"],
            "!soautoadd\r\n!addsoauto\r\n!shoutoutautoadd",
        )
        self.assertFalse(auto_add_command["enabled"])
        self.assertEqual(auto_add_command["permittedGroups"], ["Moderators"])
        self.assertIn(SYSTEM_REFERENCE, action_references(manual_action))
        self.assertIn(SYSTEM_REFERENCE, action_references(auto_toggle_action))
        self.assertIn(SYSTEM_REFERENCE, action_references(auto_add_action))

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
            manual_all_action["triggers"],
            [
                {
                    "commandId": all_command["id"],
                    "enabled": True,
                    "exclusions": [],
                    "id": module.deterministic_id(
                        "first-chat-shoutouts",
                        "trigger:FCS - Handle Manual Twitch Shoutout All:command:First Chat Shoutout All",
                    ),
                    "type": 401,
                }
            ],
        )
        self.assertEqual(
            auto_toggle_action["triggers"],
            [
                {
                    "commandId": auto_command["id"],
                    "enabled": True,
                    "exclusions": [],
                    "id": module.deterministic_id(
                        "first-chat-shoutouts",
                        "trigger:FCS - Handle Auto Shoutout Toggle:command:First Chat Shoutout Auto Toggle",
                    ),
                    "type": 401,
                }
            ],
        )
        self.assertEqual(
            auto_add_action["triggers"],
            [
                {
                    "commandId": auto_add_command["id"],
                    "enabled": True,
                    "exclusions": [],
                    "id": module.deterministic_id(
                        "first-chat-shoutouts",
                        "trigger:FCS - Handle Auto Shoutout Add:command:First Chat Shoutout Auto Add",
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

    def test_giveaway_module_import_contains_commands_and_reward_handler(self):
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
            output_path = Path(tmp_dir) / "giveaway-module.sb"
            input_path.write_bytes(encode_payload(payload))

            module.prepare_module_import(
                module_dir=GIVEAWAY_MODULE_ROOT,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        entry_command = command_by_name(prepared, "Giveaway Enter")
        clear_command = command_by_name(prepared, "Giveaway Clear")
        draw_command = command_by_name(prepared, "Giveaway Draw")
        command_entry_action = action_by_name(prepared, "GWM - Handle Command Entry")
        reward_entry_action = action_by_name(
            prepared,
            "GWM - Handle Twitch Reward Entry",
        )
        clear_action = action_by_name(prepared, "GWM - Clear Giveaway")
        draw_action = action_by_name(prepared, "GWM - Draw Giveaway")
        configure_action = action_by_name(prepared, "GWM - Configure Defaults")

        self.assertEqual(prepared["meta"]["name"], "Giveaway Module")
        self.assertEqual(len(prepared["data"]["actions"]), 6)
        self.assertEqual(len(prepared["data"]["commands"]), 3)
        self.assertEqual(entry_command["command"], "!giveaway enter")
        self.assertEqual(clear_command["command"], "!giveaway clear")
        self.assertEqual(draw_command["command"], "!giveaway draw")
        self.assertFalse(entry_command["enabled"])
        self.assertFalse(clear_command["enabled"])
        self.assertFalse(draw_command["enabled"])
        self.assertEqual(clear_command["permittedGroups"], ["Moderators"])
        self.assertEqual(draw_command["permittedGroups"], ["Moderators"])
        self.assertEqual(
            command_entry_action["triggers"],
            [
                {
                    "commandId": entry_command["id"],
                    "enabled": True,
                    "exclusions": [],
                    "id": module.deterministic_id(
                        "giveaway-module",
                        "trigger:GWM - Handle Command Entry:command:Giveaway Enter",
                    ),
                    "type": 401,
                }
            ],
        )
        self.assertEqual(clear_action["triggers"][0]["commandId"], clear_command["id"])
        self.assertEqual(clear_action["triggers"][0]["type"], 401)
        self.assertEqual(draw_action["triggers"][0]["commandId"], draw_command["id"])
        self.assertEqual(draw_action["triggers"][0]["type"], 401)
        self.assertEqual(reward_entry_action["triggers"], [])
        for action in prepared["data"]["actions"]:
            with self.subTest(action=action["name"]):
                self.assertIn(MSCORLIB_REFERENCE, action_references(action))
        self.assertIn("RewardMatches", action_code(reward_entry_action))
        self.assertIn("giveawayModule.state", action_code(configure_action))
        self.assertNotIn(module.DEFAULT_CONFIG_PLACEHOLDER, action_code(configure_action))

    def test_first_chat_shoutouts_import_uses_multi_action_stub_layout(self):
        module = load_script()
        command_id = "1427b8a0-89be-4fb1-abf1-feb3b6a75eb9"
        reset_first_words_sub_action = {
            "enabled": True,
            "id": "a6d45c1b-7a24-47cc-af41-e91929af0138",
            "index": 0,
            "parentId": None,
            "type": 1026,
            "weight": 0.0,
        }
        payload = {
            "version": 23,
            "minimumVersion": "1.0.0-alpha.1",
            "exportedFrom": "1.0.4",
            "meta": {"name": "FCS Stub"},
            "data": {
                "actions": [
                    streamerbot_action(
                        "FCS Stub - Configure Defaults",
                        "35854a27-fdfe-4abd-8384-e3483cfa2f03",
                    ),
                    streamerbot_action(
                        "FCS Stub - Handle Twitch First Words",
                        "57b57340-3381-4741-add4-06e12122554d",
                    ),
                    streamerbot_action(
                        "FCS Stub - Handle Manual Twitch Shoutout",
                        "42f53126-1192-4de7-83ec-a13f07bcdebf",
                        triggers=[
                            {
                                "enabled": True,
                                "exclusions": [],
                                "id": "60810a1f-d411-4b25-8aac-68208a9d2fe3",
                                "isUserId": False,
                                "type": 120,
                                "username": None,
                            }
                        ],
                    ),
                    streamerbot_action(
                        "FCS Stub - Run Shoutout",
                        "ac723c2d-1b46-400f-9c41-75b4d6f46c80",
                        triggers=[
                            {
                                "commandId": command_id,
                                "enabled": True,
                                "exclusions": [],
                                "id": "c33b5357-a994-485f-babe-513194af1388",
                                "type": 401,
                            }
                        ],
                    ),
                    streamerbot_action(
                        "FCS Stub - Reset Stream State",
                        "8751caf9-92f5-4e2d-aa8a-d9f2a0a7ecb3",
                        triggers=[
                            {
                                "enabled": True,
                                "exclusions": [],
                                "id": "e8226c92-03c4-4feb-9d3f-2cd5c57452bb",
                                "obsId": None,
                                "type": 14005,
                            }
                        ],
                        sub_actions=[
                            reset_first_words_sub_action,
                            {
                                **csharp_sub_action(),
                                "id": "cc88c73b-cdaf-415c-b640-88fa68d0fa39",
                                "index": 1,
                            },
                        ],
                    ),
                ],
                "commands": [
                    {
                        "caseSensitive": False,
                        "command": "!so\r\n!shoutout",
                        "enabled": False,
                        "globalCooldown": 0,
                        "grantType": 0,
                        "group": "FCS Stub",
                        "id": command_id,
                        "ignoreBotAccount": True,
                        "ignoreInternal": True,
                        "include": False,
                        "location": 0,
                        "mode": 0,
                        "name": "FCS Stub - First Chat Shoutout Stub",
                        "permittedGroups": [],
                        "permittedUsers": [],
                        "persistCounter": False,
                        "persistUserCounter": False,
                        "regexExplicitCapture": False,
                        "sources": 1,
                        "userCooldown": 0,
                    }
                ],
                "queues": [],
                "timers": [],
                "websocketServers": [],
                "websocketClients": [],
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            input_path = Path(tmp_dir) / "fcs-stub.sb"
            output_path = Path(tmp_dir) / "first-chat-shoutouts.sb"
            input_path.write_bytes(encode_payload(payload))

            module.prepare_module_import(
                module_dir=FIRST_CHAT_MODULE_ROOT,
                input_path=input_path,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        command = command_by_name(prepared, "First Chat Shoutout")
        all_command = command_by_name(prepared, "First Chat Shoutout All")
        auto_command = command_by_name(prepared, "First Chat Shoutout Auto Toggle")
        first_words_action = action_by_name(
            prepared, "FCS - Handle Twitch First Words"
        )
        manual_action = action_by_name(prepared, "FCS - Handle Manual Twitch Shoutout")
        manual_all_action = action_by_name(
            prepared,
            "FCS - Handle Manual Twitch Shoutout All",
        )
        auto_toggle_action = action_by_name(
            prepared,
            "FCS - Handle Auto Shoutout Toggle",
        )
        run_action = action_by_name(prepared, "FCS - Run Shoutout")
        reset_action = action_by_name(prepared, "FCS - Reset Stream State")

        self.assertEqual(
            [sub_action["type"] for sub_action in reset_action["subActions"]],
            [1026, 99999],
        )
        self.assertEqual([sub_action["type"] for sub_action in run_action["subActions"]], [99999])
        self.assertEqual(
            [sub_action["type"] for sub_action in manual_all_action["subActions"]],
            [99999],
        )
        self.assertEqual(
            [sub_action["type"] for sub_action in auto_toggle_action["subActions"]],
            [99999],
        )
        self.assertEqual(first_words_action["triggers"][0]["type"], 120)
        self.assertEqual(first_words_action["triggers"][0]["username"], None)
        self.assertEqual(manual_action["triggers"][0]["type"], 401)
        self.assertEqual(manual_action["triggers"][0]["commandId"], command["id"])
        self.assertEqual(manual_all_action["triggers"][0]["type"], 401)
        self.assertEqual(manual_all_action["triggers"][0]["commandId"], all_command["id"])
        self.assertEqual(auto_toggle_action["triggers"][0]["type"], 401)
        self.assertEqual(auto_toggle_action["triggers"][0]["commandId"], auto_command["id"])
        self.assertEqual(reset_action["triggers"][0]["type"], 14005)
        self.assertEqual(reset_action["triggers"][0]["obsId"], None)

    def test_generated_ids_are_uuid4_shaped_for_streamerbot_imports(self):
        module = load_script()

        generated_id = module.deterministic_id(
            "first-chat-shoutouts",
            "trigger:FCS - Handle Manual Twitch Shoutout:command:First Chat Shoutout",
        )

        self.assertEqual(uuid.UUID(generated_id).version, 4)

    def test_first_chat_shoutouts_uses_module_import_stub_when_input_is_omitted(self):
        module = load_script()

        with tempfile.TemporaryDirectory() as tmp_dir:
            output_path = Path(tmp_dir) / "first-chat-shoutouts.sb"

            module.prepare_module_import(
                module_dir=FIRST_CHAT_MODULE_ROOT,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        reset_action = action_by_name(prepared, "FCS - Reset Stream State")

        self.assertEqual(
            [sub_action["type"] for sub_action in reset_action["subActions"]],
            [1026, 99999],
        )
        self.assertEqual(reset_action["triggers"][0]["type"], 14005)

    def test_first_chat_shoutouts_configure_defaults_embeds_default_config_template(self):
        module = load_script()
        default_config = (
            FIRST_CHAT_MODULE_ROOT / "src" / "config" / "default-config.json"
        ).read_text(encoding="utf-8").strip()

        with tempfile.TemporaryDirectory() as tmp_dir:
            output_path = Path(tmp_dir) / "first-chat-shoutouts.sb"

            module.prepare_module_import(
                module_dir=FIRST_CHAT_MODULE_ROOT,
                output_path=output_path,
            )

            prepared = module.read_payload(output_path)

        configure_code = action_code(action_by_name(prepared, "FCS - Configure Defaults"))

        self.assertNotIn(module.DEFAULT_CONFIG_PLACEHOLDER, configure_code)
        self.assertIn(module.csharp_verbatim_string(default_config), configure_code)

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
