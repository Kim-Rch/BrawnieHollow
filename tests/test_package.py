from pathlib import Path
import json, zipfile
root=Path(__file__).resolve().parents[1]
p=root/'BrawnieHollow'/'bin'/'Release'/'net6.0'/'BrawnieHollow 0.1.0.zip'
with zipfile.ZipFile(p) as z:
    expected={'manifest.json','BrawnieHollow.dll','README.md','assets/Bram.png','assets/Mira.png','assets/Pip.png','assets/hollow.tmx'}
    entries={n.removeprefix('BrawnieHollow/') for n in z.namelist()}
    assert entries==expected, f'package mismatch: missing={expected-entries}, unexpected={entries-expected}'
    manifest=json.loads(z.read('BrawnieHollow/manifest.json'))
    assert manifest['UniqueID']=='RachmanNurhakim.BrawnieHollow'
    assert manifest['Author']=='Rachman Nurhakim'
    assert manifest['EntryDll']=='BrawnieHollow.dll'
    assert z.read('BrawnieHollow/BrawnieHollow.dll')[:2]==b'MZ'
    assert z.testzip() is None
    for n in entries:
        local=root/'README.md' if n=='README.md' else root/'BrawnieHollow'/n
        if n=='BrawnieHollow.dll': local=root/'BrawnieHollow'/'bin'/'Release'/'net6.0'/n
        assert z.read('BrawnieHollow/'+n)==local.read_bytes(), f'stale package: {n}'
print('PASS: release package contains exact manifest, current DLL, README and all four runtime assets')
