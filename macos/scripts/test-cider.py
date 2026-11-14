#!/usr/bin/env python3
"""Read-only Cider v2/v1 protocol fixtures, without an Apple account."""
import http.server,json,subprocess,sys,threading
for version,tempo,expected in [(v,t,e) for v in (2,1) for t,e in [(None,0),(128.5,128.5),("128.5",128.5),(True,0),(9999,0)]]:
 class Handler(http.server.BaseHTTPRequestHandler):
  def log_message(self,*args):pass
  def do_GET(self):
   assert self.headers.get('apptoken')=='fixture-token'
   info={'name':'WirePulse fixture','artistName':'WirePulse','albumName':'Generated fixture','durationInMillis':60000,'currentPlaybackTime':12,'artwork':{'url':'https://example.invalid/{w}x{h}.jpg'}}
   if tempo is not None:info['bpm']=tempo
   if self.path=='/api/v2/playback':
    if version==1:self.send_response(404);self.end_headers();return
    data={'data':{'state':'playing','nowPlaying':info,'time':{'currentTime':12,'duration':60}}}
   elif self.path=='/api/v1/playback/now-playing':data={'info':info}
   elif self.path=='/api/v1/playback/is-playing':data={'is_playing':True}
   else:raise AssertionError(self.path)
   self.send_response(200);self.end_headers();self.wfile.write(json.dumps(data).encode())
 server=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
 subprocess.run([sys.argv[1],'--cider-fixture-test',f'http://127.0.0.1:{server.server_port}','--expected-bpm',str(expected)],check=True);server.shutdown()
 print(f'PASS Cider v{version}, optional BPM {tempo!r} resolves as {expected}')
