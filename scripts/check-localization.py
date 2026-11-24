#!/usr/bin/env python3
"""Check shared English coverage while keeping persisted control keys unchanged."""
import json,pathlib,re
root=pathlib.Path(__file__).resolve().parent.parent
catalog=json.loads((root/'desktop/Assets/en.json').read_text())
assert len(catalog)>400 and all(k and v for k,v in catalog.items())
for spec in json.loads((root/'desktop/Assets/controls.json').read_text()):
 assert spec['title'] in catalog, f"Untranslated desktop control: {spec['title']}"
for title in re.findall(r'title:"([^"]+)"',(root/'macos/Sources/Configuration.swift').read_text()):
 assert title in catalog, f'Untranslated macOS control: {title}'
for label in ['Continuar','Silenciar música','Novedades de\nWirePulse','Idioma','Idioma del sistema','Propiedades','Inicio prioritario','Guardar diseño','Cancelar','No se pudo aplicar el cambio']:
 assert label in catalog
for path,marker in [('desktop/WirePulse.csproj','Assets/en.json'),('desktop/WindowsSettings/WirePulse.Settings.csproj','../Assets/en.json'),('macos/build.sh','Resources/en.json')]:
 assert marker in (root/path).read_text(), f'Missing catalog in {path}'
print(f'PASS {len(catalog)} English translations, all desktop/macOS controls, and all package catalogs')

import ast
gtk=ast.parse((root/'desktop/LinuxUI/native.py').read_text())
for node in ast.walk(gtk):
 if not isinstance(node,ast.Call) or not isinstance(node.func,ast.Attribute):continue
 name=node.func.attr;index=0 if name in ('t','label','button','chooser') else 1 if name in ('section','row','toggle','entry','choice','number','color','font') else -1
 if index<0 or len(node.args)<=index:continue
 value=node.args[index]
 if isinstance(value,ast.Constant) and isinstance(value.value,str) and value.value not in ('','WirePulse'):assert value.value in catalog,'Untranslated GTK control: '+value.value
notes=json.loads((root/'desktop/Assets/whats-new-linux.json').read_text())
for feature in notes['features']:
 assert feature['title'] in catalog and feature['description'] in catalog
print('PASS native GTK labels and Linux release highlights translated')
