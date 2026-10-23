"""Exercise production message codec and localization catalog validation."""
import csv
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, file)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


dev = module('dev', ROOT / 'tools/dev.py')
validator = module('localization', ROOT / 'tools/check-localization.py')


class LocalizationTests(unittest.TestCase):
    def test_wire_and_client_languages(self):
        dotnet = os.environ.get('DOTNET', 'dotnet')
        compiler, refs = dev.sdk_paths(dotnet)
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'LocalizedTextTests.dll'
            dev.compile_sources(dotnet, compiler,
                                [ROOT/'src/SevenDaysBeyond/LocalizedText.cs', ROOT/'tests/LocalizedTextTests.cs'],
                                list(refs.glob('*.dll')), output, executable=True, nullable=False)
            output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
                'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
            result = subprocess.run([dotnet, str(output)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('PASS', result.stdout)
            print(result.stdout.strip())

    def test_catalog_and_sources(self):
        rows = validator.validate(ROOT/'src/SevenDaysBeyond/Config/Localization.csv', ROOT/'src/SevenDaysBeyond')
        self.assertGreater(len(rows), 50)
        self.assertTrue(all(row['spanish'] and row['latam'] for row in rows.values()))

    def test_translator_errors_and_partial_translation(self):
        with tempfile.TemporaryDirectory() as folder:
            file = Path(folder) / 'Localization.csv'
            def write(rows):
                with file.open('w', newline='') as f:
                    csv.writer(f).writerows([['Key','english','spanish']] + rows)
            write([['sdb_example','Hello {0}, {1}','Hola {1}, {0}']])
            validator.validate(file)
            write([['sdb_example','Hello {0}','']])
            validator.validate(file)
            for rows in [
                    [['sdb_example','Hello {0}','Hola']],
                    [['sdb_example','Hello {0}','Hola {1}']],
                    [['sdb_example','','Hola']],
                    [['sdb_example','Hello','Hola'], ['sdb_example','Duplicate','Repetido']],
                    [['sdb_example','Hello {name}','Hola {name}']],
                    [['sdb_example','Hello {0','Hola']]]:
                write(rows)
                with self.assertRaises(ValueError): validator.validate(file)
