#!/usr/bin/env python3
"""Run shared and module-local unit tests."""

import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def module_test_dirs(repo_root=REPO_ROOT):
    modules_root = Path(repo_root) / "modules"
    return sorted(
        module_dir / "tests"
        for module_dir in modules_root.iterdir()
        if module_dir.is_dir()
        and (module_dir / "module.json").is_file()
        and (module_dir / "tests").is_dir()
    )


def run_command(command):
    print("+ " + " ".join(str(part) for part in command), flush=True)
    return subprocess.run(command, cwd=REPO_ROOT, check=False).returncode


def main():
    commands = [
        [
            sys.executable,
            "-B",
            "-m",
            "unittest",
            "discover",
            "-s",
            "tests",
            "-t",
            ".",
        ]
    ]

    for test_dir in module_test_dirs():
        commands.append(
            [
                sys.executable,
                "-B",
                "-m",
                "unittest",
                "discover",
                "-s",
                str(test_dir.relative_to(REPO_ROOT)),
            ]
        )

    for command in commands:
        return_code = run_command(command)
        if return_code != 0:
            return return_code

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
