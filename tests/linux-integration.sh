#!/usr/bin/env bash
set -euo pipefail
export DISPLAY=:97 XDG_SESSION_TYPE=x11 XDG_CURRENT_DESKTOP=X-Cinnamon LIBGL_ALWAYS_SOFTWARE=1
export XDG_CONFIG_HOME="$(mktemp -d)"
mkdir -p test-results
Xvfb :97 -screen 0 1280x720x24 +extension GLX +extension Composite >test-results/xvfb.log 2>&1 &
trap 'jobs -pr | xargs -r kill || true' EXIT
sleep 2
cinnamon --replace >test-results/cinnamon.log 2>&1 &
sleep 8
nemo-desktop >test-results/nemo.log 2>&1 &
pulseaudio --start --exit-idle-time=-1
pactl load-module module-null-sink sink_name=wirepulse_test rate=48000 channels=2
pactl set-default-sink wirepulse_test
python3 - <<'PY'
import wave,math,struct
with wave.open('test-results/tone.wav','wb') as f:
 f.setparams((2,2,48000,0,'NONE','not compressed'))
 data=b''.join(struct.pack('<hh',int(16000*math.sin(2*math.pi*1000*i/48000)),-int(16000*math.sin(2*math.pi*1000*i/48000))) for i in range(48000))
 for i in range(45):f.writeframes(data)
PY
paplay --device=wirepulse_test test-results/tone.wav &
/usr/bin/python3 tests/mpris-fixture.py >test-results/mpris.log 2>&1 &
./publish/WirePulse --no-settings --probe-seconds 22 --probe-output "$PWD/test-results/status.json" --render-output "$PWD/test-results/render.png" >test-results/app.log 2>&1 &
app_pid=$!
sleep 17
xprop -name 'WirePulse Desktop' _NET_WM_WINDOW_TYPE _NET_WM_STATE >test-results/x11-properties.txt
import -window root test-results/desktop.png
wait "$app_pid"
python3 - <<'PY'
import json
from PIL import Image
s=json.load(open('test-results/status.json'))
assert s['frames']>50 and abs(s['peak']-1000)<10,s
assert s['rate']==48000 and s['fft']==8192,s
assert s['renderFrames']>100,s
assert s['provider']=='Cider' and s['title']=='WirePulse integration tone',s
props=open('test-results/x11-properties.txt').read()
assert '_NET_WM_WINDOW_TYPE_DESKTOP' in props and '_NET_WM_STATE_BELOW' in props,props
im=Image.open('test-results/desktop.png').convert('RGB')
# Card must be visible in the composited desktop, with Nemo desktop present.
region=im.crop((430,32,850,150))
assert sum(max(p)>35 for p in region.getdata())>700,'Wallpaper is hidden behind the desktop'
print('PASS: real monitor capture + 1kHz FFT + Cider MPRIS + Cinnamon/Nemo desktop surface')
PY
