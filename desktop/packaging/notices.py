"""Collect redistributed dependency notices from the exact restored packages."""
import json,pathlib,shutil,xml.etree.ElementTree as ET

def collect(root,destination):
 destination.mkdir(parents=True,exist_ok=True)
 shutil.copytree(root/'ThirdParty',destination,dirs_exist_ok=True)
 assets=json.loads((root/'obj/project.assets.json').read_text());folders=[pathlib.Path(p) for p in assets['packageFolders']];index=['Third-party components used by WirePulse. License notices below apply to those components, not to WirePulse application source.\n']
 for key,lib in sorted(assets['libraries'].items()):
  if lib.get('type')!='package':continue
  package=next((p/lib['path'] for p in folders if (p/lib['path']).exists()),None)
  if package is None:continue
  entries=[]
  for path in package.glob('*.nuspec'):
   tree=ET.parse(path)
   for tag in ['copyright','license','licenseUrl','projectUrl','repository']:
    element=next((e for e in tree.iter() if e.tag.rsplit('}',1)[-1]==tag),None)
    if element is not None:entries.append(tag+': '+(element.text or element.get('url','')))
  index.append(key+'\n'+'\n'.join(entries)+'\n')
  target=destination/key.replace('/','-')
  for path in package.rglob('*'):
   if path.is_file() and any(word in path.name.lower() for word in ['license','licence','notice','copying']) and path.suffix.lower() in ('','.txt','.md','.html','.rtf'):
    target.mkdir(exist_ok=True);shutil.copy2(path,target/str(path.relative_to(package)).replace('/','_').replace('\\','_'))
 (destination/'COMPONENTS.txt').write_text('\n'.join(index),encoding='utf-8')
