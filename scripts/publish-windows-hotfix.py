import json,pathlib,urllib.request,urllib.parse,zipfile,hashlib,sys
import os,subprocess
repo=pathlib.Path(__file__).resolve().parent.parent
base='repos/JLiveOf/wirepulse';run=sys.argv[1];target=pathlib.Path(sys.argv[2]);token=os.environ['GH_TOKEN']
def api(path,data=None,method=None):
 req=urllib.request.Request('https://api.github.com/'+path,data=None if data is None else json.dumps(data).encode(),method=method,headers={'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json','User-Agent':'WirePulse-hotfix'})
 with urllib.request.urlopen(req,timeout=60) as response:
  payload=response.read()
  return json.loads(payload) if payload else None
r=api(f'{base}/actions/runs/{run}')
assert r['status']=='completed' and r['conclusion']=='success' and r['name']=='Windows hotfix validation'
commit=r['head_sha'];version=(repo/'VERSION').read_text().strip()
subprocess.run(['git','diff','--exit-code',commit,'HEAD','--','desktop'],cwd=repo,check=True)
release=api(f'{base}/releases/tags/v{version}');assert not release['draft'] and not release['prerelease']
old=current={a['name']:a for a in release['assets']}
assert len(current)==8 and 'BUILD-INFO.json' in current
names=[f'WirePulse-{version}-win-x64-Setup.exe',f'WirePulse-{version}-win-x64.zip']
checks={}
for line in (target/'SHA256SUMS.txt').read_text().splitlines():
 digest,name=line.split(maxsplit=1);checks[name.lstrip('*')]=digest
assert set(checks)==set(names)
for name in names:
 assert hashlib.sha256((target/name).read_bytes()).hexdigest()==checks[name]
assert (target/names[0]).read_bytes()[:2]==b'MZ'
with zipfile.ZipFile(target/names[1]) as z:
 assert z.testzip() is None
 assert any(x.endswith('/WindowsSettings/WirePulse.Settings.exe') for x in z.namelist())
 build=json.loads(z.read(next(x for x in z.namelist() if x.endswith('/Assets/build-info.json'))))
 assert build['id']==commit and build['version']==version and build['sequence']>0
metadata=urllib.request.urlopen(current['BUILD-INFO.json']['browser_download_url']).read()
assert 'sha256:'+hashlib.sha256(metadata).hexdigest()==current['BUILD-INFO.json']['digest']
builds=json.loads(metadata);assert set(builds)=={'windows','linux','linux-deb','macos'}
assert build['id']!=builds['windows']['id'] and build['sequence']>builds['windows']['sequence'],'Hotfix must have a newer source identity'
builds['windows']=build
(target/'BUILD-INFO.json').write_text(json.dumps(builds,indent=2)+'\n')
checks['BUILD-INFO.json']=hashlib.sha256((target/'BUILD-INFO.json').read_bytes()).hexdigest()
manifest=urllib.request.urlopen(current['SHA256SUMS.txt']['browser_download_url']).read().decode()
updated=[]
for line in manifest.splitlines():
 digest,name=line.split(maxsplit=1);name=name.lstrip('*');assert current[name]['digest']=='sha256:'+digest
 updated.append(checks.get(name,digest)+'  '+name)
assert len(updated)==7
(target/'SHA256SUMS.txt').write_text('\n'.join(updated)+'\n')
print('Validated Windows packages; other release digests preserved.',flush=True)
for name in names+['BUILD-INFO.json','SHA256SUMS.txt']:
 api(f"{base}/releases/assets/{current[name]['id']}",method='DELETE')
 payload=(target/name).read_bytes();url=release['upload_url'].split('{')[0]+'?name='+urllib.parse.quote(name)
 req=urllib.request.Request(url,data=payload,method='POST',headers={'Authorization':'Bearer '+token,'Content-Type':'application/octet-stream','User-Agent':'WirePulse-hotfix'})
 with urllib.request.urlopen(req,timeout=180) as response:uploaded=json.load(response)
 assert uploaded['digest']=='sha256:'+hashlib.sha256(payload).hexdigest()
 print('Replaced',name,flush=True)
notes=release['body']+f'\n\n### Windows {version} hotfix\n\nWindows-only hotfix. The updater can detect this newer build even when the version number stays the same. Windows downloads, their build identity and checksums were replaced; macOS and Linux packages are unchanged.\n\nHotfix source: https://github.com/JLiveOf/wirepulse/commit/{commit}\n'
api(f"{base}/releases/{release['id']}",{'body':notes},'PATCH')
after=api(f'{base}/releases/tags/v{version}');assets={a['name']:a for a in after['assets']}
assert len(assets)==8
for name in old:
 if name not in names+['BUILD-INFO.json','SHA256SUMS.txt']:assert assets[name]['id']==old[name]['id'] and assets[name]['digest']==old[name]['digest']
print('Verified: only Windows downloads, manifest and hotfix notes changed.',flush=True)
