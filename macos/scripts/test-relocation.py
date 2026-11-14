#!/usr/bin/env python3
"""Test a moved running bundle using a disposable copy, leaving the input app untouched."""
import pathlib,shutil,subprocess,sys,tempfile
app=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else 'dist/WirePulse.app').resolve()
if not (app/'Contents/MacOS/WirePulse').is_file():raise SystemExit('Pass a compiled WirePulse.app bundle')
with tempfile.TemporaryDirectory(prefix='wirepulse-relocation-test-') as directory:
 root=pathlib.Path(directory).resolve();source=root/'Before/WirePulse.app';target=root/'Applications/WirePulse.app'
 source.parent.mkdir();target.parent.mkdir();shutil.copytree(app,source)
 subprocess.run([str(source/'Contents/MacOS/WirePulse'),'--relocation-probe',str(target)],check=True)
