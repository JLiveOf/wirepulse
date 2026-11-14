#!/usr/bin/python3
"""Generated metadata only: no accounts, copyrighted songs, or personal library."""
import dbus, dbus.service, dbus.mainloop.glib
from gi.repository import GLib
import pathlib,time

dbus.mainloop.glib.DBusGMainLoop(set_as_default=True)
start=time.monotonic()
class Player(dbus.service.Object):
 def __init__(self):
  self.bus=dbus.SessionBus();self.name=dbus.service.BusName('org.mpris.MediaPlayer2.cider.wirepulse_test',self.bus)
  super().__init__(self.bus,'/org/mpris/MediaPlayer2')
 def props(self,iface):
  if iface=='org.mpris.MediaPlayer2':return {'Identity':'Cider','CanQuit':False,'CanRaise':False,'HasTrackList':False,'SupportedUriSchemes':dbus.Array(['file'],signature='s'),'SupportedMimeTypes':dbus.Array(['audio/wav'],signature='s')}
  return {'PlaybackStatus':'Playing','Metadata':dbus.Dictionary({'mpris:trackid':dbus.ObjectPath('/track/fixture'),'mpris:length':dbus.Int64(120000000),'xesam:title':'WirePulse integration tone','xesam:artist':dbus.Array(['WirePulse'],signature='s'),'xesam:album':'Generated test signal','mpris:artUrl':pathlib.Path('Assets/WirePulse.png').resolve().as_uri()},signature='sv'),'Position':dbus.Int64((time.monotonic()-start)*1000000),'Rate':1.0,'MinimumRate':1.0,'MaximumRate':1.0,'CanControl':False,'CanPlay':True,'CanPause':True,'CanSeek':False,'CanGoNext':False,'CanGoPrevious':False}
 @dbus.service.method('org.freedesktop.DBus.Properties',in_signature='s',out_signature='a{sv}')
 def GetAll(self,iface):return self.props(iface)
 @dbus.service.method('org.freedesktop.DBus.Properties',in_signature='ss',out_signature='v')
 def Get(self,iface,key):return self.props(iface)[key]
player=Player();GLib.MainLoop().run()
