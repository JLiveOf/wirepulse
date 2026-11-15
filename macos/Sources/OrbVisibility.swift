import simd

/// Conservative bounds of complete wire edges. Rear wires remain visible through the mesh.
/// Disabling this helper preserves the original projection and draw order.
struct OrbVisibility {
 private struct Patch {var edges:[Int];var vertices:[Int];var center:SIMD3<Float>;var radius:Float;var bins:[Int]}
 private var patches=[Patch](),vertices=[Bool](),edges=[Bool]()
 private var all=true
 func vertex(_ i:Int)->Bool {all || vertices[i]}
 func edge(_ i:Int)->Bool {all || edges[i]}
 mutating func rebuild(mesh:[MeshPoint],edges source:[(Int,Int)]) {
  vertices=Array(repeating:false,count:mesh.count);edges=Array(repeating:false,count:source.count)
  var groups=[Int:[Int]]()
  for (i,edge) in source.enumerated(){let a=mesh[edge.0],b=mesh[edge.1],x=(a.x+b.x)*0.5,y=(a.y+b.y)*0.5,z=(a.z+b.z)*0.5
   let latitude=min(7,max(0,Int(acos(min(1,max(-1,y)))/Float.pi*8))),longitude=min(15,max(0,Int((atan2(z,x)+Float.pi)/(2*Float.pi)*16)))
   groups[latitude*16+longitude,default:[]].append(i)
  }
  patches=groups.values.map{indices in
   let ids=Array(Set(indices.flatMap{[source[$0].0,source[$0].1]}))
   var lo=SIMD3<Float>(repeating:1),hi=SIMD3<Float>(repeating:-1)
   for i in ids{let p=mesh[i],v=SIMD3(p.x,p.y,p.z);lo=simd_min(lo,v);hi=simd_max(hi,v)}
   let center=(lo+hi)*Float(0.5)
   let radius:Float=ids.map{let p=mesh[$0];return simd_distance(center,SIMD3(p.x,p.y,p.z))}.max() ?? Float(0)
   return Patch(edges:indices,vertices:ids,center:center,radius:radius+0.00001,bins:Array(Set(ids.map{mesh[$0].bin})))
  }
 }
 mutating func prepare(bands:[Float],angle:Float,radius:Float,center:SIMD2<Float>,deformation:Float,full:Bool,width:Float,height:Float,margin:Float,enabled:Bool){
  all = !enabled
  if all{return}
  let factor:Float=full ? 0.38:0.07,extent=radius*(1+deformation*factor*(bands.max() ?? 0))
  if center.x-extent >= -margin && center.x+extent <= width+margin && center.y-extent >= -margin && center.y+extent <= height+margin{all=true;return}
  for i in vertices.indices{vertices[i]=false};for i in edges.indices{edges[i]=false}
  let ca=cos(angle),sa=sin(angle),ct=cos(Float(0.23)),st=sin(Float(0.23))
  for patch in patches{
   var energy:Float=0;for bin in patch.bins{energy=max(energy,bands[bin])}
   let growth=1+deformation*factor*energy,mid=(1+growth)*0.5,c=patch.center
   let x=c.x*ca-c.z*sa,z=c.x*sa+c.z*ca,y=c.y*ct-z*st
   let r=radius*(patch.radius*growth+simd_length(c)*(growth-1)*0.5)+0.01,cx=center.x+x*radius*mid,cy=center.y+y*radius*mid
   if cx+r < -margin || cx-r > width+margin || cy+r < -margin || cy-r > height+margin{continue}
   for i in patch.vertices{vertices[i]=true};for i in patch.edges{edges[i]=true}
  }
 }
}
