#!/usr/bin/env python3
"""Embed the source identity in each app and publish platform-specific hotfix identities."""
import json,pathlib,subprocess,sys
root=pathlib.Path(__file__).resolve().parent.parent
identity={'version':(root/'VERSION').read_text().strip(),'id':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'sequence':int(subprocess.check_output(['git','show','-s','--format=%ct','HEAD'],cwd=root,text=True).strip())}
value={p:identity for p in ('windows','linux','linux-deb','macos')} if '--manifest' in sys.argv else identity
path=pathlib.Path(sys.argv[1]);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,indent=2)+'\n')
