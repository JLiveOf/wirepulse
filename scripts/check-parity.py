#!/usr/bin/env python3
"""Fail CI if shared controls and native Swift ranges drift between platforms."""
import json,pathlib,re
root=pathlib.Path(__file__).resolve().parent.parent
specs=json.loads((root/'desktop/Assets/controls.json').read_text())
swift=(root/'macos/Sources/Configuration.swift').read_text()
mac={key:(group,kind,args) for key,group,kind,args in re.findall(r'\.init\(id:"([^"]+)",title:"[^"]+",group:"([^"]+)",kind:\.(\w+)(?:\(([^)]+)\))?\)',swift)}
for spec in specs:
 key=spec['key'];assert key in mac,f'macOS is missing {key}'
 group,kind,args=mac[key];assert group==spec['group'],f'{key}: group mismatch'
 assert kind==spec['kind'] or (kind,spec['kind']) in [('toggle','bool'),('image','file')],f'{key}: control mismatch {kind}/{spec["kind"]}'
 if kind=='slider':assert [float(x) for x in args.split(',')]==[spec['min'],spec['max'],spec['step']],f'{key}: range mismatch {args}'
defaults=dict(re.findall(r'"([^"]+)":(-?\d+(?:\.\d+)?)',swift[swift.index('static let defaults:'):swift.index('@Published var values:')]))
for spec in specs:
 if spec['key'] in defaults:assert float(defaults[spec['key']])==float(spec['value']),f"{spec['key']}: default mismatch"
providers=['Automático','Ninguno','Apple Music','Spotify','Cider']
for source in ['desktop/SettingsWindow.cs','desktop/WindowsSettings/SettingsWindow.cs','macos/Sources/Settings.swift']:
 text=(root/source).read_text()
 for provider in providers:assert '"'+provider+'"' in text,f'{source}: provider missing {provider}'
for source in ['desktop/WindowsSettings/SettingsWindow.cs','desktop/SettingsWindow.cs','macos/Sources/Settings.swift']:
 text=(root/source).read_text()
 for group in dict.fromkeys(x['group'] for x in specs):assert '"'+group+'"' in text,f'{source}: page missing {group}'
assert re.fullmatch(r'4\.\d+\.\d+', (root/'VERSION').read_text().strip()), 'Yamato version mismatch'
print(f'PASS {len(specs)} shared controls, groups, slider ranges/steps and five providers across all settings UIs')
