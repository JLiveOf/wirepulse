#!/usr/bin/env python3
"""Require every supported platform before any release becomes public."""
import hashlib,pathlib,sys,tarfile,zipfile
root=pathlib.Path(__file__).resolve().parent.parent
version=(root/'VERSION').read_text().strip();folder=pathlib.Path(sys.argv[1])
expected={f'WirePulse-{version}-win-x64-Setup.exe',f'WirePulse-{version}-win-x64.zip',f'WirePulse-{version}-linux-x64.tar.gz',f'wirepulse_{version}_amd64.deb',f'WirePulse-{version}-macOS-arm64.zip'}
actual={p.name for p in folder.iterdir() if p.is_file() and p.name!='SHA256SUMS.txt'}
assert actual==expected,f'Incomplete or unexpected release contents: {actual^expected}'
for name in sorted(expected):
 path=folder/name;assert path.stat().st_size>100_000,f'Empty package: {name}'
 if name.endswith('.exe'):assert path.read_bytes()[:2]==b'MZ','Invalid Windows installer'
 elif name.endswith('.zip'):
  with zipfile.ZipFile(path) as z:
   assert z.testzip() is None,f'Corrupt ZIP: {name}'
   if 'macOS' in name:
    assert 'WirePulse.app/Contents/MacOS/WirePulse' in z.namelist()
    for resource in ['Setup-intro.wav','Setup-loop.wav','whats-new.json']:assert 'WirePulse.app/Contents/Resources/'+resource in z.namelist(), 'Missing macOS resource: '+resource
    assert 'WirePulse.app/Contents/Resources/Welcome.wav' in z.namelist(), 'Missing macOS welcome audio'
   else:
    assert any(n.endswith('/WirePulse.exe') for n in z.namelist())
    for resource in ['Setup-intro.wav','Setup-loop.wav','whats-new.json']:assert any(n.endswith('/Assets/'+resource) for n in z.namelist()), 'Missing Windows resource: '+resource
    assert any(n.endswith('/Assets/Welcome.wav') for n in z.namelist()), 'Missing Windows welcome audio'
    for required in ['WindowsSettings/WirePulse.Settings.exe','WindowsSettings/WirePulse.Settings.pri']:
     assert any(n.endswith('/'+required) for n in z.namelist()),f'Missing WinUI companion resource: {required}'
 elif name.endswith('.tar.gz'):
  with tarfile.open(path) as t:
   for resource in ['Setup-intro.wav','Setup-loop.wav','whats-new.json']:assert any(x.name.endswith('/Assets/'+resource) for x in t.getmembers()), 'Missing Linux resource: '+resource
   assert any(x.name.endswith('/Assets/Welcome.wav') for x in t.getmembers()), 'Missing Linux welcome audio'
   exe=next(x for x in t.getmembers() if x.name.endswith('/WirePulse'));assert exe.mode&0o111,'Linux executable permission missing'
 elif name.endswith('.deb'):assert path.read_bytes()[:8]==b'!<arch>\n','Invalid Debian package'
(folder/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256((folder/name).read_bytes()).hexdigest()+'  '+name+'\n' for name in sorted(expected)))
print('PASS all five platform downloads are complete; SHA-256 manifest generated')
