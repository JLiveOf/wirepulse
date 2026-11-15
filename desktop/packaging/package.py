#!/usr/bin/env python3
import sys,pathlib,shutil,zipfile,tarfile,subprocess,hashlib
rid,version=sys.argv[1:3]
root=pathlib.Path(__file__).resolve().parent.parent
output=root/'packages';output.mkdir(exist_ok=True)
name=f'WirePulse-{version}-{rid}'
stage=output/name
if stage.exists():shutil.rmtree(stage)
shutil.copytree(root/'publish',stage)
shutil.copy2(root/'README.md',stage/'README.md')
from notices import collect
collect(root,stage/'ThirdPartyNotices')
if rid.startswith('win'):
 (stage/'WirePulse-SafeMode.cmd').write_text('@echo off\r\nstart "" "%~dp0WirePulse.exe" --renderer "CPU (compatibilidad)"\r\n',encoding='ascii')
 with zipfile.ZipFile(output/(name+'.zip'),'w',zipfile.ZIP_DEFLATED) as archive:
  for path in sorted(stage.rglob('*')):
   if path.is_file():archive.write(path,path.relative_to(output))
 subprocess.run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'packaging/windows/build-installer.ps1'),'-SourceDir',str(stage),'-Version',version,'-OutputDir',str(output)],check=True)
else:
 (stage/'WirePulse').chmod(0o755)
 with tarfile.open(output/(name+'.tar.gz'),'w:gz') as archive:archive.add(stage,arcname=name)
 package=output/'deb-root';app=package/'opt'/'wirepulse';app.mkdir(parents=True,exist_ok=True)
 shutil.copytree(stage,app,dirs_exist_ok=True)
 control=package/'DEBIAN';control.mkdir(exist_ok=True)
 (control/'control').write_text(f'Package: wirepulse\nVersion: {version}\nArchitecture: amd64\nMaintainer: JLiveOf <noreply@github.com>\nDepends: pulseaudio-utils, playerctl, libx11-6, libxfixes3, libice6, libsm6, libfontconfig1, libwebkit2gtk-4.1-0, libfreetype6, libgl1\nSection: graphics\nPriority: optional\nDescription: Audio-reactive native wallpaper for Cinnamon X11\n Shared WirePulse renderer with system audio capture and media metadata.\n')
 launch=package/'usr/share/applications';launch.mkdir(parents=True,exist_ok=True)
 (launch/'wirepulse.desktop').write_text('[Desktop Entry]\nType=Application\nName=WirePulse\nComment=Audio-reactive desktop wallpaper\nExec=/opt/wirepulse/WirePulse\nIcon=wirepulse\nTerminal=false\nCategories=AudioVideo;Graphics;\nStartupNotify=false\n')
 icons=package/'usr/share/icons/hicolor/1024x1024/apps';icons.mkdir(parents=True,exist_ok=True);shutil.copy2(root/'Assets/WirePulse.png',icons/'wirepulse.png')
 subprocess.run(['dpkg-deb','--build','--root-owner-group',str(package),str(output/f'wirepulse_{version}_amd64.deb')],check=True)
 shutil.rmtree(package)
shutil.rmtree(stage)
files=[p for p in sorted(output.iterdir()) if p.is_file() and p.name!='SHA256SUMS.txt']
(output/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in files))
print('\n'.join(p.name for p in files))
