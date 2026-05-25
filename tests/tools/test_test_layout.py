import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class TestLayoutTest(unittest.TestCase):
    def test_module_specific_tests_are_colocated_with_modules(self):
        module_dirs = sorted(
            path
            for path in (ROOT / "modules").iterdir()
            if path.is_dir() and (path / "module.json").is_file()
        )

        self.assertGreater(len(module_dirs), 0)
        self.assertFalse((ROOT / "tests" / "modules").exists())

        for module_dir in module_dirs:
            with self.subTest(module=module_dir.name):
                module_tests = module_dir / "tests"
                self.assertTrue(module_tests.is_dir())
                self.assertTrue(any(module_tests.rglob("test*.py")))


if __name__ == "__main__":
    unittest.main()
