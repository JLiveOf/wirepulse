#!/usr/bin/python3
import gi,pathlib

gi.require_version('Gtk','3.0')
from gi.repository import Gtk,Gdk
window=Gtk.Window(title='WirePulse foreground test')
window.set_default_size(320,90);window.move(480,60)
button=Gtk.Button(label='Click-through test')
button.connect('clicked',lambda *_:pathlib.Path('test-results/clicked.txt').write_text('clicked'))
css=Gtk.CssProvider();css.load_from_data(b'button { background-image:none; background-color:#00ff00; color:#000000; border:0; border-radius:0; }')
button.get_style_context().add_provider(css,Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION)
window.add(button);window.connect('destroy',Gtk.main_quit);window.show_all();Gtk.main()
