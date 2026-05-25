#!/usr/bin/env python3
"""Build a Streamer.bot module import from a known-good C# action export."""

import argparse
import base64
import binascii
import copy
import json
import sys
import uuid
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[1]

try:
    from .sb_import_string import read_payload, write_payload
except ImportError:
    if str(SCRIPT_DIR) not in sys.path:
        sys.path.insert(0, str(SCRIPT_DIR))
    from sb_import_string import read_payload, write_payload


SYSTEM_CORE_REFERENCE = (
    "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll"
)


class PrepResult:
    def __init__(self, replaced_code_blocks, output_path):
        self.replaced_code_blocks = replaced_code_blocks
        self.output_path = Path(output_path)


def prepare_module_import(
    module_dir,
    input_path,
    output_path,
):
    module_dir = Path(module_dir)
    manifest = load_module_manifest(module_dir)
    payload = read_payload(input_path)
    prepared = copy.deepcopy(payload)
    action_template, sub_action_template = find_csharp_action_template(prepared)
    references = manifest.get("references", [])
    if SYSTEM_CORE_REFERENCE not in references:
        references.append(SYSTEM_CORE_REFERENCE)

    actions = [
        build_csharp_action(
            action_template,
            sub_action_template,
            manifest,
            action,
            module_dir / action["source"],
            references,
        )
        for action in manifest["actions"]
    ]
    replaced_code_blocks = len(actions)

    update_metadata(prepared, manifest)
    write_bundle_data(prepared, actions)
    write_payload(prepared, output_path)

    return PrepResult(replaced_code_blocks, output_path)


def prepare_scheduler_import(input_path, output_path, scheduler_code_path=None):
    del scheduler_code_path
    module_dir = REPO_ROOT / "modules" / "activity-gated-chat-announcements"
    return prepare_module_import(module_dir, input_path, output_path)


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
    sub_action_template,
    manifest,
    action_manifest,
    source_path,
    references,
):
    action = copy.deepcopy(action_template)
    action_name = action_manifest["name"]
    action_id = deterministic_id(manifest["id"], "action:" + action_name)
    sub_action_id = deterministic_id(manifest["id"], "subaction:" + action_name)
    code = Path(source_path).read_text(encoding="utf-8")

    action["id"] = action_id
    action["name"] = action_name
    action["group"] = manifest["group"]
    action["enabled"] = True
    action["triggers"] = []
    action["subActions"] = [
        build_csharp_sub_action(sub_action_template, sub_action_id, code, references)
    ]
    action["collapsedGroups"] = []
    return action


def build_csharp_sub_action(sub_action_template, sub_action_id, code, references):
    sub_action = copy.deepcopy(sub_action_template)
    sub_action["id"] = sub_action_id
    sub_action["index"] = 0
    sub_action["enabled"] = True
    for reference in references:
        ensure_reference(sub_action, reference)
    set_csharp_code(sub_action, code)
    return sub_action


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
    return str(uuid.uuid5(namespace, name))


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
        f"Experimental {manifest['name']} import prepared from a known-good local "
        "Streamer.bot C# action export. Import into a disposable profile first, "
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


def write_bundle_data(payload, actions):
    data = payload.setdefault("data", {})
    data["actions"] = actions
    for key in (
        "queues",
        "commands",
        "websocketServers",
        "websocketClients",
        "timers",
    ):
        data.setdefault(key, [])


def main():
    parser = argparse.ArgumentParser(
        description=(
            "Prepare a Streamer.bot module import by cloning a known-good "
            "exported C# action."
        )
    )
    parser.add_argument("module", help="Module directory containing module.json")
    parser.add_argument("input", help="Known-good exported C# action stub .sb or .json")
    parser.add_argument("output", help="Prepared output .sb or .json")
    args = parser.parse_args()

    result = prepare_module_import(
        module_dir=Path(args.module),
        input_path=Path(args.input),
        output_path=Path(args.output),
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
