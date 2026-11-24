#!/usr/bin/python3
"""Native GTK 3 companion. The renderer owns all settings, assets and scene history."""
import argparse, concurrent.futures, json, math, os, pathlib, socket, sys, time, uuid
import gi
gi.require_version('Gtk', '3.0')
from gi.repository import Gtk, Gdk, GLib, Pango, GdkPixbuf
ROOT = pathlib.Path(__file__).resolve().parent.parent
ICONS={'Audio':'audio-volume-high-symbolic','Fondo':'image-x-generic-symbolic','Fondo animado':'starred-symbolic','Orbe':'web-browser-symbolic','Espectro':'view-sort-ascending-symbolic','Tarjeta':'audio-x-generic-symbolic','Reloj':'preferences-system-time-symbolic','Rendimiento':'preferences-system-symbolic','Inicio':'system-shutdown-symbolic'}
GROUPS = ['Audio','Fondo','Fondo animado','Orbe','Espectro','Tarjeta','Reloj','Rendimiento','Inicio']
LAYOUT = {'sphereX','sphereY','radius','spectrumX','spectrumY','spectrumWidth','spectrumHeight','cardX','cardY','cardscale','clockX','clockY','clocksize'}
MEDIA = ['*.png','*.jpg','*.jpeg','*.webp','*.bmp','*.gif','*.mp4','*.m4v','*.mov','*.wmv','*.avi','*.webm','*.mkv']
class Companion:
 def __init__(self,args):
  self.args=args;self.state={};self.language='es';self.catalog=json.loads((ROOT/'Assets/en.json').read_text());self.covered=set();self.inflight=0;self.pool=concurrent.futures.ThreadPoolExecutor(max_workers=1);self.busy=False;self.pending={};self.widgets={};self.profile='';self.group='Audio';self.selected=None;self.item_ids=[];self.finished=False;self.finishing=False;self.loading=False;self.phase='intro';self.started=time.monotonic();self.expanding=False;self.expanded=False;self.editor_hidden=False;self.focus_stamp=None
  self.window=Gtk.Window(title='WirePulse');self.window.set_icon_from_file(str(ROOT/'Assets/WirePulse.png'));self.window.set_default_size(880,650);self.window.connect('delete-event',self.close)
  self.box=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=0);self.window.add(self.box)
  if args.mode=='experience':
   self.window.set_default_size(320,280);self.window.set_keep_above(True);self.window.set_resizable(False);self.window.set_decorated(False);self.header=Gtk.HeaderBar();self.header.set_show_close_button(False);brand=Gtk.Box(spacing=10);self.pack(brand,Gtk.Image.new_from_pixbuf(GdkPixbuf.Pixbuf.new_from_file_at_scale(str(ROOT/'Assets/WirePulse.png'),32,32,True)));self.pack(brand,self.label('WirePulse',16));self.header.pack_start(brand);self.header.set_no_show_all(True);self.window.set_titlebar(self.header);self.header.hide()
  self.window.set_position(Gtk.WindowPosition.CENTER);self.rpc('get' if args.mode!='editor' else 'editor-get',self.loaded)
  GLib.timeout_add(50,self.flush);GLib.timeout_add(250,self.poll);GLib.timeout_add(250,self.focus)
 def t(self,text):return self.catalog.get(str(text),str(text)) if self.language=='en' else str(text)
 def rpc(self,op,done=None,**values):
  self.inflight+=1;values['op']=op
  if self.args.mode=='settings':values.setdefault('profile',self.profile)
  def work():
   with socket.socket(socket.AF_UNIX) as s:
    s.settimeout(12);s.connect(self.args.socket);s.sendall((json.dumps(values,ensure_ascii=False)+'\n').encode());reply=s.makefile('rb').readline(4_000_000)
   result=json.loads(reply)
   if 'error' in result:raise RuntimeError(result['error'])
   return result
  future=self.pool.submit(work)
  def completed(f):
   def apply():
    self.inflight=max(0,self.inflight-1)
    try:
     result=f.result()
     if done:done(result)
    except Exception as error:self.error(str(error))
    return False
   GLib.idle_add(apply)
  future.add_done_callback(completed)
 def command(self,op,refresh=False,**values):
  if op not in ('set','gradient','editor-style','editor-property','get','editor-get'):self.flush(True)
  self.rpc(op,lambda s:self.apply(s,refresh),**values)
 def defer(self,op,**values):
  if self.args.mode=='settings':values.setdefault('profile',self.profile)
  self.pending[(op,values.get('key',''))]=(op,values)
 def flush(self,force=False):
  if self.inflight and not force:return not self.finished
  pending,self.pending=self.pending,{}
  for op,values in pending.values():self.command(op,**values)
  return not self.finished
 def focus(self):
  path=pathlib.Path(self.args.socket).parent/('focus-'+self.args.mode)
  try:
   stamp=path.read_text()
   if stamp!=self.focus_stamp:self.focus_stamp=stamp;self.window.present()
  except OSError:pass
  return not self.finished
 def poll(self):
  if self.finished:return False
  if self.busy or self.inflight:return True
  self.busy=True
  def received(s):self.busy=False;self.apply(s)
  self.rpc('editor-get' if self.args.mode=='editor' else 'get',received);return True
 def apply(self,s,rebuild=False):
  previous=self.state;old_scene=self.target().get('scene') if self.args.mode=='settings' else None;self.state=s;self.language=s.get('language',self.language)
  if self.args.mode=='settings':
   if rebuild or previous.get('independent')!=s.get('independent') or list(previous.get('displays',{}))!=list(s.get('displays',{})):self.build_settings()
   elif old_scene!=self.target().get('scene'):self.build_page()
   for key,value in [('audio',s.get('audio','')),('media',s.get('media','')),('updates',s.get('updates',{}).get('status','')),('performance','\n'.join(f"{p['name']}: {p['fps']:.0f} FPS / {p['target']:.0f} · {p['drawMs']:.2f} ms · {p['backend']}" for p in s.get('performance',[]))),('tempo',f"{s.get('bpm',0):.0f} BPM")]:
    if key in self.widgets:self.widgets[key].set_text(self.t(value))
   if 'fft' in self.widgets:
    rate=s.get('sampleRate',0);fft=s.get('fft',0);self.widgets['fft'].set_text(f"64 bands · FFT {fft} · {rate} Hz · {rate/fft if fft else 0:.2f} Hz/bin" if self.language=='en' else f"64 bandas · FFT {fft} · {rate} Hz · {rate/fft if fft else 0:.2f} Hz/bin")
   if 'capture' in self.widgets:self.widgets['capture'].set_label(self.t('Detener captura' if s.get('audioRunning') else 'Activar audio del sistema'))
   if 'meters' in self.widgets:self.widgets['meters'].queue_draw()
  elif self.args.mode=='editor':
   if not s.get('open',False):self.destroy();return
   self.update_layers()
   selected=s.get('selectedId')
   if rebuild or selected!=self.selected:self.selected=selected;self.build_properties()
   if s.get('closeRequested'):self.cancel_editor()
  elif self.phase=='update':
   self.widgets['status'].set_text(self.t(s.get('status','')));self.widgets['progress'].set_fraction(min(1,max(0,s.get('progress',0))))
   self.widgets['actions'].set_sensitive(not s.get('installing',False))
 def loaded(self,s):
  self.state=s;self.language=s.get('language','es')
  if self.args.mode=='settings':self.build_settings()
  elif self.args.mode=='editor':self.build_editor()
  else:self.build_experience()
  self.window.show_all();self.fit_window();self.window.present()
  if self.args.test_output:GLib.timeout_add(600,self.smoke)
 def fit_window(self):
  screen=self.window.get_screen();area=screen.get_monitor_workarea(screen.get_monitor_at_window(self.window.get_window()))
  width,height=self.window.get_size();self.window.resize(min(width,max(300,area.width-32)),min(height,max(260,area.height-64)))
  if self.args.mode!='editor':self.window.move(area.x+max(0,(area.width-min(width,area.width-32))//2),area.y+max(0,(area.height-min(height,area.height-64))//2))
 def clear(self,box):
  for child in box.get_children():box.remove(child)
 def label(self,text,size=None,dim=False):
  label=Gtk.Label(label=self.t(text),xalign=0);label.set_line_wrap(True);label.set_line_wrap_mode(Pango.WrapMode.WORD_CHAR);label.set_max_width_chars(68)
  if size:
   attrs=Pango.AttrList();attrs.insert(Pango.attr_size_new_absolute(size*Pango.SCALE));attrs.insert(Pango.attr_weight_new(Pango.Weight.BOLD));label.set_attributes(attrs)
  if dim:label.get_style_context().add_class('dim-label')
  return label
 def button(self,text,action,icon=None):
  b=Gtk.Button.new_with_label(self.t(text));b.connect('clicked',lambda *_:action())
  if icon:b.set_image(Gtk.Image.new_from_icon_name(icon,Gtk.IconSize.BUTTON));b.set_always_show_image(True)
  return b
 def pack(self,box,child,expand=False):box.pack_start(child,expand,expand,0);return child
 def row(self,box,title,widget):
  row=Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL,spacing=16);row.pack_start(self.label(title),True,True,0);row.pack_end(widget,False,False,0);box.pack_start(row,False,False,0);return widget
 def section(self,box,title):
  outer=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=12);outer.set_margin_top(14);outer.set_margin_bottom(4);self.pack(outer,self.label(title,13));inner=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=12);self.pack(outer,inner);self.pack(box,outer);return inner
 def toggle(self,box,title,value,change):
  w=Gtk.Switch();w.set_active(bool(value));w.connect('notify::active',lambda *_:change(w.get_active()) if not self.loading else None);self.row(box,title,w);return w
 def choice(self,box,title,values,value,change,labels=None):
  w=Gtk.ComboBoxText()
  for key,label in zip(values,labels or values):w.append(str(key),self.t(label))
  w.set_active_id(str(value));w.connect('changed',lambda *_:change(w.get_active_id()) if w.get_active_id()!=None and not self.loading else None);self.row(box,title,w);return w
 def entry(self,box,title,value,change,secret=False):
  w=Gtk.Entry();w.set_text(str(value or ''));w.set_visibility(not secret);w.set_width_chars(22);saved=[w.get_text()]
  def commit(*_):
   if w.get_text()!=saved[0]:saved[0]=w.get_text();change(saved[0])
   return False
  w.connect('focus-out-event',commit);w.connect('activate',commit);self.row(box,title,w);return w
 def number(self,box,title,value,lo,hi,step,change):
  group=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=5);self.pack(group,self.label(title));row=Gtk.Box(spacing=8);adjustment=Gtk.Adjustment(value=min(hi,max(lo,float(value or 0))),lower=lo,upper=hi,step_increment=step,page_increment=max(step,(hi-lo)/10));scale=Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL,adjustment=adjustment);scale.set_draw_value(False);scale.set_hexpand(True);spin=Gtk.SpinButton(adjustment=adjustment,climb_rate=step,digits=3 if step<.01 else 2 if step<1 else 0);spin.set_width_chars(6);self.pack(row,scale,True);self.pack(row,spin);self.pack(group,row);self.pack(box,group);gesture=[uuid.uuid4().hex]
  def begin(*_):gesture[0]=uuid.uuid4().hex;return False
  scale.connect('button-press-event',begin);spin.connect('focus-in-event',begin);adjustment.connect('value-changed',lambda a:change(a.get_value(),gesture[0]) if not self.loading else None);return scale
 def rgba(self,text):
  c=Gdk.RGBA();text=str(text or '#ffffff')
  if len(text)==9 and text.startswith('#'):c.parse('#'+text[3:]);c.alpha=int(text[1:3],16)/255
  elif not c.parse(text):c.parse('#ffffff')
  return c
 def color(self,box,title,value,change):
  w=Gtk.ColorButton();w.set_title(self.t(title));w.set_use_alpha(True);w.set_rgba(self.rgba(value))
  def selected(*_):
   c=w.get_rgba();r,g,b,a=[round(x*255) for x in (c.red,c.green,c.blue,c.alpha)];change(('#%02x%02x%02x'%(r,g,b)) if a==255 else ('#%02x%02x%02x%02x'%(a,r,g,b)))
  w.connect('color-set',selected);self.row(box,title,w);return w
 def gradient(self,box,key,current,change):
  values=dict(current or {'enabled':False,'end':'#0000ff','angle':0});panel=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=10)
  def update(k,v):values[k]=v;change(dict(values))
  self.toggle(panel,'Gradiente',values.get('enabled',False),lambda v:update('enabled',v));self.color(panel,'Color final',values.get('end','#0000ff'),lambda v:update('end',v));self.number(panel,'Ángulo del gradiente',values.get('angle',0),-360,360,1,lambda v,g:update('angle',v));exp=Gtk.Expander(label=self.t('Gradiente'));exp.add(panel);self.pack(box,exp)
 def chooser(self,title,save=False,patterns=None,folder=False):
  action=Gtk.FileChooserAction.SELECT_FOLDER if folder else Gtk.FileChooserAction.SAVE if save else Gtk.FileChooserAction.OPEN
  dialog=Gtk.FileChooserNative.new(self.t(title),self.window,action,self.t('Guardar' if save else 'Elegir…'),self.t('Cancelar'))
  if patterns:
   filt=Gtk.FileFilter();filt.set_name(self.t(title))
   for pattern in patterns:filt.add_pattern(pattern)
   dialog.add_filter(filt)
  if save:dialog.set_current_name('WirePulse-layout.wirepulse');dialog.set_do_overwrite_confirmation(True)
  result=dialog.run();path=dialog.get_filename() if result==Gtk.ResponseType.ACCEPT else None;dialog.destroy();return path
 def error(self,text):
  self.busy=False
  if self.finished:return
  if self.args.test_output:
   pathlib.Path(self.args.test_output).write_text(json.dumps({'valid':False,'error':text}));print(text,file=sys.stderr);self.destroy();return
  dialog=Gtk.MessageDialog(transient_for=self.window,modal=True,message_type=Gtk.MessageType.ERROR,buttons=Gtk.ButtonsType.CLOSE,text='WirePulse');dialog.set_keep_above(True);dialog.format_secondary_text(self.t(text));dialog.run();dialog.destroy()
 def build_settings(self):
  self.loading=True;self.widgets={};self.clear(self.box);self.window.set_title(self.t('Ajustes de WirePulse'));self.window.set_resizable(True)
  split=Gtk.Box(spacing=0);self.pack(self.box,split,True);sidebar=Gtk.ListBox();sidebar.set_selection_mode(Gtk.SelectionMode.SINGLE);sidebar.set_size_request(170,-1);sidebar.get_style_context().add_class('sidebar')
  for title in GROUPS:
   row=Gtk.ListBoxRow();line=Gtk.Box(spacing=10);line.set_margin_start(12);line.set_margin_end(12);line.set_margin_top(12);line.set_margin_bottom(12);icon=Gtk.Image.new_from_icon_name(ICONS[title],Gtk.IconSize.MENU);self.pack(line,icon);self.pack(line,self.label(title));row.add(line);row.group=title;sidebar.add(row)
  self.pack(split,sidebar);self.pack(split,Gtk.Separator(orientation=Gtk.Orientation.VERTICAL));right=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=0);self.pack(split,right,True)
  header=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=10);header.set_border_width(16);self.pack(right,header)
  self.toggle(header,'Ajustar cada pantalla por separado',self.state.get('independent',False),lambda v:self.command('property',True,key='IndependentDisplays',value=v,**{'global':True}))
  displays=list(self.state.get('displays',{}))
  if self.state.get('independent') and displays:
   if self.profile not in displays:self.profile=displays[0]
   self.choice(header,'Pantalla',displays,self.profile,self.select_profile)
  else:self.profile=''
  self.pack(right,Gtk.Separator());scroll=Gtk.ScrolledWindow();scroll.set_policy(Gtk.PolicyType.NEVER,Gtk.PolicyType.AUTOMATIC);self.pack(right,scroll,True);self.page=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=14);self.page.set_border_width(20);scroll.add(self.page)
  def select(_list,row):
   if row:self.group=row.group;self.build_page()
  sidebar.connect('row-selected',select);sidebar.select_row(sidebar.get_row_at_index(GROUPS.index(self.group)));self.loading=False;self.box.show_all()
 def select_profile(self,value):self.flush(True);self.profile=value;self.rpc('get',lambda s:self.apply(s,True))
 def target(self):return self.state.get('displays',{}).get(self.profile,self.state.get('global',{})) if self.state.get('independent') else self.state.get('global',{})
 def build_page(self):
  self.loading=True;self.widgets={};self.clear(self.page);target=self.target();group=self.group
  self.pack(self.page,self.label(group,24));tools=self.section(self.page,'Escritorio');self.pack(tools,self.button('Editar escritorio…',lambda:self.command('edit'), 'document-edit'));line=Gtk.Box(spacing=8)
  for importing,title in [(True,'Importar layout…'),(False,'Exportar layout…')]:self.pack(line,self.button(title,lambda v=importing:self.transfer(v)))
  self.pack(tools,line);self.pack(tools,self.label('Mueve, redimensiona y añade objetos con el ratón. Las guías se pueden desactivar.',dim=True))
  if group in ('Orbe','Espectro','Tarjeta','Reloj','Fondo animado'):
   self.pack(self.page,self.button('Mover, redimensionar o añadir objetos…',lambda:self.command('edit')))
  if group=='Audio':
   area=self.section(self.page,'Audio del sistema');self.widgets['capture']=self.pack(area,self.button('Detener captura' if self.state.get('audioRunning') else 'Activar audio del sistema',lambda:self.command('audio')));self.widgets['audio']=self.pack(area,self.label(self.state.get('audio','')));self.widgets['fft']=self.pack(area,self.label('',dim=True));self.pack(area,self.label('Analiza el sonido que reproducen las aplicaciones, en memoria. No usa el micrófono ni guarda grabaciones.',dim=True));meter=Gtk.DrawingArea();meter.set_size_request(-1,54);meter.connect('draw',self.draw_meters);self.widgets['meters']=meter;self.pack(area,meter)
  if group=='Inicio':
   area=self.section(self.page,'Bienvenida');self.pack(area,self.button('¿Qué hay de nuevo?',lambda:self.command('show-news')));self.toggle(area,'Mostrar la splash en cada inicio',self.state.get('showSplash',False),lambda v:self.command('show-splash',value=v))
   area=self.section(self.page,'Actualizaciones');self.toggle(area,'Comprobar al iniciar',self.state.get('updates',{}).get('automatic',True),lambda v:self.command('update-automatic',value=v));self.choice(area,'Canal de actualizaciones',['stable','beta'],self.state.get('updates',{}).get('channel','stable'),lambda v:self.command('update-channel',value=v),['Estable','Beta (prereleases)']);self.pack(area,self.button('Comprobar ahora',lambda:self.command('update-check')));self.widgets['updates']=self.pack(area,self.label(self.state.get('updates',{}).get('status',''),dim=True))
   area=self.section(self.page,'Inicio de sesión');self.choice(area,'Idioma',['system','es','en'],self.state.get('languagePreference','system'),lambda v:self.command('language',value=v),['Idioma del sistema','Español','Inglés']);self.pack(area,self.label('Reinicia WirePulse para aplicar el idioma.',dim=True));self.toggle(area,'Abrir WirePulse al iniciar sesión',self.state.get('startup',False),lambda v:self.command('startup',value=v))
  if group=='Tarjeta':
   area=self.section(self.page,'Canción actual');self.choice(area,'Proveedor de música',['Automático','Ninguno','Apple Music','Spotify','Cider'],self.state.get('mediaSource','Automático'),lambda v:self.command('property',key='MediaSource',value=v,**{'global':True}));self.entry(area,'Dirección local de Cider',self.state.get('ciderUrl',''),lambda v:self.command('property',key='CiderUrl',value=v,**{'global':True}));self.entry(area,'Token de Cider',self.state.get('ciderToken',''),lambda v:self.command('property',key='CiderToken',value=v,**{'global':True}),True);self.widgets['media']=self.pack(area,self.label(self.state.get('media',''),dim=True))
  if group=='Fondo animado' and not target.get('scene'):
   self.choice(self.page,'Material',['Estrellas','Malla'],target.get('backgroundStyle','Estrellas'),lambda v:self.command('property',key='BackgroundStyle',value=v));self.widgets['tempo']=self.pack(self.page,self.label('Esperando un ritmo estable',dim=True))
  if group=='Reloj' and not target.get('scene'):self.font(self.page,'Fuente del reloj',target.get('clockFont','System'),lambda v:self.command('property',key='ClockFont',value=v))
  controls=self.section(self.page,group)
  for spec in self.state.get('specs',[]):
   if spec['group']==group and spec['key'] not in LAYOUT and (not target.get('scene') or group in ('Audio','Fondo','Rendimiento')):self.setting_control(controls,spec,target)
  if group=='Rendimiento':
   area=self.section(self.page,'Rendimiento');self.choice(area,'Perfil de rendimiento',['Automático','Calidad','Ligero'],target.get('profile','Automático'),lambda v:self.command('property',key='RenderProfile',value=v));self.toggle(area,'Pausar si una ventana está maximizada en esta pantalla',target.get('suspendMaximized',False),lambda v:self.command('property',key='SuspendMaximized',value=v));self.toggle(area,'Pausar cuando una app ocupa toda esta pantalla',target.get('suspendFullscreen',True),lambda v:self.command('property',key='SuspendFullscreen',value=v));self.toggle(area,'Pausar animación',self.state.get('paused',False),lambda v:self.command('pause',value=v));self.widgets['performance']=self.pack(area,self.label('',dim=True))
  self.pack(self.page,self.button('Restablecer valores',lambda:self.command('reset',True)));self.loading=False;self.page.show_all()
 def setting_control(self,box,spec,target):
  self.covered.add(spec['key']);key=spec['key'];value=target.get('values',{}).get(key,spec.get('value',0));kind=spec['kind'];title=spec['title']
  if kind in ('bool','toggle'):self.toggle(box,title,value,lambda v:self.command('set',key=key,value=v))
  elif kind=='slider':self.number(box,title,value,spec['min'],spec['max'],spec['step'],lambda v,g:self.defer('set',key=key,value=v))
  elif kind=='color':self.color(box,title,value,lambda v:self.command('set',key=key,value=v));self.gradient(box,key,target.get('gradients',{}).get(key),lambda v:self.defer('gradient',key=key,value=v))
  elif kind in ('image','file'):
   line=Gtk.Box(spacing=8);self.pack(box,self.label(title));self.pack(line,self.button('Elegir archivo…',lambda:self.choose_background()));self.pack(line,self.button('Quitar',lambda:self.command('set',True,key=key,value='')));self.pack(box,line);self.pack(box,self.label(pathlib.Path(str(value)).name if value else 'Sin archivo',dim=True));self.pack(box,self.label('Los vídeos se reproducen en bucle y sin audio. MP4/H.264 ofrece la mayor compatibilidad; otros formatos dependen de los códecs instalados.',dim=True))
  else:self.entry(box,title,value,lambda v:self.command('set',key=key,value=v))
 def draw_meters(self,area,cr):
  bands=self.state.get('bands',[0]*64) if self.state.get('age',999)<.4 else [0]*64;color=area.get_style_context().get_color(Gtk.StateFlags.SELECTED);cr.set_source_rgba(color.red,color.green,color.blue,1);w=area.get_allocated_width()/64;h=area.get_allocated_height()
  for i,value in enumerate(bands):height=max(2,min(h,float(value)*h));cr.rectangle(i*w,h-height,max(1,w-2),height)
  cr.fill();return False
 def choose_background(self):
  path=self.chooser('Imágenes, GIF y vídeo',patterns=MEDIA)
  if path:self.command('import',True,value=path)
 def transfer(self,importing):
  path=self.chooser('Importar layout' if importing else 'Exportar layout',save=not importing,patterns=['*.wirepulse','*.zip'])
  if path:self.command('layout-import' if importing else 'layout-export',True,value=path)
 def font(self,box,title,value,change):
  w=Gtk.FontButton();w.set_use_font(False);w.set_font_name('Sans 12' if value=='System' else value+' 12');w.connect('font-set',lambda *_:change(w.get_font_desc().get_family()));self.row(box,title,w)
 def build_editor(self):
  self.window.set_title(self.t('Editar escritorio'));self.window.set_type_hint(Gdk.WindowTypeHint.UTILITY);self.window.set_skip_taskbar_hint(True);self.window.set_keep_above(True);self.window.set_default_size(350,600);self.clear(self.box)
  header=Gtk.HeaderBar();header.set_title(self.t('Propiedades'));header.set_show_close_button(True);self.window.set_titlebar(header)
  scroll=Gtk.ScrolledWindow();scroll.set_policy(Gtk.PolicyType.NEVER,Gtk.PolicyType.AUTOMATIC);self.pack(self.box,scroll,True);panel=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=12);panel.set_border_width(14);scroll.add(panel)
  self.pack(panel,self.label('Objetos',18));self.pack(panel,self.label('Arrastra ⠿ para ordenar capas · arriba queda delante',dim=True));self.layer_model=Gtk.ListStore(str,str,bool,bool);self.layers=Gtk.TreeView(model=self.layer_model);self.layers.set_headers_visible(False);text_cell=Gtk.CellRendererText(ellipsize=Pango.EllipsizeMode.END,width_chars=16,max_width_chars=22);name_column=Gtk.TreeViewColumn('',text_cell,text=1);name_column.set_expand(True);self.layers.append_column(name_column);self.layer_buttons={}
  for key,icons in [('locked',('changes-allow-symbolic','changes-prevent-symbolic')),('visible',('view-conceal-symbolic','view-reveal-symbolic'))]:
   cell=Gtk.CellRendererPixbuf();column=Gtk.TreeViewColumn(self.t('Bloqueado' if key=='locked' else 'Visible'),cell);column.set_fixed_width(28);column.set_sizing(Gtk.TreeViewColumnSizing.FIXED);column.set_cell_data_func(cell,lambda col,renderer,model,it,data,k=key,choices=icons:renderer.set_property('icon-name',choices[bool(model[it][2 if k=='locked' else 3])]),None);self.layers.append_column(column);self.layer_buttons[column]=key
  self.layers.connect('button-press-event',self.layer_button);self.layers.set_reorderable(True);self.layers.get_selection().connect('changed',self.select_layer);self.layer_model.connect('rows-reordered',self.reordered);list_scroll=Gtk.ScrolledWindow();list_scroll.set_min_content_height(135);list_scroll.set_max_content_height(200);list_scroll.set_propagate_natural_height(True);list_scroll.add(self.layers);self.pack(panel,list_scroll)
  self.properties=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=12);self.pack(panel,self.properties);self.toolbar=Gtk.Window(title=self.t('Editar escritorio'));self.toolbar.set_type_hint(Gdk.WindowTypeHint.UTILITY);self.toolbar.set_skip_taskbar_hint(True);self.toolbar.set_keep_above(True);self.toolbar.set_resizable(False);self.toolbar.set_decorated(False);self.toolbar.connect('delete-event',lambda *_:(self.cancel_editor(),True)[1]);bar=Gtk.FlowBox();bar.set_selection_mode(Gtk.SelectionMode.NONE);bar.set_max_children_per_line(8);bar.set_column_spacing(6);bar.set_row_spacing(6);bar.set_border_width(10);self.toolbar.add(bar)
  add=Gtk.MenuButton(label=self.t('＋ Añadir'));menu=Gtk.Menu()
  for kind,title in self.state.get('kinds',{}).items():
   item=Gtk.MenuItem(label=self.t(title));item.connect('activate',lambda _,k=kind:self.add_object(k));menu.append(item)
  folder=Gtk.MenuItem(label=self.t('Widget desde carpeta…'));folder.connect('activate',lambda *_:self.add_object('widget',True));menu.append(folder);menu.show_all();add.set_popup(menu);bar.add(add)
  for title,op in [('↶ Deshacer','editor-undo'),('↷ Rehacer','editor-redo'),('Duplicar','editor-duplicate'),('Eliminar objeto','editor-delete')]:bar.add(self.button(title,lambda v=op:self.command(v,True)))
  snap=Gtk.CheckButton(label=self.t('Ajustar a guías'));snap.set_active(self.state.get('snap',True));snap.connect('toggled',lambda w:self.command('editor-snap',value=w.get_active()));bar.add(snap);bar.add(self.button('Propiedades',self.toggle_properties));bar.add(self.button('Cancelar',self.cancel_editor));save=self.button('Guardar diseño',lambda:self.command('editor-save'));save.get_style_context().add_class('suggested-action');bar.add(save)
  self.window.connect('size-allocate',lambda *_:self.place_inspector());self.window.connect('key-press-event',self.editor_key);self.toolbar.connect('key-press-event',self.editor_key);self.toolbar.connect('size-allocate',lambda *_:self.place_toolbar());self.toolbar.show_all();self.update_layers();self.build_properties();self.position_editor()
 def position_editor(self):
  s=self.state.get('screen',{});screen=self.window.get_screen();monitor=screen.get_monitor_at_point(int(s.get('x',0)),int(s.get('y',0)));area=screen.get_monitor_workarea(monitor)
  self.window.resize(min(350,area.width-32),max(260,min(650,area.height-110)));self.editor_area=area;self.window.move(area.x+max(8,area.width-366),area.y+12);self.toolbar.get_preferred_size();self.toolbar.resize(min(850,area.width-32),64);self.toolbar.move(area.x+max(8,(area.width-min(850,area.width-32))//2),area.y+area.height-84)
  try:
   gi.require_version('GdkX11','3.0');from gi.repository import GdkX11
   owner=GdkX11.X11Window.foreign_new_for_display(self.window.get_display(),int(self.state['owner']))
   if owner:
    self.window.realize();self.toolbar.realize();self.window.get_window().set_transient_for(owner);self.toolbar.get_window().set_transient_for(owner)
  except (ValueError,AttributeError,TypeError):pass
 def place_inspector(self):
  if not hasattr(self,'editor_area') or not self.window.get_window():return
  area=self.editor_area;w=self.window.get_window().get_width();x,y=area.x+max(8,area.width-w-12),area.y+12
  if self.window.get_position()!=(x,y):self.window.move(x,y)
 def place_toolbar(self):
  screen=self.toolbar.get_screen();s=self.state.get('screen',{});area=screen.get_monitor_workarea(screen.get_monitor_at_point(int(s.get('x',0)),int(s.get('y',0))));w,h=self.toolbar.get_size();self.toolbar.move(area.x+max(8,(area.width-w)//2),area.y+max(8,area.height-h-12))
 def toggle_properties(self):
  self.editor_hidden=not self.editor_hidden
  if self.editor_hidden:self.window.hide()
  else:self.window.show_all();self.fit_window();self.window.present()
 def update_layers(self):
  if not hasattr(self,'layer_model'):return
  items=self.state.get('items',[]);ids=[x['id'] for x in items];self.loading=True
  if ids!=self.item_ids:
   self.layer_model.clear()
   for item in items:self.layer_model.append([item['id'],self.layer_title(item),item.get('locked',False),item.get('visible',True)])
   self.item_ids=ids
  else:
   for row,item in zip(self.layer_model,items):row[1]=self.layer_title(item);row[2]=item.get('locked',False);row[3]=item.get('visible',True)
  for i,item in enumerate(items):
   if item['id']==self.state.get('selectedId'):self.layers.get_selection().select_path(i)
  self.loading=False
 def layer_title(self,item):return '⠿  '+('○ ' if not item.get('visible',True) else '')+self.t(item.get('name',item['kind']))
 def layer_button(self,tree,event):
  if event.button!=1:return False
  hit=tree.get_path_at_pos(int(event.x),int(event.y))
  if hit and hit[1] in self.layer_buttons:
   path,column,_,_=hit;key=self.layer_buttons[column];row=self.layer_model[path];self.command('editor-property',id=row[0],key=key,value=not row[2 if key=='locked' else 3]);return True
  return False
 def select_layer(self,selection):
  if self.loading:return
  model,it=selection.get_selected()
  if it:self.command('editor-select',True,id=model[it][0])
 def reordered(self,*_):
  if not self.loading:self.command('editor-order',ids=[row[0] for row in self.layer_model])
 def property(self,key,value,gesture=None):
  args={'id':self.state.get('selectedId'),'key':key,'value':value}
  if gesture:args['gesture']=gesture;self.defer('editor-property',**args)
  else:self.command('editor-property',**args)
 def build_properties(self):
  self.clear(self.properties);item=self.state.get('selected');self.selected=self.state.get('selectedId')
  if not item:self.pack(self.properties,self.label('Selecciona un objeto'));self.properties.show_all();return
  self.loading=True;self.entry(self.properties,'Nombre',item.get('name',''),lambda v:self.property('name',v));self.toggle(self.properties,'Visible',item.get('visible',True),lambda v:self.property('visible',v));self.toggle(self.properties,'Bloqueado',item.get('locked',False),lambda v:self.property('locked',v));self.toggle(self.properties,'Mantener proporciones',item.get('keepAspect',True),lambda v:self.property('keepAspect',v));actions=Gtk.Box(spacing=6)
  for title,op in [('Centrar','editor-center'),('Ajustar a pantalla','editor-fit')]:self.pack(actions,self.button(title,lambda v=op:self.command(v)))
  self.pack(self.properties,actions);self.number(self.properties,'Giro del objeto',item.get('rotation',0),-360,360,1,lambda v,g:self.property('rotation',v,g));self.number(self.properties,'Opacidad',item.get('opacity',1),0,1,.01,lambda v,g:self.property('opacity',v,g));kind=item['kind']
  if kind in ('image','widget'):self.pack(self.properties,self.label(pathlib.Path(item.get('asset','')).name,dim=True))
  if kind in ('text','clock'):
   if kind=='text':
    self.pack(self.properties,self.label('Texto'));text=Gtk.TextView();text.set_wrap_mode(Gtk.WrapMode.WORD_CHAR);text.set_size_request(-1,90);buffer=text.get_buffer();buffer.set_text(item.get('text',''));gesture=uuid.uuid4().hex;buffer.connect('changed',lambda b:self.property('text',b.get_text(b.get_start_iter(),b.get_end_iter(),True),gesture));self.pack(self.properties,text)
   self.font(self.properties,'Fuente',item.get('font','System'),lambda v:self.property('font',v))
   if kind=='text':self.color(self.properties,'Color',item.get('color','#ffffff'),lambda v:self.property('color',v));self.gradient(self.properties,'color',item.get('gradients',{}).get('color'),lambda v:self.defer('editor-gradient',id=item['id'],key='color',value=v))
  if kind=='text':
   self.number(self.properties,'Tamaño de texto (0 = automático)',item.get('textSize',0),0,512,1,lambda v,g:self.property('textSize',v,g));self.toggle(self.properties,'Negrita',item.get('textBold',False),lambda v:self.property('textBold',v));self.toggle(self.properties,'Cursiva',item.get('textItalic',False),lambda v:self.property('textItalic',v));self.choice(self.properties,'Alineación',['left','center','right'],item.get('textAlign','center'),lambda v:self.property('textAlign',v),['Izquierda','Centro','Derecha']);self.choice(self.properties,'Animación de texto',['none','float','pulse','typewriter','scroll'],item.get('textAnimation','none'),lambda v:self.property('textAnimation',v),['Sin animación','Flotar','Pulso','Máquina de escribir','Desplazar']);self.number(self.properties,'Velocidad de animación',item.get('textSpeed',1),.1,5,.1,lambda v,g:self.property('textSpeed',v,g));self.number(self.properties,'Recorrido de animación',item.get('textDistance',12),0,200,1,lambda v,g:self.property('textDistance',v,g))
  group={'orb':'Orbe','spectrum':'Espectro','card':'Tarjeta','clock':'Reloj','stars':'Fondo animado','mesh':'Fondo animado'}.get(kind)
  for spec in self.state.get('specs',[]):
   key=spec['key']
   if spec['group']!=group:continue
   value=item.get('style',{}).get(key,self.state.get('defaults',{}).get(key,spec.get('value',0)))
   if spec['kind'] in ('bool','toggle'):self.toggle(self.properties,spec['title'],value,lambda v,k=key:self.command('editor-style',id=item['id'],key=k,value=v))
   elif spec['kind']=='slider':self.number(self.properties,spec['title'],value,spec['min'],spec['max'],spec['step'],lambda v,g,k=key:self.defer('editor-style',id=item['id'],key=k,value=v,gesture=g))
   elif spec['kind']=='color':self.color(self.properties,spec['title'],value,lambda v,k=key:self.command('editor-style',id=item['id'],key=k,value=v));self.gradient(self.properties,key,item.get('gradients',{}).get(key),lambda v,k=key:self.defer('editor-gradient',id=item['id'],key=k,value=v))
  self.loading=False;self.properties.show_all()
 def add_object(self,kind,folder=False):
  path=None
  if kind in ('image','widget'):
   path=self.chooser('Elegir archivo…',patterns=MEDIA if kind=='image' else ['*.html','*.htm','*.zip'],folder=folder)
   if not path:return
  args={'kind':kind}
  if path:args['path']=path
  self.command('editor-add',True,**args)
 def cancel_editor(self):
  if self.state.get('dirty') or self.pending:
   dialog=Gtk.MessageDialog(transient_for=self.window,modal=True,message_type=Gtk.MessageType.QUESTION,buttons=Gtk.ButtonsType.NONE,text=self.t('¿Descartar los cambios de este diseño?'));dialog.set_keep_above(True);dialog.add_button(self.t('Seguir editando'),Gtk.ResponseType.CANCEL);dialog.add_button(self.t('Descartar'),Gtk.ResponseType.ACCEPT);choice=dialog.run();dialog.destroy()
   if choice!=Gtk.ResponseType.ACCEPT:self.command('editor-continue');return
  self.command('editor-discard')
 def editor_key(self,window,event):
  if isinstance(window.get_focus(),(Gtk.Entry,Gtk.TextView,Gtk.SpinButton)):return False
  key=Gdk.keyval_name(event.keyval);control=bool(event.state&Gdk.ModifierType.CONTROL_MASK)
  op={'z':'editor-undo','y':'editor-redo','d':'editor-duplicate','s':'editor-save'}.get(key) if control else {'Delete':'editor-delete','BackSpace':'editor-delete'}.get(key)
  if op:self.command(op,True);return True
  if key=='Escape':self.cancel_editor();return True
  return False
 def build_experience(self):
  self.clear(self.box);self.fixed=Gtk.Fixed();self.pack(self.box,self.fixed,True);self.splash_logo=Gtk.Image.new_from_pixbuf(GdkPixbuf.Pixbuf.new_from_file_at_scale(str(ROOT/'Assets/WirePulse.png'),112,112,True));self.splash_name=self.label('WirePulse',30);self.reveal=Gtk.Revealer();self.reveal.set_transition_type(Gtk.RevealerTransitionType.SLIDE_RIGHT);self.reveal.set_transition_duration(int(self.state.get('duration',6.136)*1000));self.reveal.add(self.splash_name);self.fixed.put(self.splash_logo,104,42);self.fixed.put(self.reveal,86,174)
  if self.state.get('update'):self.experience_page('update');return
  if self.state.get('direct'):self.experience_page('news');return
  self.rpc('start');self.started=time.monotonic();self.reveal.set_reveal_child(True)
  def intro():
   if self.finished:return False
   elapsed=time.monotonic()-self.started;self.splash_name.set_opacity(min(1,elapsed/max(.1,self.state.get('duration',6.136)/2)))
   if elapsed>=self.state.get('duration',6.136)+.2:
    if self.state.get('setup'):self.experience_page('setup')
    elif self.state.get('news'):self.experience_page('news')
    else:self.destroy()
    return False
   return True
  GLib.timeout_add(16,intro)
 def experience_page(self,phase):
  self.phase=phase
  def content():
   self.clear(self.box);self.header.set_show_close_button(True);self.header.set_no_show_all(False);self.header.show_all();self.window.set_decorated(True)
   scroll=Gtk.ScrolledWindow();scroll.set_policy(Gtk.PolicyType.NEVER,Gtk.PolicyType.AUTOMATIC);self.pack(self.box,scroll,True);body=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=18);body.set_border_width(24);scroll.add(body);self.expanded=True
   if phase=='setup':
    self.pack(body,self.label('Vamos a preparar tu escritorio',24));choices={}
    for key,title in [('startup','Abrir WirePulse al iniciar sesión'),('automatic','Comprobar actualizaciones automáticamente'),('splash','Mostrar la splash en cada inicio')]:choices[key]=self.toggle(body,title,self.state.get(key,False),lambda v:None)
    channel=self.choice(body,'Canal de actualizaciones',['stable','beta'],self.state.get('channel','stable'),lambda v:None,['Estable','Beta (prereleases)'])
    def save():self.rpc('save',lambda s:(setattr(self,'state',s),self.experience_page('news') if s.get('news') else self.finish()),**{k:w.get_active() for k,w in choices.items()},priority=False,channel=channel.get_active_id())
    self.footer(body,save)
   elif phase=='news':
    title=self.label('Novedades de\nWirePulse',32);title.set_justify(Gtk.Justification.CENTER);title.set_xalign(.5);self.pack(body,title);version=self.label(self.state.get('version',''));version.set_xalign(.5);self.pack(body,version)
    for note in self.state.get('notes',[]):
     row=Gtk.Box(spacing=16);icon={'music':'audio-x-generic-symbolic','update':'view-refresh-symbolic'}.get(note.get('icon'),'starred-symbolic');image=Gtk.Image.new_from_icon_name(icon,Gtk.IconSize.DIALOG);image.set_valign(Gtk.Align.START);self.pack(row,image);copy=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=6);self.pack(copy,self.label(note['title'],18));self.pack(copy,self.label(note['description']));self.pack(row,copy,True);self.pack(body,row)
    self.footer(body,lambda:self.finish(True))
   else:
    self.pack(body,self.label('Hay una nueva versión de WirePulse.',24));self.pack(body,self.label(self.state.get('version','')+' → '+self.state.get('update','')));self.widgets['status']=self.pack(body,self.label(self.state.get('status','')));self.widgets['progress']=self.pack(body,Gtk.ProgressBar());actions=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=12);self.widgets['actions']=actions
    self.pack(actions,self.button('Actualizar',lambda:self.command('install')));self.pack(actions,self.button('Omitir esta versión',lambda:self.rpc('skip',lambda _:self.destroy())));self.pack(actions,self.button('No me vuelvas a preguntar',lambda:self.rpc('never',lambda _:self.destroy())));self.pack(body,actions)
   self.box.show_all();self.fit_window()
   if phase!='update':self.rpc('expanded')
  if self.expanded:content();return
  self.reveal.set_transition_duration(0);self.reveal.set_reveal_child(True);self.splash_name.set_opacity(1);start=time.monotonic()
  def expand():
   p=min(1,(time.monotonic()-start)/.8);e=1-(1-p)**3;w,h=int(320+300*e),int(280+360*e);screen=self.window.get_screen();area=screen.get_monitor_workarea(screen.get_monitor_at_window(self.window.get_window()));self.window.resize(min(w,area.width-32),min(h,area.height-64));self.window.move(area.x+(area.width-min(w,area.width-32))//2,area.y+(area.height-min(h,area.height-64))//2);self.fixed.move(self.splash_logo,int(104-76*e),int(42-18*e));self.fixed.move(self.reveal,int(86-2*e),int(174-143*e))
   if p>=1:content();return False
   return True
  GLib.timeout_add(16,expand)
 def footer(self,body,action):
  line=Gtk.Box(spacing=10);mute=self.button('Silenciar música',lambda:self.rpc('mute',lambda s:mute.set_label(self.t('Activar música' if s.get('muted') else 'Silenciar música'))));self.pack(line,mute);finish=self.button('Continuar',action);finish.get_style_context().add_class('suggested-action');line.pack_end(finish,False,False,0);self.pack(body,line)
 def finish(self,ack=False):
  if self.finishing:return
  self.finishing=True;self.box.set_sensitive(False)
  def complete(_):
   self.rpc('finish')
   def wait():
    self.rpc('get',lambda s:self.destroy() if s.get('finished') else None);return not self.finished
   GLib.timeout_add(100,wait)
  if ack:self.rpc('ack',complete)
  else:complete(None)
 def close(self,*_):
  if self.args.mode=='editor':self.editor_hidden=True;self.window.hide();return True
  if self.args.mode=='experience':
   if self.state.get('installing'):return True
   if self.expanded and self.phase!='update':self.finish(self.phase=='news');return True
  self.destroy();return True
 def destroy(self):
  if self.finished:return
  if self.args.test_output and self.args.mode=='experience' and self.finishing:
   duration=time.monotonic()-getattr(self,'smoke_close_time',time.monotonic());pathlib.Path(self.args.test_output+'-experience.json').write_text(json.dumps({'valid':duration>=.5,'closeSeconds':duration,'nativeGTK':True,'topmost':True}))
  self.finished=True;self.window.destroy()
  if hasattr(self,'toolbar'):self.toolbar.destroy()
  self.pool.shutdown(wait=False);Gtk.main_quit()

 def capture(self,name):
  for _ in range(30):
   if not Gtk.events_pending():break
   Gtk.main_iteration_do(False)
  Gdk.Window.process_all_updates()
  window=self.window.get_window();width,height=window.get_width(),window.get_height();pixbuf=Gdk.pixbuf_get_from_window(window,0,0,width,height)
  if pixbuf:pixbuf.savev(self.args.test_output+'-'+name+'.png','png',[],[])
 def smoke(self):
  try:
   if self.args.mode=='settings':
    for group in GROUPS:self.group=group;self.build_page()
    expected={s['key'] for s in self.state['specs'] if s['key'] not in LAYOUT};assert self.covered==expected,(self.covered,expected);assert set(ICONS)==set(GROUPS)
    self.group='Audio';self.build_page();self.capture('settings')
    def descendants(w):
     yield w
     if isinstance(w,Gtk.Container):
      for child in w.get_children():yield from descendants(child)
    scales=[w for w in descendants(self.page) if isinstance(w,Gtk.Scale)];assert scales
    value=scales[0].get_value();scales[0].set_value(value+.1);self.flush(True)
    def verify(s):
     assert abs(s['global']['values']['sensitivity']-(value+.1))<.001,s['global']['values']['sensitivity']
     self.command('edit');self.window.hide();GLib.timeout_add(300,self.smoke_wait_editor)
    self.rpc('get',verify);return False
   if self.args.mode=='editor':
    assert self.window.get_titlebar()!=None;self.toggle_properties();assert not self.window.get_visible();self.toggle_properties();assert self.window.get_visible()
    def step(operations,final):
     if not operations:final();return
     op,values=operations[0]
     def next(s):self.apply(s,True);step(operations[1:],final)
     self.rpc(op,next,**values)
    def after_text():
     item=self.state['selected'];assert item['kind']=='text'
     edits=[('editor-property',{'key':'text','value':'GTK · WirePulse'}),('editor-property',{'key':'rotation','value':35}),('editor-property',{'key':'opacity','value':.6}),('editor-gradient',{'key':'color','value':{'enabled':True,'end':'#00ff00','angle':45}})]
     def properties_done():
      selected=self.state['selected'];assert selected['rotation']==35 and abs(selected['opacity']-.6)<.001 and selected['text']=='GTK · WirePulse' and selected['gradients']['color']['enabled'];self.capture('editor');path=str(ROOT.parent/'tests/fixtures/disposal.gif');step([('editor-add',{'kind':'image','path':path})],after_image)
     step(edits,properties_done)
    def after_image():
     assert self.state['selected']['kind']=='image';old=[i['id'] for i in self.state['items']];ordered=list(reversed(old))
     def reordered(s):
      self.apply(s,True);assert [i['id'] for i in s['items']]==ordered
      step([('editor-undo',{}),('editor-redo',{}),('editor-property',{'key':'locked','value':True})],after_lock)
     self.rpc('editor-order',reordered,ids=ordered)
    def after_lock():
     count=len(self.state['items']);assert self.state['selected']['locked']
     def kept(s):
      self.apply(s,True);assert len(s['items'])==count
      step([('editor-property',{'key':'locked','value':False}),('editor-snap',{'value':False}),('editor-snap',{'value':True})],save)
     self.rpc('editor-delete',kept)
    def save():
     report={'valid':True,'nativeProperties':True,'reorder':True,'undoRedo':True,'rotation':True,'gradient':True,'gif':True,'lock':True,'snap':True,'propertiesToggle':True};pathlib.Path(self.args.test_output+'-editor.json').write_text(json.dumps(report));self.command('editor-save')
    step([('editor-add',{'kind':'text'})],after_text);return False
   if self.phase=='intro':return True
   if not self.expanded:return True
   assert self.window.get_titlebar()!=None and self.window.get_decorated()
   assert self.window.get_window().get_state() & Gdk.WindowState.ABOVE,'Welcome is not above normal windows'
   if self.phase=='setup':
    self.capture('setup');self.experience_page('news');return True
   self.capture('news');self.smoke_close_time=time.monotonic();self.window.close();self.window.close();return False
  except Exception as e:self.error('GTK regression: '+str(e));return False
 def smoke_wait_editor(self):
  try:
   path=pathlib.Path(self.args.test_output+'-editor.json')
   if not path.exists():return True
   self.smoke_editor=json.loads(path.read_text());assert self.smoke_editor['valid'];self.command('gtk-test-welcome');GLib.timeout_add(300,self.smoke_wait_welcome);return False
  except Exception as e:self.error(str(e));return False
 def smoke_wait_welcome(self):
  try:
   path=pathlib.Path(self.args.test_output+'-experience.json')
   if not path.exists():return True
   experience=json.loads(path.read_text());assert experience['valid'];self.rpc('gtk-test-finish',None,value={'valid':True,'gtk':Gtk.get_major_version(),'controls':sorted(self.covered),'editor':self.smoke_editor,'welcome':experience});return False
  except Exception as e:self.error(str(e));return False

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--socket',required=True);parser.add_argument('--mode',choices=['settings','editor','experience'],required=True);parser.add_argument('--test-output');args=parser.parse_args();Gtk.init(None);app=Companion(args);Gtk.main()
if __name__=='__main__':main()
