using Avalonia;
using SkiaSharp;
using System.Numerics;
using WirePulse;
var random=new Random(731);int cases=0,removed=0;
foreach(int density in new[]{12,42,72})foreach(bool full in new[]{false,true})foreach(double glow in new[]{0d,15d})foreach(var center in new[]{(160d,90d),(320d,180d),(-50d,200d),(170d,-45d),(900d,900d)}){
 var mesh=new OrbMesh(density);var bands=Enumerable.Range(0,64).Select(_=>random.NextDouble()).ToArray();double angle=random.NextDouble()*Math.PI*2,time=random.NextDouble()*100,radius=150,deformation=2;var legacy=new Vector3[mesh.Vertices.Length];var optimized=new Vector3[legacy.Length];mesh.Project(legacy,bands,angle,time,radius,center.Item1,center.Item2,deformation,full);
 using var before=Draw(legacy,false);double pad=glow+4;mesh.Project(optimized,bands,angle,time,radius,center.Item1,center.Item2,deformation,full,new Rect(-pad,-pad,320+pad*2,180+pad*2));removed+=legacy.Length-mesh.Visibility.ProjectedVertices;
 using var after=Draw(optimized,true);if(!before.Bytes.AsSpan().SequenceEqual(after.Bytes))throw new Exception($"Pixel mismatch density={density}, full={full}, glow={glow}, center={center}");cases++;
 SKBitmap Draw(Vector3[] points,bool cull){var image=new SKBitmap(320,180);using var canvas=new SKCanvas(image);canvas.Clear(SKColors.Transparent);using var paint=new SKPaint{IsAntialias=true,Style=SKPaintStyle.Stroke};for(int pass=0;pass<2;pass++)for(int i=0;i<mesh.Edges.Length;i++){if(cull&&!mesh.Visibility.Edge(i))continue;var(a,b)=mesh.Edges[i];var p=points[a];var q=points[b];double z=(p.Z+q.Z)*.5;if(!full&&Math.Abs(z)>.25)continue;if((z>0)!=(pass==1))continue;paint.StrokeWidth=(float)(.65+glow);paint.Color=new SKColor(255,30,60,8);canvas.DrawLine(p.X,p.Y,q.X,q.Y,paint);paint.StrokeWidth=.65f;paint.Color=new SKColor(255,30,60,(byte)(pass==1?217:61));canvas.DrawLine(p.X,p.Y,q.X,q.Y,paint);}return image;}
}
if(removed==0)throw new Exception("Optimized rendering never culled any projection");Console.WriteLine($"PASS {cases} pixel-identical legacy/optimized frames, {removed:N0} vertex projections avoided; full/reduced mesh, glow, density, deformation and offscreen positions.");
if(args.Contains("--benchmark"))foreach(int density in new[]{24,42,72})foreach(var center in new[]{(960d,540d),(1862d,1328d)}){
 var mesh=new OrbMesh(density);var bands=Enumerable.Repeat(.45d,64).ToArray();var points=new Vector3[mesh.Vertices.Length];foreach(bool optimized in new[]{false,true}){for(int i=0;i<100;i++)mesh.Project(points,bands,.7,i/60d,594,center.Item1,center.Item2,1,true,optimized?new Rect(-20,-20,1960,1120):null);var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<2000;i++)mesh.Project(points,bands,.7,i/60d,594,center.Item1,center.Item2,1,true,optimized?new Rect(-20,-20,1960,1120):null);Console.WriteLine($"PROJECTION density={density} center={center} optimized={optimized} us/frame={watch.Elapsed.TotalMicroseconds/2000:F2} projected={mesh.Visibility.ProjectedVertices}/{mesh.Vertices.Length}");}
}
