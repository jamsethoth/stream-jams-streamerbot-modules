#!/usr/bin/env python3
"""Build all Streamer.bot module import artifacts deterministically."""

import argparse
import hashlib
import json
import shutil
import zipfile
from dataclasses import dataclass
from pathlib import Path

try:
    from . import build_module_import
except ImportError:
    import build_module_import


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[1]
DEFAULT_STUB_PATH = build_module_import.DEFAULT_STUB_PATH
DEFAULT_OUTPUT_ROOT = REPO_ROOT / "dist" / "modules"
REQUIRED_README_SECTIONS = (
    "## What It Does",
    "## Installation",
    "## Configuration",
    "## Generated Actions",
)
ZIP_TIMESTAMP = (1980, 1, 1, 0, 0, 0)


@dataclass(frozen=True)
class ModuleBuildSummary:
    module_id: str
    name: str
    version: str
    action_count: int
    output_dir: Path
    import_path: Path
    import_sha256: str


@dataclass(frozen=True)
class BuildAllResult:
    output_root: Path
    modules: list[ModuleBuildSummary]


def discover_module_dirs(repo_root):
    modules_root = Path(repo_root) / "modules"
    if not modules_root.is_dir():
        raise ValueError(f"Modules directory does not exist: {modules_root}")

    return sorted(
        path
        for path in modules_root.iterdir()
        if path.is_dir() and (path / "module.json").is_file()
    )


def validate_module_readme(module_dir):
    readme_path = Path(module_dir) / "README.md"
    if not readme_path.is_file():
        raise ValueError(f"Module is missing README.md: {module_dir}")

    readme = readme_path.read_text(encoding="utf-8")
    missing_sections = [
        section for section in REQUIRED_README_SECTIONS if section not in readme
    ]
    if missing_sections:
        raise ValueError(
            f"Module README '{readme_path}' is missing required sections: "
            + ", ".join(missing_sections)
        )


def validate_module_sources(module_dir, manifest):
    module_dir = Path(module_dir)
    source_paths = [action.get("source") for action in manifest["actions"]]
    missing_sources = [
        str(source_path)
        for source_path in source_paths
        if not source_path or not (module_dir / source_path).is_file()
    ]

    default_config = manifest.get("defaultConfig")
    if default_config and not (module_dir / default_config).is_file():
        missing_sources.append(default_config)

    if missing_sources:
        raise ValueError(
            f"Module '{manifest['id']}' references missing files: "
            + ", ".join(missing_sources)
        )


def build_all_modules(
    repo_root=REPO_ROOT,
    output_root=DEFAULT_OUTPUT_ROOT,
    stub_path=DEFAULT_STUB_PATH,
):
    repo_root = Path(repo_root)
    output_root = Path(output_root)
    stub_path = Path(stub_path)
    ensure_safe_output_root(output_root, repo_root)

    if not stub_path.is_file():
        raise ValueError(f"Streamer.bot C# stub fixture does not exist: {stub_path}")

    if output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    summaries = []
    for module_dir in discover_module_dirs(repo_root):
        manifest = build_module_import.load_module_manifest(module_dir)
        validate_module_readme(module_dir)
        validate_module_sources(module_dir, manifest)

        module_id = manifest["id"]
        module_output_dir = output_root / module_id
        module_output_dir.mkdir(parents=True, exist_ok=True)

        import_path = module_output_dir / f"{module_id}.sb"
        import_text_path = module_output_dir / f"{module_id}.import.txt"
        module_manifest_path = module_output_dir / "module.json"
        readme_path = module_output_dir / "README.md"
        artifact_manifest_path = module_output_dir / "manifest.json"

        module_stub_path = resolve_module_stub_path(repo_root, manifest, stub_path)
        result = build_module_import.prepare_module_import(
            module_dir=module_dir,
            input_path=module_stub_path,
            output_path=import_path,
        )
        assert_generated_import(import_path, manifest)
        import_text_path.write_bytes(import_path.read_bytes())
        shutil.copyfile(module_dir / "README.md", readme_path)
        shutil.copyfile(module_dir / "module.json", module_manifest_path)

        import_sha256 = sha256_file(import_path)
        artifact_manifest = {
            "actionCount": len(manifest["actions"]),
            "description": manifest["description"],
            "generatedActions": result.replaced_code_blocks,
            "importFile": import_path.name,
            "importSha256": import_sha256,
            "importTextFile": import_text_path.name,
            "moduleId": module_id,
            "moduleManifestFile": module_manifest_path.name,
            "name": manifest["name"],
            "readmeFile": readme_path.name,
            "version": manifest["version"],
        }
        write_json(artifact_manifest_path, artifact_manifest)

        summaries.append(
            ModuleBuildSummary(
                module_id=module_id,
                name=manifest["name"],
                version=manifest["version"],
                action_count=len(manifest["actions"]),
                output_dir=module_output_dir,
                import_path=import_path,
                import_sha256=import_sha256,
            )
        )

    return BuildAllResult(output_root=output_root, modules=summaries)


def resolve_module_stub_path(repo_root, manifest, default_stub_path):
    import_stub = manifest.get("importStub")
    if not import_stub:
        return Path(default_stub_path)

    module_stub_path = Path(repo_root) / import_stub
    if not module_stub_path.is_file():
        raise ValueError(
            f"Module '{manifest['id']}' references missing import stub: {import_stub}"
        )

    return module_stub_path


def create_release_archive(dist_root, archive_path):
    dist_root = Path(dist_root)
    archive_path = Path(archive_path)
    archive_path.parent.mkdir(parents=True, exist_ok=True)

    file_entries = {
        relative_posix(path, dist_root): path.read_bytes()
        for path in sorted(dist_root.rglob("*"))
        if path.is_file()
    }
    checksum_lines = [
        f"{hashlib.sha256(data).hexdigest()}  {name}"
        for name, data in sorted(file_entries.items())
    ]
    archive_entries = {
        **file_entries,
        "SHA256SUMS": ("\n".join(checksum_lines) + "\n").encode("utf-8"),
    }

    with zipfile.ZipFile(
        archive_path,
        mode="w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        for name, data in sorted(archive_entries.items()):
            info = zipfile.ZipInfo(filename=name, date_time=ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            archive.writestr(info, data)


def ensure_safe_output_root(output_root, repo_root):
    output_root = Path(output_root).resolve()
    repo_root = Path(repo_root).resolve()
    forbidden_paths = {
        repo_root,
        repo_root / ".git",
        repo_root / ".github",
        repo_root / "modules",
        repo_root / "tests",
        repo_root / "tools",
    }

    for forbidden_path in forbidden_paths:
        if output_root == forbidden_path or output_root.is_relative_to(
            forbidden_path
        ):
            raise ValueError(f"Refusing to clean output path: {output_root}")


def assert_generated_import(import_path, manifest):
    payload = build_module_import.read_payload(import_path)
    actions = payload.get("data", {}).get("actions", [])
    if len(actions) != len(manifest["actions"]):
        raise ValueError(
            f"Generated import '{import_path}' has {len(actions)} actions; "
            f"expected {len(manifest['actions'])} from module.json."
        )


def relative_posix(path, root):
    return str(Path(path).relative_to(root)).replace("\\", "/")


def sha256_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def summarize_result(result, archive_path=None):
    return {
        "archive": str(archive_path) if archive_path else None,
        "moduleCount": len(result.modules),
        "modules": [
            {
                "actionCount": module.action_count,
                "import": str(module.import_path),
                "importSha256": module.import_sha256,
                "moduleId": module.module_id,
                "name": module.name,
                "version": module.version,
            }
            for module in result.modules
        ],
        "outputRoot": str(result.output_root),
    }


def main():
    parser = argparse.ArgumentParser(
        description="Build deterministic Streamer.bot import artifacts for all modules."
    )
    parser.add_argument("--repo-root", default=REPO_ROOT)
    parser.add_argument("--output", default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--stub", default=DEFAULT_STUB_PATH)
    parser.add_argument("--archive", default=None)
    args = parser.parse_args()

    result = build_all_modules(
        repo_root=Path(args.repo_root),
        output_root=Path(args.output),
        stub_path=Path(args.stub),
    )

    archive_path = Path(args.archive) if args.archive else None
    if archive_path:
        create_release_archive(result.output_root, archive_path)

    print(json.dumps(summarize_result(result, archive_path), indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
