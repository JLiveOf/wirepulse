using System.Numerics;
namespace WirePulse;

public readonly record struct OrbVertex(double X, double Y, double Z, int Band, double SinPhase, double CosPhase, double Envelope);
public sealed class OrbMesh
{
    public OrbVertex[] Vertices { get; }
    public (int A, int B)[] Edges { get; }
    public OrbVisibility Visibility{get;}
    public OrbMesh(int density)
    {
        density = Math.Clamp(density, 8, 72); int columns = density * 2; var vertices = new List<OrbVertex>(); var edges = new List<(int, int)>();
        void Add(double lat, double lon, int band, double phase) { double s = Math.Sin(lat); vertices.Add(new(s * Math.Cos(lon), Math.Cos(lat), s * Math.Sin(lon), band, Math.Sin(phase), Math.Cos(phase), s * s)); }
        // A single vertex per pole. Duplicated poles with independent radial noise create needles.
        Add(0, 0, 0, 0);
        for (int j = 1; j < density; j++) for (int i = 0; i < columns; i++) { double lat = j / (double)density * Math.PI; Add(lat, i / (double)columns * Math.PI * 2, Math.Min(63, (int)((1 - Math.Abs(Math.Cos(lat))) * 46 + i / (double)columns * 17)), i * 2.399 + j * .7); }
        int bottom = vertices.Count; Add(Math.PI, 0, 0, 0);
        for (int i = 0; i < columns; i++) edges.Add((0, 1 + i));
        for (int j = 0; j < density - 1; j++) for (int i = 0; i < columns; i++) { int a = 1 + j * columns + i; edges.Add((a, 1 + j * columns + (i + 1) % columns)); if (j < density - 2) { edges.Add((a, a + columns)); edges.Add((a, 1 + (j + 1) * columns + (i + 1) % columns)); } else edges.Add((a, bottom)); }
        Vertices = vertices.ToArray(); Edges = edges.ToArray(); Visibility=new OrbVisibility(this);
    }
    public static bool ClipFront(ref Vector3 a,ref Vector3 b){
        if(a.Z<0&&b.Z<0)return false;
        if(a.Z<0)a=Vector3.Lerp(a,b,-a.Z/(b.Z-a.Z));
        else if(b.Z<0)b=Vector3.Lerp(b,a,-b.Z/(a.Z-b.Z));
        return true;
    }
    public void Project(Span<Vector3> output, ReadOnlySpan<double> bands, double angle, double time, double radius, double x, double y, double deformation, bool full,Avalonia.Rect? visibleBounds=null)
    {
        Visibility.Prepare(bands,angle,radius,x,y,deformation,full,visibleBounds);
        double ca = Math.Cos(angle), sa = Math.Sin(angle), ct = Math.Cos(.23), st = Math.Sin(.23), sp = Math.Sin(time * 1.7), cp = Math.Cos(time * 1.7);
        for (int i = 0; i < Vertices.Length; i++) { if(!Visibility.Vertex(i))continue;var p = Vertices[i]; double energy = bands[p.Band], noise = p.SinPhase * cp + p.CosPhase * sp; double d = 1 + deformation * p.Envelope * energy * ((full ? .22 : .045) + noise * (full ? .16 : .025)); double px = (p.X * ca - p.Z * sa) * d, py = p.Y * d, pz = (p.X * sa + p.Z * ca) * d; output[i] = new((float)(x + px * radius), (float)(y + (py * ct - pz * st) * radius), (float)(pz * ct + py * st)); }
    }
}
