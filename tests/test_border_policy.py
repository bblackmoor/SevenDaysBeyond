"""Compile and execute production geometry/ground rules, without a mocked game API."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('dev', ROOT / 'tools/dev.py')
dev = importlib.util.module_from_spec(spec)
spec.loader.exec_module(dev)

class BorderPolicyTests(unittest.TestCase):
    def test_routes_geometry_and_ground_rejection(self):
        dotnet = os.environ.get('DOTNET', 'dotnet')
        compiler, references = dev.sdk_paths(dotnet)
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'BorderPolicyTests.dll'
            dev.compile_sources(dotnet, compiler,
                [ROOT / 'src/SevenDaysBeyond/BorderPolicy.cs', ROOT / 'src/SevenDaysBeyond/SuspensionPolicy.cs', ROOT / 'tests/BorderPolicyTests.cs'],
                list(references.glob('*.dll')), output, executable=True, nullable=False)
            output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
                'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
            result = subprocess.run([dotnet,str(output)],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
            self.assertIn('PASS',result.stdout)
            print(result.stdout.strip())
