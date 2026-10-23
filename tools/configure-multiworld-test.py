#!/usr/bin/env python3
"""Prepare the isolated two-map test while the baseline service is stopped."""
import datetime
import hashlib
from pathlib import Path
import shutil
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path('/srv/seven-days-beyond')
UPSTREAM_SHA256 = '946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6'
FILES = {
    'LITW_MultiWorld/LITWMultiWorldProbe.dll',
    'LITW_MultiWorld/ModInfo.xml',
    'LITW_MultiWorld/serverconfig.xml',
    'SevenDaysBeyond/SevenDaysBeyond.dll',
    'SevenDaysBeyond/ModInfo.xml',
}


def main():
    state = subprocess.run(['systemctl', 'is-active', 'seven-days-beyond.service'],
                           capture_output=True, text=True).stdout.strip()
    if state != 'inactive':
        raise SystemExit('Stop seven-days-beyond.service cleanly before preparing this test.')
    for name in ('Navezgane', 'Pregen06k01'):
        if not (ROOT / 'server/Data/Worlds' / name).is_dir():
            raise SystemExit('Missing shipped world: ' + name)
    with zipfile.ZipFile(ROOT / 'tools/SevenDaysBeyond-Server-Test.zip') as package:
        if set(package.namelist()) != FILES:
            raise SystemExit('Unexpected package contents.')
        content = {name: package.read(name) for name in FILES}
    if hashlib.sha256(content['LITW_MultiWorld/LITWMultiWorldProbe.dll']).hexdigest() != UPSTREAM_SHA256:
        raise SystemExit('Upstream DLL does not match the inspected version.')
    if (ROOT / 'config/multiworld.xml').exists() or (ROOT / 'data-multiworld').exists():
        raise SystemExit('A two-world test already exists; inspect it before changing anything.')
    for name in FILES:
        if (ROOT / 'server/Mods' / name).exists():
            raise SystemExit('Refusing to replace an existing mod file: ' + name)
    baseline_unit = Path('/etc/systemd/system/seven-days-beyond.service').read_text()
    if '-configfile=/srv/seven-days-beyond/config/baseline.xml' not in baseline_unit:
        raise SystemExit('The active service definition is not the expected baseline.')
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    backup = ROOT / 'backups' / ('baseline-before-multiworld-' + stamp + '.tar.gz')
    backup.parent.mkdir(exist_ok=True)
    with tarfile.open(backup, 'w:gz') as archive:
        for name in ('data', 'config', 'logs/server.log'):
            archive.add(ROOT / name, arcname=name)
    shutil.copy2('/etc/systemd/system/seven-days-beyond.service',
                 ROOT / 'config/seven-days-beyond.baseline.service')
    for name, data in content.items():
        path = ROOT / 'server/Mods' / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        path.chmod(0o644)
    tree = ET.parse(ROOT / 'config/baseline.xml')
    changes = {
        'ServerName': 'Seven Days Beyond - Two World Test',
        'ServerDescription': 'Disposable West and East map transfer test',
        'GameName': 'SDB_Test_West',
        'GameWorld': 'Navezgane',
        'UserDataFolder': str(ROOT / 'data-multiworld'),
    }
    for prop in tree.getroot().findall('property'):
        if prop.get('name') in changes:
            prop.set('value', changes[prop.get('name')])
    tree.write(ROOT / 'config/multiworld.xml', encoding='utf-8', xml_declaration=True)
    (ROOT / 'config/multiworld.xml').chmod(0o640)
    (ROOT / 'data-multiworld').mkdir()
    unit = baseline_unit.replace('disposable baseline dedicated server', 'disposable two-world dedicated server')
    unit = unit.replace('-configfile=/srv/seven-days-beyond/config/baseline.xml',
                        '-configfile=/srv/seven-days-beyond/config/multiworld.xml')
    unit = unit.replace('-logfile /srv/seven-days-beyond/logs/server.log',
                        '-logfile /srv/seven-days-beyond/logs/multiworld.log')
    (ROOT / 'config/seven-days-beyond.multiworld.service').write_text(unit)
    print('Backup:', backup)
    print('Installed mod files:', len(content))
    print('Prepared isolated two-world config and service; activation is a separate step.')


if __name__ == '__main__':
    main()
