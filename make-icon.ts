// Adaptation of OrbMusic's code-drawn icon for a macOS iconset.
// Optional: bun install && bun make-icon.ts
import {createCanvas} from '@napi-rs/canvas';
import {mkdirSync} from 'node:fs';
const dest=import.meta.dir+'/Resources/WirePulse.iconset';mkdirSync(dest,{recursive:true});
function icon(size:number){
 const c=createCanvas(size,size),x=c.getContext('2d');x.scale(size/1024,size/1024);
 const a=92,b=840,r=190;
 x.shadowColor='#00000055';x.shadowBlur=28;x.shadowOffsetY=14;x.fillStyle='#102039';x.beginPath();x.roundRect(a,a,b,b,r);x.fill();x.shadowBlur=0;x.shadowOffsetY=0;
 x.save();x.beginPath();x.roundRect(a,a,b,b,r);x.clip();
 const bg=x.createLinearGradient(150,90,850,950);bg.addColorStop(0,'#1e3e66');bg.addColorStop(.5,'#0d2039');bg.addColorStop(1,'#030812');x.fillStyle=bg;x.fillRect(a,a,b,b);
 const haze=x.createRadialGradient(790,770,20,790,770,620);haze.addColorStop(0,'#ff164325');haze.addColorStop(1,'#ff164300');x.fillStyle=haze;x.fillRect(a,a,b,b);
 x.strokeStyle='#ff315b';x.lineWidth=size<=32?10:5.5;x.shadowColor='#ff194c';x.shadowBlur=size<=32?0:9;
 const cx=782,cy=746,rad=370,n=size<=32?5:9;
 for(let i=0;i<n;i++){x.beginPath();x.ellipse(cx,cy,rad*Math.sin((i+1)/(n+1)*Math.PI),rad,-.12,0,2*Math.PI);x.stroke();}
 for(let j=-3;j<=3;j++){const y=j*rad/3.5;x.beginPath();x.ellipse(cx+Math.sin(.12)*y,cy+y,Math.sqrt(Math.max(0,rad*rad-y*y)),rad*.105,-.12,0,2*Math.PI);x.stroke();}
 x.shadowBlur=0;
 const rows=size<=32?10:16,top=195,span=660,step=span/rows;
 const widths=[3,4,3,6,8,6,4,5,8,11,9,7,5,8,6,4];
 for(let j=0;j<rows;j++){
  const cells=size<=32?Math.max(2,Math.round(widths[Math.floor(j*16/rows)]*.6)):widths[j];
  for(let k=0;k<cells;k++){x.fillStyle=k<4?'#8cff26':k<7?'#f1e63b':'#ff3c5e';x.fillRect(157+k*19,top+j*step,14,Math.max(13,step*.58));}
 }
 const shine=x.createLinearGradient(0,a,0,a+400);shine.addColorStop(0,'#ffffff14');shine.addColorStop(1,'#ffffff00');x.fillStyle=shine;x.fillRect(a,a,b,400);x.restore();
 x.strokeStyle='#ffffff27';x.lineWidth=2;x.beginPath();x.roundRect(a+1,a+1,b-2,b-2,r);x.stroke();
 return c.toBuffer('image/png');
}
for(const n of [16,32,128,256,512]){await Bun.write(`${dest}/icon_${n}x${n}.png`,icon(n));await Bun.write(`${dest}/icon_${n}x${n}@2x.png`,icon(n*2));}
await Bun.write(import.meta.dir+'/Resources/WirePulse-icono.png',icon(1024));
