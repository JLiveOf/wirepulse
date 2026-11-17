#!/usr/bin/env python3
"""Keep the Windows/Linux assembly, macOS bundle and release tag in sync."""
import pathlib,plistlib,re,sys,xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parent.parent
version=(root/'VERSION').read_text().strip()
if not re.fullmatch(r'\d+\.\d+\.\d+',version):raise SystemExit('VERSION must be major.minor.patch')
project=root/'desktop/WirePulse.csproj';plist=root/'macos/Resources/Info.plist'
info=plistlib.loads(plist.read_bytes())
companion=root/'desktop/WindowsSettings/WirePulse.Settings.csproj'
assert '../../VERSION' in ET.parse(companion).findtext('.//Version'), 'WinUI companion must read the central VERSION'
if '--check' in sys.argv:
 assert ET.parse(project).findtext('.//Version')==version,'Desktop version does not match VERSION'
 assert info['CFBundleShortVersionString']==version and info['CFBundleVersion']==version,'macOS version does not match VERSION'
else:
 project.write_text(re.sub(r'<Version>[^<]+</Version>',f'<Version>{version}</Version>',project.read_text()))
 info['CFBundleShortVersionString']=info['CFBundleVersion']=version;plist.write_bytes(plistlib.dumps(info,sort_keys=False))
print(version)
