#!/usr/bin/env python3
"""Deploy the four-world/quest test package on the stopped isolated ANNWN service."""
import argparse
from datetime import datetime, timezone
import hashlib
import os
from pathlib import Path
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--package',type=Path,required=True)
p.add_argument('--sha256',required=True)
a=p.parse_args()
root=Path('/srv/seven-days-beyond')
state=subprocess.check_output(['systemctl','show','seven-days-beyond.service','-p','ActiveState','--value'],text=True).strip()
pid=subprocess.check_output(['systemctl','show','seven-days-beyond.service','-p','MainPID','--value'],text=True).strip()
if state not in ('inactive','failed') or pid!='0': raise SystemExit('Stop the test service cleanly first.')
if hashlib.sha256(a.package.read_bytes()).hexdigest()!=a.sha256: raise SystemExit('Package checksum mismatch.')
if hashlib.sha256((root/'server/Mods/LITW_MultiWorld/LITWMultiWorldProbe.dll').read_bytes()).hexdigest()!='946e95cc22a2e27a1d7e8e2114554966f97970d42042088cee3577dbb6ce5bd6': raise SystemExit('Unexpected upstream DLL.')
expected={'SevenDaysBeyond/SevenDaysBeyond.dll','SevenDaysBeyond/ModInfo.xml','SevenDaysBeyond/BoundaryTest.xml','SevenDaysBeyond/Config/quests.xml','LITW_MultiWorld/serverconfig.xml'}
with zipfile.ZipFile(a.package) as z:
    if set(z.namelist())!=expected or len(z.infolist())!=len(expected) or any(v.file_size>1024*1024 for v in z.infolist()): raise SystemExit('Unexpected archive members.')
    data={n:z.read(n) for n in expected}
    multi=ET.fromstring(data['LITW_MultiWorld/serverconfig.xml'])
    boundary=ET.fromstring(data['SevenDaysBeyond/BoundaryTest.xml'])
    worlds=multi.findall('World')
    if {w.get('name') for w in worlds}!={'West','East','NorthWest','NorthEast'} or len(worlds)!=4: raise SystemExit('Unexpected test worlds.')
    cells=boundary.findall('Grid/Cell')
    if {(c.get('name'),c.get('x'),c.get('z')) for c in cells}!={('West','0','0'),('East','1','0'),('NorthWest','0','1'),('NorthEast','1','1')}: raise SystemExit('Unexpected grid.')
    if multi.get('storageId')!='SevenDaysBeyond_Test': raise SystemExit('Wrong isolated save namespace.')
    for w in worlds:
        props={v.get('name'):v.get('value') for v in w.findall('property')}
        if props.get('GameName')!='SDB_Test_'+w.get('name') or not (root/'server/Data/Worlds'/props['GameWorld']).is_dir(): raise SystemExit('Missing map or unexpected save.')
    # Preserve the existing two map identities and saves exactly.
    previous=ET.parse(root/'server/Mods/LITW_MultiWorld/serverconfig.xml').getroot()
    for old in previous.findall('World'):
        new=next((w for w in worlds if w.get('name')==old.get('name')),None)
        if new is None or old.attrib!=new.attrib: raise SystemExit('Existing world identity changed.')
        before={v.get('name'):v.get('value') for v in old.findall('property')}
        after={v.get('name'):v.get('value') for v in new.findall('property')}
        if before!=after: raise SystemExit('Existing world properties changed.')
    stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    backup=root/('backups/before-grid-quests-'+stamp+'.tar.gz')
    with tarfile.open(backup,'x:gz') as t:
        for name in ['data-multiworld','config','server/Mods/SevenDaysBeyond','server/Mods/LITW_MultiWorld','logs/multiworld.log']:
            t.add(root/name,arcname=name)
    print('BACKUP='+str(backup),flush=True)
    for name in sorted(expected):
        dest=root/'server/Mods'/name
        dest.parent.mkdir(parents=True,exist_ok=True)
        tmp=dest.with_suffix(dest.suffix+'.new');tmp.write_bytes(data[name]);tmp.chmod(0o640);os.replace(tmp,dest)
    config=ET.parse(root/'config/multiworld.xml')
    for prop in config.getroot().findall('property'):
        if prop.get('name')=='ServerName': prop.set('value','Seven Days Beyond - Grid Test')
        elif prop.get('name')=='ServerDescription': prop.set('value','Four connected maps with quest testing')
    config.write(root/'config/multiworld.xml',encoding='utf-8',xml_declaration=True)
    print('COMPANION_SHA256='+hashlib.sha256((root/'server/Mods/SevenDaysBeyond/SevenDaysBeyond.dll').read_bytes()).hexdigest())
print('DEPLOYED; start the exact test service and verify four-world readiness, quest roundtrip, landing, suspension and serialized clock checks.')
