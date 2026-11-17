import gi,json,subprocess,time
from pathlib import Path
gi.require_version('Gtk','3.0')
from gi.repository import Gtk,Gdk
w=Gtk.Window(title='WirePulse coverage fixture');w.set_default_size(400,200);w.show_all()
def wait():
 end=time.monotonic()+1
 while time.monotonic()<end:
  while Gtk.events_pending():Gtk.main_iteration_do(False)
  time.sleep(.02)
def reason(*args):
 output=subprocess.check_output(['./publish/WirePulse','--coverage-probe','[0,0,1280,720]',*args],text=True)
 return json.loads(output)['reason']
wait();assert reason()=='','ordinary window incorrectly covers wallpaper'
w.maximize();wait();assert reason()=='ventana maximizada','maximized EWMH state missed'
assert reason('--ignore-maximized')=='','maximized preference ignored'
# A window on this display cannot cover a disjoint monitor.
other=json.loads(subprocess.check_output(['./publish/WirePulse','--coverage-probe','[1280,0,1440,900]'],text=True))
assert other['reason']=='',other
w.unmaximize();wait();assert reason()=='','restored window leaves stale coverage'
w.fullscreen();wait();assert reason()=='pantalla completa','fullscreen EWMH state missed'
assert reason('--ignore-fullscreen')=='','fullscreen preference ignored'
w.iconify();wait();assert reason()=='','minimized window still covers wallpaper'
w.destroy();wait()
Path('test-results/linux-coverage.json').write_text(json.dumps({'valid':True,'checks':['maximized','restored','fullscreen','minimized','independent displays','disabled preferences']}))
print('PASS real Cinnamon maximize, restore, fullscreen, minimize and per-monitor coverage')
