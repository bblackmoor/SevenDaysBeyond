#!/usr/bin/env python3
"""Deploy the opt-in border companion on the stopped ANNWN disposable test server."""
import argparse
from datetime import datetime, timezone
import hashlib
import os
from pathlib import Path
import subprocess
import tarfile
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--package', type=Path, required=True)
parser.add_argument('--sha256', required=True)
args = parser.parse_args()
root = Path('/srv/seven-days-beyond')
state = subprocess.check_output(['systemctl','show','seven-days-beyond.service','-p','ActiveState','--value'],text=True).strip()
if state != 'inactive':
    raise SystemExit('Stop seven-days-beyond.service cleanly first.')
package = args.package.read_bytes()
if hashlib.sha256(package).hexdigest() != args.sha256:
    raise SystemExit('Package checksum mismatch.')
upstream = root / 'server/Mods/LITW_MultiWorld/LITWMultiWorldProbe.dll'
if hashlib.sha256(upstream.read_bytes()).hexdigest() != '946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6':
    raise SystemExit('Unexpected upstream version.')
expected = {'SevenDaysBeyond/SevenDaysBeyond.dll','SevenDaysBeyond/ModInfo.xml','SevenDaysBeyond/BoundaryTest.xml'}
with zipfile.ZipFile(args.package) as archive:
    if set(archive.namelist()) != expected or any(i.file_size > 1024*1024 for i in archive.infolist()):
        raise SystemExit('Unexpected package contents.')
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    backup = root / ('backups/before-border-travel-' + stamp + '.tar.gz')
    with tarfile.open(backup, 'x:gz') as output:
        for name in ['data-multiworld','config','server/Mods/SevenDaysBeyond','logs/multiworld.log']:
            output.add(root/name,arcname=name)
    print('BACKUP=' + str(backup),flush=True)
    for name in sorted(expected):
        destination = root/'server/Mods'/name
        temporary = destination.with_suffix(destination.suffix + '.new')
        temporary.write_bytes(archive.read(name))
        temporary.chmod(0o640)
        os.replace(temporary,destination)
    print('COMPANION_SHA256=' + hashlib.sha256((root/'server/Mods/SevenDaysBeyond/SevenDaysBeyond.dll').read_bytes()).hexdigest())
print('DEPLOYED. Start seven-days-beyond.service and inspect BORDER_TRAVEL_ENABLED, BORDER_BOUNDS, and BORDER_SAFETY_PROBE.')
