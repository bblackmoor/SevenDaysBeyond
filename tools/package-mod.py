#!/usr/bin/env python3
"""Package the verified build as one folder for either client or dedicated server."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
subprocess.run([sys.executable, str(ROOT / 'tools/check-localization.py')], check=True)
report = json.loads((ROOT / 'artifacts/dependency-report.json').read_text())
if not report.get('GameSdkPinned') or not report.get('ModBuildSucceeded'):
    raise SystemExit('Build against the pinned SDK before packaging.')
mod = ROOT / 'artifacts/mod/SevenDaysBeyond'
expected = {'SevenDaysBeyond.dll','ModInfo.xml','Config/quests.xml','Config/Localization.csv'}
if {str(p.relative_to(mod)) for p in mod.rglob('*') if p.is_file()} != expected:
    raise SystemExit('Unexpected or missing build files; clean and rebuild.')
for name in expected - {'SevenDaysBeyond.dll'}:
    if (mod / name).read_bytes() != (ROOT / 'src/SevenDaysBeyond' / name).read_bytes():
        raise SystemExit('Source changed since build: ' + name)
version = ET.parse(mod / 'ModInfo.xml').getroot().find('Version').get('value')
output = ROOT / ('artifacts/SevenDaysBeyond-' + version + '.zip')
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in sorted(expected):
        archive.write(mod / name, 'SevenDaysBeyond/' + name)
digest = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix('.zip.sha256').write_text(digest + '  ' + output.name + '\n')
print(str(output))
print('SHA256=' + digest)
