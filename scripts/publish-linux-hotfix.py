import json,pathlib,urllib.request,urllib.parse,tarfile,hashlib,sys
import os,subprocess
repo=pathlib.Path(__file__).resolve().parent.parent
base='repos/JLiveOf/wirepulse';run=sys.argv[1];target=pathlib.Path(sys.argv[2]);token=os.environ['GH_TOKEN']
def api(path,data=None,method=None):
 req=urllib.request.Request('https://api.github.com/'+path,data=None if data is None else json.dumps(data).encode(),method=method,headers={'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json','User-Agent':'WirePulse-hotfix'})
 with urllib.request.urlopen(req,timeout=60) as response:
  payload=response.read()
  return json.loads(payload) if payload else None
r=api(f'{base}/actions/runs/{run}')
assert r['status']=='completed' and r['conclusion']=='success' and r['name']=='Linux native UI hotfix validation'
commit=r['head_sha'];version=(repo/'VERSION').read_text().strip()
subprocess.run(['git','diff','--exit-code',commit,'HEAD','--','desktop'],cwd=repo,check=True)
release=api(f'{base}/releases/tags/v{version}');assert not release['draft'] and not release['prerelease']
old=current={a['name']:a for a in release['assets']}
assert len(current)==8 and 'BUILD-INFO.json' in current
names=[f'WirePulse-{version}-linux-x64.tar.gz',f'wirepulse_{version}_amd64.deb']
checks={}
for line in (target/'SHA256SUMS.txt').read_text().splitlines():
 digest,name=line.split(maxsplit=1);checks[name.lstrip('*')]=digest
assert set(checks)==set(names)
for name in names:
 assert hashlib.sha256((target/name).read_bytes()).hexdigest()==checks[name]
assert (target/names[1]).read_bytes()[:8]==b'!<arch>\n'
with tarfile.open(target/names[0]) as archive:
 members=archive.getmembers()
 assert any(x.name.endswith('/LinuxUI/native.py') for x in members)
 build=json.load(archive.extractfile(next(x for x in members if x.name.endswith('/Assets/build-info.json'))))
 assert build['id']==commit and build['version']==version and build['sequence']>0
 dependencies=subprocess.check_output(['dpkg-deb','--field',str(target/names[1]),'Depends'],text=True)
 assert all(x in dependencies for x in ['python3-gi','python3-gi-cairo','gir1.2-gtk-3.0','librsvg2-common'])
metadata=urllib.request.urlopen(current['BUILD-INFO.json']['browser_download_url']).read()
assert 'sha256:'+hashlib.sha256(metadata).hexdigest()==current['BUILD-INFO.json']['digest']
builds=json.loads(metadata);assert set(builds)=={'windows','linux','linux-deb','macos'}
assert all(build['id']!=builds[p]['id'] and build['sequence']>builds[p]['sequence'] for p in ['linux','linux-deb']),'Hotfix must have a newer source identity'
builds['linux']=build;builds['linux-deb']=build
(target/'BUILD-INFO.json').write_text(json.dumps(builds,indent=2)+'\n')
checks['BUILD-INFO.json']=hashlib.sha256((target/'BUILD-INFO.json').read_bytes()).hexdigest()
manifest=urllib.request.urlopen(current['SHA256SUMS.txt']['browser_download_url']).read().decode()
updated=[]
for line in manifest.splitlines():
 digest,name=line.split(maxsplit=1);name=name.lstrip('*');assert current[name]['digest']=='sha256:'+digest
 updated.append(checks.get(name,digest)+'  '+name)
assert len(updated)==7
(target/'SHA256SUMS.txt').write_text('\n'.join(updated)+'\n')
print('Validated Linux packages; other release digests preserved.',flush=True)
for name in names+['BUILD-INFO.json','SHA256SUMS.txt']:
 api(f"{base}/releases/assets/{current[name]['id']}",method='DELETE')
 payload=(target/name).read_bytes();url=release['upload_url'].split('{')[0]+'?name='+urllib.parse.quote(name)
 req=urllib.request.Request(url,data=payload,method='POST',headers={'Authorization':'Bearer '+token,'Content-Type':'application/octet-stream','User-Agent':'WirePulse-hotfix'})
 with urllib.request.urlopen(req,timeout=180) as response:uploaded=json.load(response)
 assert uploaded['digest']=='sha256:'+hashlib.sha256(payload).hexdigest()
 print('Replaced',name,flush=True)
notes=release['body']+f'\n\n### Linux {version} hotfix\n\nLinux-only hotfix: native GTK 3 settings, editor, welcome and release highlights with Mint/Cinnamon theme support, category icons, native file/color/font pickers and readable release highlights. Tested against Cinnamon, the official Mint-Y theme and the actual renderer. The updater detects the new Linux build without changing the version number. Windows and macOS packages are unchanged.\n\nHotfix source: https://github.com/JLiveOf/wirepulse/commit/{commit}\n'
api(f"{base}/releases/{release['id']}",{'body':notes},'PATCH')
after=api(f'{base}/releases/tags/v{version}');assets={a['name']:a for a in after['assets']}
assert len(assets)==8
for name in old:
 if name not in names+['BUILD-INFO.json','SHA256SUMS.txt']:assert assets[name]['id']==old[name]['id'] and assets[name]['digest']==old[name]['digest']
print('Verified: only Linux downloads, manifest and hotfix notes changed.',flush=True)
