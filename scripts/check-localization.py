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
