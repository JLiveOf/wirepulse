#!/usr/bin/env python3
"""Require every supported platform before any release becomes public."""
import hashlib,pathlib,sys,tarfile,zipfile
root=pathlib.Path(__file__).resolve().parent.parent
version=(root/'VERSION').read_text().strip();folder=pathlib.Path(sys.argv[1])
expected={f'WirePulse-{version}-win-x64.zip',f'WirePulse-{version}-linux-x64.tar.gz',f'wirepulse_{version}_amd64.deb',f'WirePulse-{version}-macOS-arm64.zip'}
actual={p.name for p in folder.iterdir() if p.is_file() and p.name!='SHA256SUMS.txt'}
assert actual==expected,f'Incomplete or unexpected release contents: {actual^expected}'
for name in sorted(expected):
 path=folder/name;assert path.stat().st_size>100_000,f'Empty package: {name}'
 if name.endswith('.zip'):
  with zipfile.ZipFile(path) as z:
   assert z.testzip() is None,f'Corrupt ZIP: {name}'
   if 'macOS' in name:assert 'WirePulse.app/Contents/MacOS/WirePulse' in z.namelist()
   else:assert any(n.endswith('/WirePulse.exe') for n in z.namelist())
 elif name.endswith('.tar.gz'):
  with tarfile.open(path) as t:
   exe=next(x for x in t.getmembers() if x.name.endswith('/WirePulse'));assert exe.mode&0o111,'Linux executable permission missing'
 elif name.endswith('.deb'):assert path.read_bytes()[:8]==b'!<arch>\n','Invalid Debian package'
(folder/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256((folder/name).read_bytes()).hexdigest()+'  '+name+'\n' for name in sorted(expected)))
print('PASS all four platform downloads are complete; SHA-256 manifest generated')
