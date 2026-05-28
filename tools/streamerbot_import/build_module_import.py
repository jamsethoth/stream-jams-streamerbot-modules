#!/usr/bin/env python3
"""Build a Streamer.bot module import from a C# action template."""

import argparse
import base64
import binascii
import copy
import json
import re
import sys
import uuid
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[1]
DEFAULT_STUB_PATH = SCRIPT_DIR / "fixtures" / "streamerbot-1.0.4-csharp-stub.json"

try:
    from .sb_import_string import read_payload, write_payload
except ImportError:
    if str(SCRIPT_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPT_DIR))
    from sb_import_string import read_payload, write_payload


SYSTEM_CORE_REFERENCE = (
    "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll"
)
SYSTEM_REFERENCE = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll"
REQUIRED_REFERENCES_BY_USING = {
    "System.Linq": SYSTEM_CORE_REFERENCE,
    "System.Text.RegularExpressions": SYSTEM_REFERENCE,
}
DEFAULT_CONFIG_PLACEHOLDER = "__STREAMERBOT_MODULE_DEFAULT_CONFIG_JSON__"


class PrepResult:
    def __init__(self, replaced_code_blocks, output_path):
        self.replaced_code_blocks = replaced_code_blocks
        self.output_path = Path(output_path)


def prepare_module_import(
    module_dir,
    output_path,
    input_path=None,
):
    module_dir = Path(module_dir)
    manifest = load_module_manifest(module_dir)
    input_path = resolve_input_path(manifest, input_path)
    payload = read_payload(input_path)
    prepared = copy.deepcopy(payload)
    action_templates = select_action_templates(prepared, manifest)
    trigger_templates = collect_trigger_templates(prepared)
    references = list(manifest.get("references", []))
    if SYSTEM_CORE_REFERENCE not in references:
        references.append(SYSTEM_CORE_REFERENCE)
    validate_csharp_references(module_dir, manifest, references)

    actions = [
        build_csharp_action(
            action_templates[index],
            manifest,
            action,
            module_dir,
            module_dir / action["source"],
            references,
        )
        for index, action in enumerate(manifest["actions"])
    ]
    commands = [
        build_command(manifest, command) for command in manifest.get("commands", [])
    ]
    attach_configured_triggers(actions, commands, manifest, trigger_templates)
    replaced_code_blocks = len(actions)

    update_metadata(prepared, manifest)
    write_bundle_data(prepared, actions, commands)
    write_payload(prepared, output_path)

    return PrepResult(replaced_code_blocks, output_path)


def resolve_input_path(manifest, input_path):
    if input_path is not None:
        return Path(input_path)

    import_stub = manifest.get("importStub")
    if import_stub:
        module_stub_path = REPO_ROOT / import_stub
        if not module_stub_path.is_file():
            raise ValueError(
                f"Module '{manifest['id']}' references missing import stub: {import_stub}"
            )
        return module_stub_path

    return DEFAULT_STUB_PATH


def prepare_scheduler_import(input_path, output_path, scheduler_code_path=None):
    del scheduler_code_path
    module_dir = REPO_ROOT / "modules" / "activity-gated-chat-announcements"
    return prepare_module_import(module_dir, input_path=input_path, output_path=output_path)


def load_module_manifest(module_dir):
    manifest_path = Path(module_dir) / "module.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    required_keys = ("id", "name", "version", "description", "group", "actions")
    missing_keys = [key for key in required_keys if key not in manifest]
    if missing_keys:
        raise ValueError(
            f"Module manifest '{manifest_path}' is missing keys: {', '.join(missing_keys)}"
        )

    return manifest


def find_csharp_action_template(payload):
    for action in payload.get("data", {}).get("actions", []):
        for sub_action in action.get("subActions", []):
            if sub_action_has_csharp_code(sub_action):
                return action, sub_action

    raise ValueError(
        "No C# code block was found. Export a C# stub action with one "
        "Execute C# Code sub-action, then run this script again."
    )


def select_action_templates(payload, manifest):
    exported_actions = payload.get("data", {}).get("actions", [])
    action_template, _ = find_csharp_action_template(payload)
    templates_by_key = {
        action_template_key(action.get("name", "")): action
        for action in exported_actions
        if action_has_csharp_code(action)
    }

    return [
        templates_by_key.get(action_template_key(action["name"]), action_template)
        for action in manifest["actions"]
    ]


def action_template_key(action_name):
    name = (action_name or "").strip().lower()
    if " - " in name:
        name = name.split(" - ", 1)[1]
    return " ".join(name.split())


def action_has_csharp_code(action):
    return any(
        sub_action_has_csharp_code(sub_action)
        for sub_action in action.get("subActions", [])
    )


def validate_csharp_references(module_dir, manifest, references):
    normalized_references = {normalize_reference(reference) for reference in references}
    missing = []

    for action in manifest["actions"]:
        source_path = Path(module_dir) / action["source"]
        code = source_path.read_text(encoding="utf-8")

        for namespace in extract_using_namespaces(code):
            required_reference = required_reference_for_namespace(namespace)
            if not required_reference:
                continue

            if normalize_reference(required_reference) not in normalized_references:
                missing.append(
                    {
                        "action": action["name"],
                        "source": action["source"],
                        "namespace": namespace,
                        "reference": required_reference,
                    }
                )

    if missing:
        details = "; ".join(
            (
                f"{item['source']} ({item['action']}) uses {item['namespace']} "
                f"and requires {item['reference']}"
            )
            for item in missing
        )
        raise ValueError(
            f"Module '{manifest['id']}' is missing C# references: {details}"
        )


def extract_using_namespaces(code):
    namespaces = []
    for line in code.splitlines():
        match = re.match(
            r"^\s*using\s+(?:static\s+)?(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?"
            r"([A-Za-z_][A-Za-z0-9_.]*)\s*;",
            line,
        )
        if match:
            namespaces.append(match.group(1))
    return namespaces


def required_reference_for_namespace(namespace):
    for reference_namespace, reference in REQUIRED_REFERENCES_BY_USING.items():
        if namespace == reference_namespace or namespace.startswith(
            reference_namespace + "."
        ):
            return reference

    return None


def normalize_reference(reference):
    return str(reference).replace("/", "\\").lower()


def sub_action_has_csharp_code(sub_action):
    return any(
        isinstance(value, str)
        and (
            is_csharp_code_field(key, value)
            or is_csharp_bytecode_field(key, value)
        )
        for key, value in sub_action.items()
    )


def build_csharp_action(
    action_template,
    manifest,
    action_manifest,
    module_dir,
    source_path,
    references,
):
    action = copy.deepcopy(action_template)
    action_name = action_manifest["name"]
    action_id = deterministic_id(manifest["id"], "action:" + action_name)
    code = render_action_source(module_dir, manifest, source_path)

    action["id"] = action_id
    action["name"] = action_name
    action["group"] = manifest["group"]
    action["enabled"] = True
    action["triggers"] = []
    action["subActions"] = build_sub_actions(
        action_template,
        manifest,
        action_name,
        code,
        references,
    )
    action.setdefault("collapsedGroups", [])
    return action


def render_action_source(module_dir, manifest, source_path):
    code = Path(source_path).read_text(encoding="utf-8")
    if DEFAULT_CONFIG_PLACEHOLDER not in code:
        return code

    default_config = manifest.get("defaultConfig")
    if not default_config:
        raise ValueError(
            f"Action source '{source_path}' uses {DEFAULT_CONFIG_PLACEHOLDER} "
            f"but module '{manifest['id']}' has no defaultConfig."
        )

    config_text = (Path(module_dir) / default_config).read_text(encoding="utf-8").strip()
    json.loads(config_text)
    placeholder_literal = f'"{DEFAULT_CONFIG_PLACEHOLDER}"'
    replacement = csharp_verbatim_string(config_text)
    if placeholder_literal in code:
        return code.replace(placeholder_literal, replacement)

    return code.replace(DEFAULT_CONFIG_PLACEHOLDER, replacement)


def csharp_verbatim_string(value):
    return '@"' + value.replace('"', '""') + '"'


def build_command(manifest, command_manifest):
    command_name = command_manifest["name"]
    return {
        "caseSensitive": bool(command_manifest.get("caseSensitive", False)),
        "command": command_text(command_manifest),
        "enabled": bool(command_manifest.get("enabled", False)),
        "globalCooldown": int(command_manifest.get("globalCooldown", 0)),
        "grantType": int(command_manifest.get("grantType", 0)),
        "group": command_manifest.get("group", manifest["group"]),
        "id": deterministic_id(manifest["id"], "command:" + command_name),
        "ignoreBotAccount": bool(command_manifest.get("ignoreBotAccount", True)),
        "ignoreInternal": bool(command_manifest.get("ignoreInternal", True)),
        "include": bool(command_manifest.get("include", True)),
        "location": int(command_manifest.get("location", 0)),
        "mode": int(command_manifest.get("mode", 0)),
        "name": command_name,
        "permittedGroups": list(command_manifest.get("permittedGroups", [])),
        "permittedUsers": list(command_manifest.get("permittedUsers", [])),
        "persistCounter": bool(command_manifest.get("persistCounter", False)),
        "persistUserCounter": bool(command_manifest.get("persistUserCounter", False)),
        "regexExplicitCapture": bool(command_manifest.get("regexExplicitCapture", False)),
        "sources": command_manifest.get("sources", 1),
        "userCooldown": int(command_manifest.get("userCooldown", 0)),
    }


def command_text(command_manifest):
    if "command" in command_manifest:
        return command_manifest["command"]

    aliases = command_manifest.get("aliases", [])
    if isinstance(aliases, str):
        return aliases

    return "\r\n".join(aliases)


def attach_configured_triggers(actions, commands, manifest, trigger_templates=None):
    trigger_templates = trigger_templates or {}
    action_by_name = {action["name"]: action for action in actions}
    command_by_name = {command["name"]: command for command in commands}

    for command_manifest in manifest.get("commands", []):
        action_name = command_manifest.get("action")
        if not action_name:
            continue

        if action_name not in action_by_name:
            raise ValueError(
                f"Command '{command_manifest['name']}' references unknown action '{action_name}'."
            )

        command = command_by_name[command_manifest["name"]]
        action_by_name[action_name]["triggers"].append(
            build_command_trigger(
                manifest,
                action_name,
                command_manifest["name"],
                command,
                trigger_templates.get(401),
            )
        )

    for action_manifest in manifest["actions"]:
        action_name = action_manifest["name"]
        action = action_by_name[action_name]
        for trigger_manifest in action_manifest.get("triggers", []):
            action["triggers"].append(
                build_configured_trigger(
                    manifest,
                    action_name,
                    trigger_manifest,
                    trigger_templates,
                )
            )


def build_command_trigger(
    manifest,
    action_name,
    command_name,
    command,
    trigger_template=None,
):
    trigger = copy.deepcopy(trigger_template or {})
    trigger.update(
        {
            "commandId": command["id"],
            "enabled": True,
            "exclusions": list(trigger.get("exclusions", [])),
            "id": deterministic_id(
                manifest["id"],
                f"trigger:{action_name}:command:{command_name}",
            ),
            "type": 401,
        }
    )
    return trigger


def build_configured_trigger(
    manifest,
    action_name,
    trigger_manifest,
    trigger_templates=None,
):
    trigger_templates = trigger_templates or {}
    trigger_type = trigger_manifest["type"]
    if trigger_type == "twitch-first-words":
        trigger = copy.deepcopy(trigger_templates.get(120, {}))
        trigger.update(
            {
                "enabled": bool(trigger_manifest.get("enabled", True)),
                "exclusions": list(trigger_manifest.get("exclusions", [])),
                "id": deterministic_id(
                    manifest["id"],
                    f"trigger:{action_name}:twitch-first-words",
                ),
                "isUserId": bool(trigger_manifest.get("isUserId", False)),
                "type": 120,
                "username": trigger_manifest.get(
                    "username",
                    trigger.get("username", ""),
                ),
            }
        )
        return trigger

    if trigger_type == "twitch-stream-online":
        trigger = copy.deepcopy(trigger_templates.get(14005, {}))
        trigger.update(
            {
                "enabled": bool(trigger_manifest.get("enabled", True)),
                "exclusions": list(trigger_manifest.get("exclusions", [])),
                "id": deterministic_id(
                    manifest["id"],
                    f"trigger:{action_name}:twitch-stream-online",
                ),
                "obsId": trigger_manifest.get("obsId", trigger.get("obsId")),
                "type": 14005,
            }
        )
        return trigger

    raise ValueError(f"Unsupported trigger type '{trigger_type}' for action '{action_name}'.")


def collect_trigger_templates(payload):
    templates = {}
    for action in payload.get("data", {}).get("actions", []):
        for trigger in action.get("triggers", []):
            trigger_type = trigger.get("type")
            if trigger_type is not None and trigger_type not in templates:
                templates[trigger_type] = trigger
    return templates


def build_sub_actions(action_template, manifest, action_name, code, references):
    rebuilt_sub_actions = []
    id_map = {}
    csharp_blocks = 0

    for index, sub_action_template in enumerate(action_template.get("subActions", [])):
        sub_action = copy.deepcopy(sub_action_template)
        old_id = sub_action.get("id")
        sub_action["id"] = deterministic_id(
            manifest["id"],
            f"subaction:{action_name}:{index}:{sub_action.get('type', 'unknown')}",
        )
        sub_action["index"] = index
        sub_action["enabled"] = bool(sub_action.get("enabled", True))

        if old_id:
            id_map[old_id] = sub_action["id"]

        if sub_action_has_csharp_code(sub_action):
            csharp_blocks += 1
            for reference in references:
                ensure_reference(sub_action, reference)
            set_csharp_code(sub_action, code)

        rebuilt_sub_actions.append(sub_action)

    if csharp_blocks == 0:
        raise ValueError(f"Action template for '{action_name}' has no C# code block.")

    for sub_action in rebuilt_sub_actions:
        parent_id = sub_action.get("parentId")
        if parent_id in id_map:
            sub_action["parentId"] = id_map[parent_id]

    return rebuilt_sub_actions


def ensure_reference(sub_action, reference):
    references = sub_action.get("references")
    if not isinstance(references, list):
        references = []
        sub_action["references"] = references

    if not any(str(existing).lower() == reference.lower() for existing in references):
        references.append(reference)


def set_csharp_code(sub_action, code):
    if "byteCode" in sub_action:
        sub_action["byteCode"] = base64.b64encode(code.encode("utf-8")).decode("ascii")
        return

    for key, value in list(sub_action.items()):
        if isinstance(value, str) and is_csharp_code_field(key, value):
            sub_action[key] = code
            return

    sub_action["code"] = code


def deterministic_id(module_id, name):
    namespace = uuid.uuid5(uuid.NAMESPACE_URL, "streamerbot-module:" + module_id)
    # Streamer.bot exports use UUIDv4-shaped IDs. Keep generation deterministic for
    # reproducible artifacts while matching the version bits observed in exports.
    return str(uuid.UUID(bytes=uuid.uuid5(namespace, name).bytes, version=4))


def is_csharp_code_field(key, value):
    normalized_key = str(key).lower()
    return (
        "code" in normalized_key
        and normalized_key != "bytecode"
        and "public class CPHInline" in value
        and "public bool Execute()" in value
    )


def is_csharp_bytecode_field(key, value):
    if str(key).lower() != "bytecode":
        return False

    try:
        decoded = base64.b64decode(value).decode("utf-8")
    except (binascii.Error, UnicodeDecodeError):
        return False

    return "public class CPHInline" in decoded and "public bool Execute()" in decoded


def update_metadata(payload, manifest):
    meta = payload.setdefault("meta", {})
    meta["name"] = manifest["name"]
    meta["version"] = manifest["version"]
    meta["description"] = (
        f"Experimental {manifest['name']} import prepared from the repository "
        "Streamer.bot C# action fixture. Import into a disposable profile first, "
        "then inspect before live use."
    )

    auto_run_actions = [
        action for action in manifest["actions"] if action.get("autoRun") is True
    ]
    meta["autoRunAction"] = (
        deterministic_id(manifest["id"], "action:" + auto_run_actions[0]["name"])
        if auto_run_actions
        else None
    )


def write_bundle_data(payload, actions, commands=None):
    data = payload.setdefault("data", {})
    data["actions"] = actions
    data["commands"] = list(commands or [])
    for key in (
        "queues",
        "websocketServers",
        "websocketClients",
        "timers",
    ):
        data.setdefault(key, [])


def main():
    parser = argparse.ArgumentParser(
        description=(
            "Prepare a Streamer.bot module import from the committed C# action "
            "fixture, or from a custom exported C# action stub."
        )
    )
    parser.add_argument("module", help="Module directory containing module.json")
    parser.add_argument(
        "paths",
        nargs="+",
        help=(
            "Either OUTPUT, or INPUT OUTPUT for compatibility with older commands. "
            "When INPUT is omitted, the committed Streamer.bot C# stub fixture is used."
        ),
    )
    parser.add_argument(
        "--stub",
        default=None,
        help="Optional custom exported C# action stub .sb or .json.",
    )
    args = parser.parse_args()

    if len(args.paths) == 1:
        input_path = Path(args.stub) if args.stub else None
        output_path = Path(args.paths[0])
    elif len(args.paths) == 2:
        input_path = Path(args.paths[0])
        output_path = Path(args.paths[1])
    else:
        parser.error("expected OUTPUT or INPUT OUTPUT")

    result = prepare_module_import(
        module_dir=Path(args.module),
        input_path=input_path,
        output_path=output_path,
    )

    print(
        json.dumps(
            {
                "output": str(result.output_path),
                "replacedCodeBlocks": result.replaced_code_blocks,
                "generatedActions": result.replaced_code_blocks,
                "status": "prepared-experimental-module-import",
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
