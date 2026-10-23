#!/usr/bin/env python3
"""Update only Beyond's shared package on the stopped isolated ANNWN service."""
import argparse
from datetime import datetime, timezone
import hashlib
import os
from pathlib import Path
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--sha256', required=True)
    args = parser.parse_args()
    root = Path('/srv/seven-days-beyond')
    state = subprocess.check_output(['systemctl','show','seven-days-beyond.service','-p','ActiveState','--value'], text=True).strip()
    pid = subprocess.check_output(['systemctl','show','seven-days-beyond.service','-p','MainPID','--value'], text=True).strip()
    if state not in ('inactive','failed') or pid != '0':
        raise SystemExit('Stop seven-days-beyond.service cleanly first.')
    if hashlib.sha256(args.package.read_bytes()).hexdigest() != args.sha256:
        raise SystemExit('Package checksum mismatch.')
    upstream = root/'server/Mods/LITW_MultiWorld/LITWMultiWorldProbe.dll'
    if hashlib.sha256(upstream.read_bytes()).hexdigest() != '946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6':
        raise SystemExit('Unexpected upstream DLL.')
    names = {'SevenDaysBeyond.dll','ModInfo.xml','Config/quests.xml','Config/Localization.csv'}
    with zipfile.ZipFile(args.package) as archive:
        expected = {'SevenDaysBeyond/'+name for name in names}
        if set(archive.namelist()) != expected or len(archive.infolist()) != len(expected) or any(i.file_size > 1024*1024 for i in archive.infolist()):
            raise SystemExit('Unexpected archive members.')
        data = {name: archive.read('SevenDaysBeyond/'+name) for name in names}
    version = ET.fromstring(data['ModInfo.xml']).find('Version').get('value')
    if version != '0.6.0':
        raise SystemExit('Unexpected package version.')
    ET.fromstring(data['Config/quests.xml'])
    mod = root/'server/Mods/SevenDaysBeyond'
    protected = [mod/'BoundaryTest.xml', root/'server/Mods/LITW_MultiWorld/serverconfig.xml', root/'config/multiworld.xml', upstream]
    before = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    backup = root/('backups/before-shared-mod-'+stamp+'.tar.gz')
    with tarfile.open(backup, 'x:gz') as archive:
        for path in ['data-multiworld','config','server/Mods/SevenDaysBeyond','server/Mods/LITW_MultiWorld','logs/multiworld.log']:
            archive.add(root/path, arcname=path)
    print('BACKUP='+str(backup), flush=True)
    original = {name:(mod/name).read_bytes() if (mod/name).exists() else None for name in names}
    try:
        for name in sorted(names):
            dest = mod/name
            dest.parent.mkdir(parents=True, exist_ok=True)
            tmp = dest.with_suffix(dest.suffix+'.new')
            tmp.write_bytes(data[name]); tmp.chmod(0o640); os.replace(tmp, dest)
        if any(hashlib.sha256(p.read_bytes()).hexdigest() != digest for p,digest in before.items()):
            raise RuntimeError('Protected server configuration changed.')
        if any((mod/name).read_bytes() != contents for name,contents in data.items()):
            raise RuntimeError('Installed file mismatch.')
    except Exception:
        for name,contents in original.items():
            dest = mod/name
            if contents is None:
                dest.unlink(missing_ok=True)
            else:
                dest.write_bytes(contents)
        raise
    print('DEPLOYED_VERSION='+version)
    print('COMPANION_SHA256='+hashlib.sha256(data['SevenDaysBeyond.dll']).hexdigest())
    print('PROTECTED_CONFIG_UNCHANGED=True')
    print('Start seven-days-beyond.service and check native localization, all four worlds, quest roundtrip, landing, suspension and clock checks.')


if __name__ == '__main__':
    main()
