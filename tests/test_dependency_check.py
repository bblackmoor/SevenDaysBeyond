"""Exercise real metadata, wrong binaries, and missing dependency handling."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = os.environ.get("DOTNET", "dotnet")
CHECKER = ROOT / "artifacts/tools/DependencyCheck.dll"
UPSTREAM = Path(os.environ.get("MULTIWORLD_DLL") or
                ROOT / "local-dependencies/multiworld/LITWMultiWorldProbe.dll").expanduser()


@unittest.skipUnless(UPSTREAM.is_file(), "External MultiWorld fixture absent; set MULTIWORLD_DLL")
class DependencyCheckTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        subprocess.run([sys.executable, str(ROOT / "tools/dev.py"), "check", "--reference-only", "--dotnet", DOTNET,
                        "--upstream", str(UPSTREAM.resolve())], check=True)

    def check(self, extra=(), upstream=UPSTREAM):
        with tempfile.TemporaryDirectory() as folder:
            report = Path(folder) / "report.json"
            result = subprocess.run([DOTNET, str(CHECKER), "--upstream", str(upstream),
                                     "--output", str(report), *extra], capture_output=True, text=True)
            return result, json.loads(report.read_text()) if report.exists() else None

    def test_reference_check_decodes_all_twelve_signatures(self):
        result, report = self.check(["--reference-only"])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(report["ContractOk"])
        self.assertEqual(len(report["Hooks"]), 12)
        self.assertFalse(report["DependenciesOk"])
        self.assertFalse(report["GameTested"])

    def test_strict_check_blocks_missing_dependencies(self):
        result, report = self.check()
        self.assertEqual(result.returncode, 2)
        self.assertTrue(all(d["Status"] == "missing" for d in report["Dependencies"]))

    def test_valid_but_changed_binary_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            changed = Path(folder) / "changed.dll"
            changed.write_bytes(UPSTREAM.read_bytes() + b"changed")
            result, report = self.check(["--reference-only"], changed)
        self.assertEqual(result.returncode, 2)
        self.assertFalse(report["UpstreamPinned"])
        self.assertTrue(all(h["Matches"] == 1 for h in report["Hooks"]))

    def test_unrelated_managed_binary_is_rejected(self):
        result, report = self.check(["--reference-only"], CHECKER)
        self.assertEqual(result.returncode, 2)
        self.assertFalse(report["ContractOk"])

    def test_non_managed_file_is_rejected_without_a_success_report(self):
        with tempfile.TemporaryDirectory() as folder:
            invalid = Path(folder) / "invalid.dll"
            invalid.write_bytes(b"not a managed assembly")
            result, report = self.check(["--reference-only"], invalid)
        self.assertEqual(result.returncode, 1)
        self.assertIsNone(report)


class DependencyLockTests(unittest.TestCase):
    def test_dependency_lock_rejects_changed_files(self):
        spec = importlib.util.spec_from_file_location("fetch_dependencies", ROOT / "tools/fetch-dependencies.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            item = root / "game.dll"
            item.write_bytes(b"original")
            lock = {"files": [{"path": "game.dll", "sha256": module.sha256(item)}]}
            module.verify(root, lock)
            item.write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "SHA256 mismatch"):
                module.verify(root, lock)


if __name__ == "__main__":
    unittest.main()
