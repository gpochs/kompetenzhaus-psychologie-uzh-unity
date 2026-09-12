"""Own mesh production from the reviewed image references. No paid services.

blender --background --python art/blender/build_assets.py -- <repository>
The default produces Blender sources and review images only. Add --export after
the recorded Form review (or explicit user-delegated review) for FBX output.
"""
import bpy, math, json, sys, hashlib
from datetime import datetime, timezone
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

if '--' not in sys.argv or not sys.argv[sys.argv.index('--')+1:]:
    raise RuntimeError('Pass -- <repository> [--export] after the Blender arguments.')
ARGS=sys.argv[sys.argv.index('--')+1:]
ROOT=Path(ARGS[0]).resolve()
REVIEW=ROOT/'art'/'review'
OUT=ROOT/'art'/'models'
decisions=json.loads((REVIEW/'milestone-reviews.json').read_text(encoding='utf-8'))
if decisions['function']['status']!='approved' or not decisions['function'].get('approvedBy'):
    raise RuntimeError('Function reference/layout approval is pending. No production models generated.')
DO_EXPORT='--export' in ARGS
if DO_EXPORT and (decisions['form']['status']!='approved' or not decisions['form'].get('approvedBy')):
    raise RuntimeError('Form review is pending. No FBX export performed.')
manifest=json.loads((REVIEW/'props.json').read_text(encoding='utf-8'))
PROP={item['id']:item for item in manifest['assets']}
# Check source identity before clearing even the disposable Blender scene.
for item in PROP.values():
    reference=(REVIEW/item['image']['source']).resolve()
    if not reference.is_relative_to(ROOT) or hashlib.sha256(reference.read_bytes()).hexdigest()!=item['image']['sha256']:
        raise RuntimeError('Reference identity changed for '+item['id'])
OUT.mkdir(parents=True,exist_ok=True)
def export_reviewed_source():
    """Export the measured Form review source, never silently regenerate it."""
    audit_path=OUT/'asset-audit.json'
    audit=json.loads(audit_path.read_text(encoding='utf-8'))
    source=OUT/audit['sourceBlend']['path']
    if hashlib.sha256(source.read_bytes()).hexdigest()!=audit['sourceBlend']['sha256']:
        raise RuntimeError('Reviewed .blend changed; repeat Form review before export.')
    if hashlib.sha256(Path(__file__).read_bytes()).hexdigest()!=audit.get('generatorScriptSha256'):
        raise RuntimeError('Generator changed after modelling; prepare and review the new source first.')
    if hashlib.sha256((REVIEW/'props.json').read_bytes()).hexdigest()!=audit.get('propsManifestSha256'):
        raise RuntimeError('Asset contract changed after modelling; repeat Form review.')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    target=ROOT/'UnityProject'/'Assets'/'Kompetenzhaus'/'Art'/'Models';target.mkdir(parents=True,exist_ok=True)
    exports=[]
    for name in PROP:
        collection=bpy.data.collections.get(name)
        objects=[o for o in collection.objects if o.type=='MESH'] if collection else []
        if not objects:raise RuntimeError('Reviewed collection missing: '+name)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        path=target/(name+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,
            use_triangles=True,mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False)
        digest=hashlib.sha256(path.read_bytes()).hexdigest()
        exports.append({'assetId':name,'path':'../../UnityProject/Assets/Kompetenzhaus/Art/Models/'+name+'.fbx','sha256':digest})
        print('FBX_EXPORTED',name,digest)
    audit['exports']=exports;audit['exported']=True
    audit['exportedAt']=datetime.now(timezone.utc).isoformat()
    audit_path.write_text(json.dumps(audit,indent=2),encoding='utf-8')
    print('REVIEWED_SOURCE_EXPORTED',len(exports))
if DO_EXPORT:
    export_reviewed_source()
    raise SystemExit(0)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'; bpy.context.scene.unit_settings.scale_length=1.0

PALETTE={
 'Stone':('#d3c7ae',.86,0),'Oak':('#b88b55',.68,0),'Navy':('#173650',.42,.10),
 'Sage':('#758e74',.8,0),'Copper':('#b77b4b',.32,.65),'Glass':('#56838c',.20,0),
 'Leaf':('#71844a',.9,0),'Skin':('#bd875f',.65,0),'Ivory':('#f1ede2',.8,0),
 'Ink':('#162331',.56,0),'Ochre':('#d9ac55',.85,0),'Soil':('#584b38',1,0),
 'OakLight':('#c89d69',.72,0),'OakDark':('#a87d4b',.73,0),
 'NavyFabric':('#294969',.93,0),'SageFabric':('#829079',.94,0),
 'ClothTan':('#b49b78',.94,0),'LeafLight':('#859957',.92,0),
 'WarmLight':('#ffdf9e',.55,0),'StoneJoint':('#b4a68d',.94,0)
}
def rgba(h):
    # Hex colours are sRGB; Principled values are linear. Export the original hex
    # separately so Unity need not reconstruct it from a rendered swatch.
    def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    return tuple(linear(int(h[i:i+2],16)/255) for i in (1,3,5))+(1,)
M={}
for name,(color,rough,metal) in PALETTE.items():
    mat=bpy.data.materials.new('GameMat_'+name); mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=rgba(color)
    shader.inputs['Roughness'].default_value=rough; shader.inputs['Metallic'].default_value=metal
    shader.inputs['Alpha'].default_value=.32 if name=='Glass' else 1.0
    if 'Fabric' in name or name=='ClothTan':
        shader.inputs['Sheen Weight'].default_value=.18
    if name=='WarmLight':
        shader.inputs['Emission Color'].default_value=rgba(color)
        shader.inputs['Emission Strength'].default_value=.7
    mat.diffuse_color=rgba(color); M[name]=mat

assets={}; active=None; guide_piece='GuideBody'
def add(obj,name,mat):
    obj.name=name; obj.data.materials.append(M[mat]); assets[active].append(obj)
    if active=='student-guide':obj['motion_group']=guide_piece
    return obj
def soft_edges(obj,width,segments=2):
    if width<=0:return
    b=obj.modifiers.new('Authored soft edges','BEVEL');b.width=width;b.segments=segments
    b.affect='EDGES';b.harden_normals=True;b.limit_method='ANGLE';b.angle_limit=math.radians(35)
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=b.name)
def box(name,location,size,mat='Stone',bevel=.025,segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location)
    o=bpy.context.object; o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    add(o,name,mat)
    soft_edges(o,min(bevel,min(size)*.45),segments)
    return o
def cylinder(name,location,radius,depth,mat='Navy',vertices=24,bevel=.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=location)
    o=add(bpy.context.object,name,mat)
    soft_edges(o,min(bevel,depth*.2,radius*.15),1)
    for p in o.data.polygons:p.use_smooth=abs(p.normal.z)<.5
    return o
def ellipsoid(name,location,size,mat='Leaf',segments=16,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=location)
    o=bpy.context.object;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    add(o,name,mat)
    for p in o.data.polygons:p.use_smooth=True
    return o
def beam(name,a,b,radius,mat='Oak',vertices=10):
    a,b=Vector(a),Vector(b);o=cylinder(name,(a+b)/2,radius,(b-a).length,mat,vertices,bevel=0)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def mesh_object(name,vertices,faces,mat):
    mesh=bpy.data.meshes.new(name+' mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    return add(obj,name,mat)
def stroke(name,a,b,width,mat='Navy'):
    # Printed chart line: one front-facing quad, not a beveled 3D pipe.
    a,b=Vector(a),Vector(b);t=(b-a).normalized();s=Vector((t.z,0,-t.x))*width/2
    return mesh_object(name,[a-s,b-s,b+s,a+s],[(0,1,2,3)],mat)
def disc(name,centre,radius,mat='Copper',segments=12):
    x,y,z=centre
    return mesh_object(name,[(x+radius*math.cos(i*math.tau/segments),y,z+radius*math.sin(i*math.tau/segments)) for i in range(segments)],
                       [tuple(range(segments))],mat)
def tube(name,points,radius,mat='Copper',sides=10):
    # Parallel XZ path rings: shared vertices give a continuous, economical arc.
    vertices=[];faces=[]
    for i,p in enumerate(points):
        tangent=(Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])).normalized()
        reference=Vector((0,1,0)) if abs(tangent.y)<.95 else Vector((1,0,0))
        side=(reference-tangent*tangent.dot(reference)).normalized();normal=tangent.cross(side).normalized()
        for j in range(sides):
            angle=j*math.tau/sides
            vertices.append(Vector(p)+radius*(math.cos(angle)*side+math.sin(angle)*normal))
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))])
    obj=mesh_object(name,vertices,faces,mat)
    for p in obj.data.polygons:p.use_smooth=True
    return obj
def dome(name,centre,radius,height,mat='Ivory',segments=24,rings=6):
    # Open-bottom hemispherical shell; the diffuser remains visible from below.
    x,y,z=centre;vertices=[(x,y,z+height)];faces=[]
    for ring in range(1,rings+1):
        a=ring*math.pi/(2*rings)
        for j in range(segments):
            b=j*math.tau/segments;vertices.append((x+radius*math.sin(a)*math.cos(b),y+radius*math.sin(a)*math.sin(b),z+height*math.cos(a)))
    for j in range(segments):faces.append((0,1+j,1+(j+1)%segments))
    for ring in range(rings-1):
        for j in range(segments):
            a=1+ring*segments+j;b=1+ring*segments+(j+1)%segments
            faces.append((a,a+segments,b+segments,b))
    obj=mesh_object(name,vertices,faces,mat)
    for p in obj.data.polygons:p.use_smooth=True
    return obj
def arc_block(name,inner,outer,start_angle,end_angle,bottom,height,mat,segments=6,bevel=.025):
    angles=[math.radians(start_angle+(end_angle-start_angle)*i/segments) for i in range(segments+1)]
    vertices=[(r*math.cos(a),r*math.sin(a),z) for z in (bottom,bottom+height) for r in (inner,outer) for a in angles]
    row=segments+1;layer=row*2;faces=[]
    for i in range(segments):
        faces.extend([(i,i+1,row+i+1,row+i),(layer+i,layer+row+i,layer+row+i+1,layer+i+1),
                      (i,layer+i,layer+i+1,i+1),(row+i,row+i+1,layer+row+i+1,layer+row+i)])
    faces.extend([(0,row,layer+row,layer),(segments,layer+segments,layer+row+segments,row+segments)])
    obj=mesh_object(name,vertices,faces,mat);soft_edges(obj,bevel,2)
    return obj
def start(name):
    global active
    active=name; assets[name]=[]
def hole(wall,location,size):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location);cut=bpy.context.object;cut.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=wall.modifiers.new('True opening','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cut
    bpy.context.view_layer.objects.active=wall;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cut,do_unlink=True)

def make_wall(name,style):
    start(name)
    wall=box('Masonry with thickness',(0,0,1.6),(4,.24,3.2),bevel=0)
    if style=='window':
        hole(wall,(0,0,1.7),(3,.8,2.2))
        for x in [-1.5,-.5,.5,1.5]:box('Window mullion',(x,-.01,1.7),(.065,.32,2.28),'Navy',.008)
        for z in [.58,2.30,2.82]:box('Window transom',(0,-.01,z),(3.08,.32,.075),'Navy',.008)
        for x in [-1,0,1]:box('Glass pane',(x,.05,1.7),(.93,.028,2.12),'Glass',.003)
    if style in ('door','partition'):
        hole(wall,(0,0,1.35),(2.4,.8,2.7))
        box('Oak lintel',(0,-.04,2.92),(2.48,.34,.34),'Oak')
        for x in [-1.3,1.3]:box('Portal pier',(x,-.04,1.4),(.18,.34,2.8),'Navy' if style=='door' else 'Oak')
        for x in [-1.85,-1.7,-1.55,1.55,1.7,1.85]:
            box('Acoustic fin',(x,-.16,1.6),(.055,.08,3.08),'Navy' if style=='door' else 'Oak',.006)
        for x in [-.70,.70]:cylinder('Recessed lintel diffuser',(x,-.04,2.755),.055,.01,'WarmLight',16,0)
    box('Cap',(0,0,3.16),(4.04,.29,.12),'Stone',.015)
    box('Skirting',(0,-.13,.09),(4,.035,.18),'Stone',.008) if style not in ('door','partition') else None
    for z in [.8,1.6,2.4,3.05]:
        aperture=1.5 if style=='window' and z<2.8 else 1.2 if style in ('door','partition') and z<2.7 else 0
        spans=[(-1.97,-aperture),(aperture,1.97)] if aperture else [(-1.97,1.97)]
        for left,right in spans:stroke('Limestone bed joint',(left,-.1208,z),(right,-.1208,z),.007,'StoneJoint')
    b=wall.modifiers.new('Masonry edge','BEVEL');b.width=.018;b.segments=2
    bpy.context.view_layer.objects.active=wall;bpy.ops.object.modifier_apply(modifier=b.name)

make_wall('wall-window','window'); make_wall('wall-door','door')
make_wall('wall-solid','solid');make_wall('wall-partition','partition')

start('floor-tile')
box('Foundation slab',(0,0,-.15),(4,4,.3),'Stone',.025)
box('Parquet backing',(0,0,.008),(3.80,3.80,.016),'OakDark',0)
# Herringbone boards are printed surface polygons over a solid slab, avoiding
# hundreds of hidden bottoms and beveled edges. Clip the lattice to the border.
def clip_polygon(poly,axis,bound,keep_less):
    result=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        inside_a=(a[axis]<=bound) if keep_less else (a[axis]>=bound)
        inside_b=(b[axis]<=bound) if keep_less else (b[axis]>=bound)
        if inside_a:result.append(a)
        if inside_a!=inside_b:
            t=(bound-a[axis])/(b[axis]-a[axis]);result.append(tuple(a[k]+t*(b[k]-a[k]) for k in range(2)))
    return result
for a in range(-8,9):
    for b in range(-25,26):
        ox,oy=4*a+b,4*a-b
        for orientation,(x0,y0,x1,y1) in enumerate([(0,0,4,1),(4,0,5,4)]):
            corners=[(x0+.015,y0+.015),(x1-.015,y0+.015),(x1-.015,y1-.015),(x0+.015,y1-.015)]
            poly=[((x+ox-y-oy)*.15/math.sqrt(2),(x+ox+y+oy)*.15/math.sqrt(2)) for x,y in corners]
            for axis in [0,1]:
                poly=clip_polygon(poly,axis,-1.89,False) if poly else []
                poly=clip_polygon(poly,axis,1.89,True) if poly else []
            if len(poly)>=3:
                material=['Oak','OakLight','OakDark'][(a*13+b*7+orientation)%3]
                mesh_object('Herringbone oak',[(x,y,.024) for x,y in poly],[tuple(range(len(poly)))],material)
for x in [-1.9475,1.9475]:box('Limestone border',(x,0,.012),(.105,4,.024),'Stone',.003,1)
for y in [-1.9475,1.9475]:box('Limestone border',(0,y,.012),(3.79,.105,.024),'Stone',.003,1)

start('roof-flat');box('Roof deck',(0,0,.15),(4.1,4.1,.3),'Stone')
for x in [-1.97,1.97]:box('Parapet',(x,0,.4),(.16,4.1,.4),'Stone')
for y in [-1.97,1.97]:box('Parapet',(0,y,.4),(4.1,.16,.4),'Stone')
box('Roof surface',(0,0,.31),(3.76,3.76,.035),'Sage',.005)

start('roof-gabled')
angle=math.atan2(1.25,2.1)
for side in [-1,1]:
    o=box('Pitched roof',(side*1.05,0,.7),(math.sqrt(2.1**2+1.25**2),4.12,.14),'Navy',.018)
    o.rotation_euler.y=side*angle
    o=box('Oak roof soffit',(side*1.05,0,.647),(math.sqrt(2.1**2+1.25**2)-.03,4.08,.025),'Oak',.008,1)
    o.rotation_euler.y=side*angle
    for y in [-1.95,-1.3,-.65,0,.65,1.3,1.95]:
        a=(0,y,1.395);b=(side*2.15,y,.115);beam('Standing seam',a,b,.016,'Navy',8)
beam('Ridge',(0,-2.1,1.4),(0,2.1,1.4),.04,'Copper')
for y in [-2.02,2.02]:
    for side in [-1,1]:beam('Roof timber edge',(0,y,1.27),(side*2.1,y,.02),.065,'Oak')

start('balcony')
box('Balcony slab',(0,-.6,-.12),(4,1.2,.24),'Stone')
for x in [-1.94,-1.5,-1,-.5,0,.5,1,1.5,1.94]:
    beam('Front baluster',(x,-1.15,0),(x,-1.15,1.05),.025,'Navy',8)
beam('Oak handrail',(-1.96,-1.15,1.08),(1.96,-1.15,1.08),.045,'Oak')
for x in [-1.94,1.94]:beam('Side rail',(x,-1.15,1.08),(x,0,1.08),.035,'Navy')

start('staircase')
# Exactly 16 rises of .2 m = 3.2 m; .2 m goings occupy 3.2 m in a 4 m
# bay. Unity supplies the remaining .8 m landing. Do not add a second landing.
for i in range(16):
    y=-2+(i+.5)*.20;h=(i+1)*.2
    box('Step structural support',(0,y,h/2),(1.5,.20,h),'Stone',.008)
    box('Oak tread',(0,y,h+.01),(1.53,.22,.02),'Oak',.004)
for side in [-.72,.72]:
    for i in [0,3,6,9,12,15]:
        y=-2+(i+.5)*.20;h=(i+1)*.2
        beam('Stair rail post',(side,y,h),(side,y,h+.88),.022,'Navy',8)
    beam('Stair handrail',(side,-1.9,1.09),(side,1.1,4.12),.034,'Oak',12)

start('evidence-table')
cylinder('Table solid rim',(0,0,.79),1.3,.14,'Oak',48)
cylinder('Copper inset surround',(0,0,.865),1.12,.022,'Copper',48)
cylinder('Evidence display',(0,0,.881),1.065,.013,'Navy',48)
for angle in [30,150,270]:
    a=math.radians(angle);x,y=.85*math.cos(a),.85*math.sin(a)
    o=box('Table support',(x,y,.37),(.30,.54,.74),'Navy',.07);o.rotation_euler.z=a
    o=box('Foot',(x,y,.035),(.34,.58,.07),'Copper',.02);o.rotation_euler.z=a
nodes=[(-.55,-.30),(.3,-.5),(.65,.05),(.1,.52),(-.5,.42),(0,0)]
for i,(x,y) in enumerate(nodes):
    cylinder('Evidence node',(x,y,.897),.038,.007,'Ivory',12)
    if i<5:beam('Evidence link',(x,y,.892),(0,0,.892),.007,'Glass',6)

start('research-desk')
box('Desktop',(0,0,.79),(1.4,.7,.08),'Oak',.025)
box('Side support',(-.61,0,.38),(.10,.64,.76),'Oak',.015)
box('Drawer carcass',(.45,.02,.37),(.40,.60,.74),'Navy',.025)
for z in [.22,.48,.68]:
    box('Drawer front',(.45,-.292,z),(.36,.025,.19),'Navy',.008)
    box('Drawer handle',(.45,-.321,z+.035),(.18,.03,.018),'Copper',.005)
box('Display foot',(-.19,.07,.85),(.44,.23,.045),'Navy')
beam('Display stand',(-.19,.15,.85),(-.19,.15,1.05),.035,'Navy')
screen_tilt=math.radians(-18)
o=box('Display frame',(-.19,.12,1.14),(.8,.06,.43),'Oak',.02);o.rotation_euler.x=screen_tilt
o=box('Display screen',(-.19,.079,1.14),(.75,.015,.38),'Navy',.005);o.rotation_euler.x=screen_tilt
screen_rotation=Matrix.Rotation(screen_tilt,3,'X');screen_origin=Vector((-.19,.079,1.14))
for i,h in enumerate([.08,.14,.12,.24,.27]):
    u=-.29+i*.11
    vertices=[screen_origin+screen_rotation@Vector((x,-.009,z)) for x,z in [(u,-.145),(u+.055,-.145),(u+.055,-.145+h),(u,-.145+h)]]
    mesh_object('Screen chart bar',vertices,[(0,1,2,3)],'Glass')
cylinder('Desk lamp base',(-.59,.18,.854),.055,.035,'Copper',16)
beam('Desk lamp stem',(-.59,.18,.87),(-.59,.18,1.385),.012,'Copper',10)
box('Desk lamp bar',(-.43,.18,1.385),(.36,.044,.03),'Copper',.008,1)
box('Desk lamp diffuser',(-.43,.18,1.368),(.32,.03,.006),'WarmLight',0)

start('discussion-sofa')
arc_block('Continuous oak sofa plinth',.67,1.29,18,162,.06,.16,'Oak',18,.025)
for i in range(5):
    lo=19+i*28.4;hi=lo+27.7;angle=(lo+hi)/2;a=math.radians(angle)
    arc_block('Curved seat cushion',.68,1.23,lo,hi,.22,.24,'NavyFabric',5,.035)
    arc_block('Curved upholstered back',1.13,1.36,lo,hi,.38,.47,'NavyFabric',5,.045)
    if i in [0,2,4]:
        pillow=box('Ochre cushion',(1.06*math.cos(a),1.06*math.sin(a),.59),(.30,.15,.30),'Ochre',.06)
        pillow.rotation_euler.z=a-math.pi/2;pillow.rotation_euler.x=math.radians(-10)
for lo,hi in [(15,23),(157,165)]:arc_block('Upholstered sofa arm',.65,1.34,lo,hi,.24,.47,'NavyFabric',3,.035)

start('courtyard-tree')
cylinder('Stone planter',(0,0,.17),.8,.34,'Stone',32)
cylinder('Soil',(0,0,.347),.73,.015,'Soil',32)
beam('Trunk',(0,0,.35),(.06,.04,1.95),.12,'Oak',10)
tips=[(-.55,.05,2.25),(.45,.12,2.45),(.1,-.45,2.20),(-.18,.48,2.50)]
for i,t in enumerate(tips):
    beam('Tree branch',(.02,0,1.35+i*.08),t,.065,'Oak',8)
    ellipsoid('Canopy mass',t,(.60,.55,.54),'Leaf' if i%2 else 'LeafLight',12,6)
    for j in range(3):
        a=(i*3+j)*2.4
        ellipsoid('Leaf cluster',(t[0]+.34*math.cos(a),t[1]+.30*math.sin(a),t[2]+.18),(.30,.27,.27),'LeafLight' if j%2 else 'Leaf',10,5)
ellipsoid('Top canopy',(.02,.02,2.65),(.60,.58,.43),'Sage',12,6)
for i in range(8):
    a=i*math.tau/8;ellipsoid('Planter foliage',(.53*math.cos(a),.53*math.sin(a),.48),(.24,.22,.22),'Leaf',10,5)

start('student-guide')
# Separate rigid body pieces permit inexpensive authored idle/walk/build animation
# at runtime; names provide a stable contract for Unity animation pivots.
for side in [-1,1]:
    label='Left' if side<0 else 'Right';x=side*.13
    guide_piece=label+'Foot'
    box('Sneaker sole',(x,-.070,.025),(.205,.34,.05),'Ivory',.022,3)
    box('Sneaker upper',(x,-.056,.088),(.195,.31,.126),'Ivory',.052,3)
    for i in range(3):beam('Shoe lace',(x-.057,-.112+i*.035,.146),(x+.057,-.112+i*.035,.146),.004,'Stone',6)
    box('Heel tab',(x,.092,.103),(.08,.02,.07),'NavyFabric',.008,1)
    guide_piece=label+'Leg'
    box(label+'Leg',(x,0,.50),(.205,.23,.76),'ClothTan',.055,3)
    box('Trouser cuff',(x,0,.15),(.213,.238,.075),'ClothTan',.022,2)
guide_piece='GuideBody'
box('Torso',(0,0,1.13),(.48,.27,.60),'NavyFabric',.085,3)
box('Shirt',(0,-.141,1.17),(.18,.015,.48),'Ivory',.025)
for side in [-1,1]:
    collar=box('Open jacket collar',(side*.105,-.158,1.385),(.115,.03,.15),'NavyFabric',.018)
    collar.rotation_euler.y=side*math.radians(20)
    box('Jacket pocket',(side*.165,-.145,1.03),(.108,.023,.16),'NavyFabric',.014)
    stroke('Pocket seam',(side*.165-.045,-.160,1.08),(side*.165+.045,-.160,1.08),.005,'Copper')
    for z in [.98,1.14,1.28]:disc('Jacket button',(side*.103,-.166,z),.008,'Copper',8)
box('Belt',(0,-.008,.868),(.39,.235,.05),'OakDark',.012)
box('Belt buckle',(0,-.130,.868),(.035,.018,.036),'Copper',.005,1)
box('Compact backpack',(0,.139,1.19),(.34,.075,.43),'Ink',.035,3)
for side in [-1,1]:
    beam('Backpack shoulder strap',(side*.185,-.135,1.43),(side*.185,-.159,1.18),.018,'Ink',8)
cylinder('Neck',(0,0,1.45),.075,.13,'Skin',16)
ellipsoid('Head',(0,-.01,1.60),(.175,.16,.205),'Skin',20,12)
ellipsoid('Hair cap',(0,.026,1.72),(.178,.146,.106),'Ink',16,8)
for i in range(4):
    lock=ellipsoid('Swept hair',(-.11+i*.068,-.060,1.760+math.sin(i*.9)*.025),(.082,.115,.055),'Ink',12,6)
    lock.rotation_euler.y=-.3+i*.08;lock.rotation_euler.z=-.25
for side in [-1,1]:
    ellipsoid('Ear',(side*.171,0,1.60),(.031,.033,.060),'Skin',12,6)
    ellipsoid('Eye',(side*.061,-.156,1.629),(.027,.017,.033),'Ivory',12,6)
    ellipsoid('Pupil',(side*.061,-.172,1.627),(.014,.009,.022),'Ink',12,6)
    ellipsoid('Eye catchlight',(side*.061-.004,-.180,1.637),(.004,.003,.005),'Ivory',8,4)
    beam('Brow',(side*.035,-.163,1.680),(side*.083,-.150,1.689),.009,'Ink',6)
ellipsoid('Nose',(0,-.171,1.588),(.026,.034,.035),'Skin',12,6)
tube('Gentle smile',[(-.044,-.153,1.548),(-.023,-.166,1.541),(0,-.170,1.539),(.023,-.166,1.541),(.044,-.153,1.548)],.005,'Copper',6)
for side in [-1,1]:
    label='Left' if side<0 else 'Right'
    shoulder=(side*.27,0,1.37);elbow=(side*.38,-.01,1.13);hand=(side*.48,-.02,.92)
    guide_piece=label+'UpperArm'
    beam(label+'UpperArm',shoulder,elbow,.075,'NavyFabric',12)
    ellipsoid('Shoulder sleeve',shoulder,(.076,.075,.085),'NavyFabric',12,6)
    beam('Rolled sleeve cuff',(side*.365,-.009,1.175),elbow,.083,'NavyFabric',12)
    guide_piece=label+'Forearm'
    beam(label+'Forearm',elbow,hand,.055,'Skin',12)
    guide_piece=label+'Hand'
    ellipsoid(label+'Hand',hand,(.051,.037,.065),'Skin',12,6)
    ellipsoid('Thumb',(hand[0]-side*.039,-.054,.934),(.020,.025,.046),'Skin',10,5)
    for i in range(3):ellipsoid('Finger',(hand[0]+(i-1)*.022,-.026,.862+abs(i-1)*.005),(.012,.026,.032),'Skin',8,4)
guide_piece='GuideBody'

start('bookshelf')
for x in [-.57,.57]:box('Oak upright',(x,0,.9),(.06,.32,1.8),'Oak',.02)
box('Shelf back',(0,.135,.95),(1.08,.035,1.7),'Oak',.01)
for z in [.12,.65,1.18,1.75]:box('Oak shelf',(0,0,z),(1.12,.32,.055),'Oak',.012)
for row,z in enumerate([.15,.68,1.21]):
    for i in range(9):
        if row==2 and i<7:continue
        x=-.47+i*.109; h=.24+((i*7+row*3)%5)*.035
        book=box('Research book',(x,-.025,z+h/2),(.085,.22,h),['Navy','Ivory','Copper','Sage'][i%4],.006,1)
        stroke('Book spine rule',(x-.028,-.137,z+h*.72),(x+.028,-.137,z+h*.72),.012,'Ochre')
cylinder('Shelf plant pot',(-.34,0,1.28),.085,.13,'Ivory',16)
for i in range(7):
    a=i*2.4
    leaf=ellipsoid('Trailing shelf plant',(-.34+.07*math.cos(a),-.04+.075*math.sin(a),1.43-.038*i),(.05,.035,.092),'Leaf' if i%2 else 'LeafLight',8,4)
    leaf.rotation_euler.y=math.sin(a)*.6
cylinder('Study sculpture base',(.06,0,1.238),.08,.046,'Stone',16)
cylinder('Study sculpture neck',(.06,0,1.34),.04,.17,'Stone',12)
ellipsoid('Study sculpture head',(.06,-.015,1.47),(.072,.06,.105),'Stone',12,8)
ellipsoid('Study sculpture profile',(.06,-.075,1.458),(.014,.022,.02),'Stone',8,4)

start('lamp')
cylinder('Stable lamp base',(-.10,0,.04),.275,.08,'Navy',32)
end_angle=.85;arc_radius=.32/(1+math.cos(end_angle));arc_centre=arc_radius-.10
points=[(-.10,0,.06),(-.10,0,.75),(-.10,0,1.14)]
points.extend([(arc_centre+arc_radius*math.cos(a),0,1.14+.54*math.sin(a))
               for a in [math.pi-i*(math.pi-end_angle)/20 for i in range(1,21)]])
tube('Continuous brass arch',points,.018,'Copper',10)
shade_top=points[-1][2]-.015;shade_base=shade_top-.21
beam('Shade suspension',points[-1],(.22,0,shade_top),.015,'Copper',10)
dome('Open ivory dome',(.22,0,shade_base),.255,.21,'Ivory',24,6)
cylinder('Warm diffuser',(.22,0,shade_base+.007),.245,.012,'WarmLight',24,0)

start('noticeboard')
for x in [-.59,.59]:
    box('Board foot',(x,0,.045),(.12,.5,.09),'Navy',.035)
    beam('Board leg',(x,0,.05),(x,0,1.48),.027,'Navy',12)
box('Board frame',(0,0,1.0),(1.3,.075,1.0),'Navy',.045)
box('Pin surface',(0,-.043,1.0),(1.20,.022,.9),'NavyFabric',.025)
for i in range(9):
    x=(i%3-1)*.35;z=.71+(i//3)*.29
    card=box('Evidence card',(x,-.064,z),(.26,.012,.22),'Ivory',.003,1)
    disc('Copper pin',(x,-.078,z+.085),.011,'Copper',10)
    if i%3==0:
        for j in range(3):stroke('Evidence bars',(x-.07+j*.07,-.073,z-.06),(x-.07+j*.07,-.073,z-.02+j*.035),.024,'Navy')
    elif i%3==1:
        for j in [-1,0,1]:
            disc('Network node',(x+j*.07,-.073,z+.02*(1-abs(j))),.015,'Sage',10)
            stroke('Network edge',(x+j*.07,-.072,z+.02*(1-abs(j))),(x,-.072,z-.065),.004,'Navy')
    else:
        stroke('Trend',(x-.085,-.073,z-.06),(x,-.073,z-.005),.009,'Copper')
        stroke('Trend',(x,-.073,z-.005),(x+.085,-.073,z+.055),.009,'Copper')

start('chair')
for x in [-.22,.22]:
    for y in [-.23,.23]:beam('Oak chair leg',(x,y,.015),(x*.86,y*.86,.45),.025,'Oak',10)
box('Curved upholstered seat',(0,0,.44),(.55,.56,.12),'SageFabric',.07,3)
back=box('Upholstered back',(0,.235,.68),(.52,.12,.34),'SageFabric',.09,3);back.rotation_euler.x=math.radians(-6)
for x in [-.245,.245]:
    beam('Arm support',(x,.19,.45),(x,.19,.64),.023,'Oak',10)
    beam('Oak armrest',(x,-.18,.62),(x,.20,.65),.028,'Oak',12)

start('bench')
for x in [-.63,.63]:
    for y in [-.20,.20]:beam('Bench leg',(x,y,.02),(x*.9,y*.85,.43),.034,'Navy',10)
for y in [-.20,-.065,.07,.205]:box('Oak bench seat',(0,y,.45),(1.5,.125,.065),'Oak',.027)
for x in [-.62,.62]:beam('Back support',(x,.20,.40),(x,.25,.75),.032,'Navy',10)
box('Oak bench back',(0,.25,.65),(1.5,.065,.26),'Oak',.07)

start('acoustic-divider')
for x in [-.44,.44]:
    box('Divider feet',(x,0,.035),(.11,.45,.07),'Oak',.025)
    box('Divider post',(x,0,.85),(.06,.09,1.5),'Oak',.022)
box('Felt screen',(0,0,.90),(.83,.065,1.38),'SageFabric',.035)
for z in [.19,1.61]:box('Frame edge',(0,0,z),(1.0,.095,.055),'Oak',.02)

start('achievement-display')
for i,(x,y,h) in enumerate([(-.22,-.05,.22),(.15,.07,.38),(.30,-.13,.15)]):
    cylinder('Display plinth',(x,y,h/2),.20,h,'Stone',32)
    if i<2:
        bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,location=(x,y,h+.12),major_radius=.105,minor_radius=.013)
        o=add(bpy.context.object,'Inquiry orbit','Copper');o.rotation_euler.x=math.pi/2
        ellipsoid('Evidence sphere',(x,y,h+.08),(.035,.035,.035),'Copper',12,6)
    else:
        for j in [-1,0,1]:
            leaf=ellipsoid('Growing responsibility',(x+j*.05,y,h+.13+j*.015),(.035,.025,.12),'Copper',12,6);leaf.rotation_euler.y=j*.35

start('research-poster')
for x in [-.3375,.3375]:box('Poster side frame',(x,0,.45),(.025,.06,.9),'Oak',.006,1)
for z in [.0125,.8875]:box('Poster horizontal frame',(0,0,z),(.65,.06,.025),'Oak',.006,1)
box('Poster backing',(0,.015,.45),(.65,.03,.85),'OakDark',0)
box('Poster paper',(0,-.002,.45),(.65,.004,.85),'Ivory',0)
for i in range(10):
    a=i*math.tau/10;x=.21*math.cos(a);z=.64+.19*math.sin(a)
    disc('Mind map node',(x,-.0055,z),.027,'Sage' if i%2 else 'Copper',12)
    stroke('Research link',(x,-.005,z),(0,-.005,.64),.004,'Navy')
disc('Central research idea',(0,-.0055,.64),.05,'Stone',16)
for i,h in enumerate([.05,.08,.11,.14,.20,.24]):stroke('Data bar',(-.23+i*.08,-.0055,.12),(-.23+i*.08,-.0055,.12+h),.035,'Navy')
stroke('Plot baseline',(-.26,-.0055,.115),(.25,-.0055,.115),.004,'Stone')

def bounds(objects):
    bpy.context.view_layer.update()
    points=[obj.matrix_world@vertex.co for obj in objects for vertex in obj.data.vertices]
    return ([min(p[k] for p in points) for k in range(3)], [max(p[k] for p in points) for k in range(3)])

def join_group(objects,name,pivot=(0,0,0)):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name
    bpy.context.scene.cursor.location=pivot;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Mesh material indices remain intact; one mesh renderer can have several
    # submeshes. Report that cost instead of claiming one draw call per prop.
    obj.data.update()
    return obj

# Resolve full furniture envelopes explicitly before measuring; architectural
# geometry and guide joint anchors are never globally rescaled. Retain the exact
# applied sizing transform in the audit, rather than disguising it as a measurement.
sizing={}
for name,objects in assets.items():
    structural=name.startswith(('wall','floor','roof','stair','balcony'))
    if structural or name=='student-guide':continue
    lo,hi=bounds(objects);actual=[hi[k]-lo[k] for k in range(3)];target=PROP[name]['dimensionsMeters']
    scales=[target[k]/actual[k] for k in range(3)];centre=[(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]]
    for obj in objects:
        transform=obj.matrix_world.copy()
        for vertex in obj.data.vertices:
            point=transform@vertex.co
            vertex.co=Vector([(point[k]-centre[k])*scales[k] for k in range(3)])
        obj.matrix_world=Matrix.Identity(4);obj.data.update()
    sizing[name]={'method':'authored-full-envelope-fit','unfittedDimensions':actual,'appliedScale':scales,'targetDimensions':target}

# Runtime GuideMotion expects these exact eleven mesh names. Details are joined
# into their moving limb rather than becoming dozens of motionless renderers.
GUIDE_PIVOTS={'GuideBody':(0,0,0),
 'LeftUpperArm':(-.27,0,1.37),'RightUpperArm':(.27,0,1.37),
 'LeftForearm':(-.38,-.01,1.13),'RightForearm':(.38,-.01,1.13),
 'LeftHand':(-.48,-.02,.92),'RightHand':(.48,-.02,.92),
 'LeftLeg':(-.13,0,.87),'RightLeg':(.13,0,.87),
 'LeftFoot':(-.13,0,.15),'RightFoot':(.13,0,.15)}
guide_objects=assets['student-guide'];guide_groups={}
for obj in guide_objects:guide_groups.setdefault(obj.get('motion_group','GuideBody'),[]).append(obj)
assets['student-guide']=[join_group(group,name,GUIDE_PIVOTS[name]) for name,group in guide_groups.items()]

records=[]; opening_records=[];preflight_errors=[]
bpy.context.view_layer.update()
for asset_id,objects in list(assets.items()):
    if asset_id!='student-guide':
        assets[asset_id]=[join_group(objects,asset_id)]
    bpy.context.view_layer.update()
    for obj in assets[asset_id]:obj.data.calc_loop_triangles()
    tris=sum(len(obj.data.loop_triangles) for obj in assets[asset_id])
    lo,hi=bounds(assets[asset_id]);dimensions=[hi[k]-lo[k] for k in range(3)]
    structural=asset_id.startswith(('wall','floor','roof','stair','balcony'))
    furniture=asset_id in ['bookshelf','lamp','noticeboard','chair','bench','acoustic-divider','achievement-display','research-poster']
    reference='../references/'+('architecture-kit.png' if structural else 'furniture-references.png' if furniture else 'asset-references.png')
    target=PROP[asset_id]['dimensionsMeters'];budget=PROP[asset_id]['targetPolycount']
    if tris>budget:preflight_errors.append(f'{asset_id}: {tris} triangles exceed budget {budget}')
    if any(abs(dimensions[k]-target[k])>max(.02,target[k]*.05) for k in range(3)):
        preflight_errors.append(f'{asset_id}: measured envelope {dimensions} differs from planned {target}')
    records.append({'id':asset_id,'triangles':tris,'triangleBudget':budget,'min':lo,'max':hi,'dimensions':dimensions,
        'sourceReference':reference,'sourceReferenceSha256':hashlib.sha256((OUT/reference).read_bytes()).hexdigest(),
        'pivot':list(assets[asset_id][0].location) if asset_id!='student-guide' else [0,0,0],
        'pivotMode':'named-part-pivots' if asset_id=='student-guide' else 'grid-contact-plane' if structural else 'base-center',
        'objects':[{'name':o.name,'origin':list(o.location),'materialSlots':len(o.material_slots)} for o in assets[asset_id]],
        'rendererCount':len(assets[asset_id]),'materialSlotCount':sum(len(o.material_slots) for o in assets[asset_id]),
        'sourceCell':PROP[asset_id]['image'].get('sheetCell',PROP[asset_id].get('cell')),
        'sizing':sizing.get(asset_id,{'method':'authored-grid-or-joint-contract-no-envelope-rescale'})})
    if asset_id in ['wall-door','wall-partition']:
        obj=assets[asset_id][0];mesh=obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
        tree=BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons],all_triangles=False)
        samples=[]
        for ix in range(25):
            for iz in range(28):
                x=-1.195+2.39*ix/24;z=.005+2.69*iz/27
                hit=tree.ray_cast(Vector((x,-.6,z)),Vector((0,1,0)),1.2)[0]
                samples.append({'x':round(x,4),'z':round(z,4),'clear':hit is None})
        clear=all(s['clear'] for s in samples)
        # Find actual aperture boundaries with a binary ray search, in addition
        # to checking a grid inside the requested clear rectangle.
        def clear_at(x,z):return tree.ray_cast(Vector((x,-.6,z)),Vector((0,1,0)),1.2)[0] is None
        def boundary(clear_limit,blocked_limit,position):
            for _ in range(24):
                mid=(clear_limit+blocked_limit)/2
                if clear_at(*position(mid)):clear_limit=mid
                else:blocked_limit=mid
            return clear_limit
        widths=[boundary(0,1.4,lambda n:(n,z))+boundary(0,1.4,lambda n:(-n,z)) for z in [.3,.7,1.35,2.3,2.6]]
        heights=[boundary(1.35,3.15,lambda n:(x,n)) for x in [-.9,-.45,0,.45,.9]]
        measured_width=min(widths);measured_height=min(heights)
        evidence=asset_id+'-clearance.json'
        (OUT/evidence).write_text(json.dumps({'method':'Evaluated-mesh BVH boundary search plus inset aperture ray grid',
            'axis':'Blender +Y','nominalWidthMetres':2.4,'nominalHeightMetres':2.7,'floorDatumMetres':0,
            'measuredWidths':widths,'measuredHeights':heights,'sampleCount':len(samples),'samples':samples},indent=2),encoding='utf-8')
        opening_records.append({'assetId':asset_id,'clearWidthMetres':measured_width,'clearHeightMetres':measured_height,'depthMetres':hi[1]-lo[1],
            'unobstructed':clear,'measurementMethod':'evaluated-mesh','evidence':evidence,
            'evidenceSha256':hashlib.sha256((OUT/evidence).read_bytes()).hexdigest()})
        obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
        if not clear or measured_width<2.399 or measured_height<2.699:preflight_errors.append(asset_id+' fails measured door clearance')

if set(assets)!=set(PROP):preflight_errors.append('Manifest and generated asset IDs differ')
(OUT/'geometry-preflight.json').write_text(json.dumps({'measuredAt':datetime.now(timezone.utc).isoformat(),
    'assets':records,'openings':opening_records,'errors':preflight_errors,
    'status':'blocked' if preflight_errors else 'geometry-checks-passed-not-form-approved'},indent=2),encoding='utf-8')
if preflight_errors:raise RuntimeError('Geometry preflight failed: '+'; '.join(preflight_errors))

material_records=[]
for name,(colour,roughness,metallic) in PALETTE.items():
    material_records.append({'name':'GameMat_'+name,'baseColorSrgb':colour,'roughness':roughness,'metallic':metallic,
        'opacity':.32 if name=='Glass' else 1.0,'emissionColorSrgb':colour if name=='WarmLight' else '#000000',
        'emissionStrength':.7 if name=='WarmLight' else 0.0,'sheenWeight':.18 if 'Fabric' in name or name=='ClothTan' else 0.0})
(OUT/'material-manifest.json').write_text(json.dumps({'schema':'kompetenzhaus.materials.v1','materials':material_records,'textures':[],
    'note':'Authored portable PBR constants. No baked texture maps are claimed. Read these values when replacing FBX materials in Unity.'},indent=2),encoding='utf-8')

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1800;scene.render.resolution_y=1800;scene.render.resolution_percentage=100
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.72,.76,.80,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.55
scene.view_settings.view_transform='AgX'
bpy.ops.mesh.primitive_plane_add(size=180,location=(0,0,-.018));ground=bpy.context.object;ground.name='Temporary review backdrop'
ground.data.materials.append(M['Ivory'])
bpy.ops.object.light_add(type='AREA',location=(-5,-8,16));key=bpy.context.object;key.data.energy=3400;key.data.shape='DISK';key.data.size=10
bpy.ops.object.light_add(type='AREA',location=(10,7,13));fill=bpy.context.object;fill.data.energy=2200;fill.data.size=9
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';scene.camera=cam
review_images=[]
def render_group(ids,filename,columns=4,spacing=5.8):
    if not ids:raise RuntimeError('No manifest assets selected for review image '+filename)
    rows=math.ceil(len(ids)/columns);offsets={}
    for name,objects in assets.items():
        for obj in objects:obj.hide_render=name not in ids
    for index,name in enumerate(ids):
        lo,_=bounds(assets[name]);offset=Vector(((index%columns-(columns-1)/2)*spacing,(index//columns-(rows-1)/2)*spacing,-lo[2]))
        offsets[name]=offset
        for obj in assets[name]:obj.location+=offset
    span=max(columns*spacing,rows*spacing)
    cam.location=(span*.66,-span*1.08,span*.83)
    cam.rotation_euler=(Vector((0,0,.7))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=span*1.25
    scene.render.filepath=str(REVIEW/filename)
    try:bpy.ops.render.render(write_still=True)
    finally:
        for name,offset in offsets.items():
            for obj in assets[name]:obj.location-=offset
        for objects in assets.values():
            for obj in objects:obj.hide_render=False
    review_images.append({'path':'../review/'+filename,'sha256':hashlib.sha256((REVIEW/filename).read_bytes()).hexdigest(),
                          'assetIds':ids,'status':'rendered-awaiting-form-review'})

render_group(list(assets),'model-contact-sheet.png',4)
render_group([name for name in assets if PROP[name].get('family')=='architecture'],'architecture-models.png',3)
render_group([name for name in assets if PROP[name].get('family')=='learning-prop'],'learning-models.png',3,4.6)
render_group([name for name in assets if PROP[name].get('family')=='furniture'],'furniture-models.png',4,3.0)
render_group(['student-guide'],'student-guide-model.png',1,2.2)
for obj in [ground,key,fill,cam]:bpy.data.objects.remove(obj,do_unlink=True)
scene.camera=None;scene.cursor.location=(0,0,0)
for name,objects in assets.items():
    # A named collection per exportable authored model.
    coll=bpy.data.collections.new(name);scene.collection.children.link(coll)
    for o in objects:
        for old in list(o.users_collection):old.objects.unlink(o)
        coll.objects.link(o)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'kompetenzhaus-assets.blend'))
(OUT/'asset-audit.json').write_text(json.dumps({'generator':'Own local Blender geometry from approved references','version':2,
    'generatedAt':datetime.now(timezone.utc).isoformat(),'blenderVersion':bpy.app.version_string,
    'generatorScriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    'propsManifestSha256':hashlib.sha256((REVIEW/'props.json').read_bytes()).hexdigest(),
    'conventions':{'units':'meters','gridSizeMetres':4,'floorHeightMetres':3.2,'blenderUp':'Z','unityUp':'Y'},
    'sourceBlend':{'path':'kompetenzhaus-assets.blend','sha256':hashlib.sha256((OUT/'kompetenzhaus-assets.blend').read_bytes()).hexdigest()},
    'materialManifest':{'path':'material-manifest.json','sha256':hashlib.sha256((OUT/'material-manifest.json').read_bytes()).hexdigest()},
    'stairContract':{'riseMetres':3.2,'runMetres':3.2,'riserCount':16,'riserHeightMetres':.2,'goingMetres':.2,
                     'externalUnityLandingMetres':.8,'ascentAxis':'Blender +Y; measured and normalized to Unity +Z in WorldSetup'},
    'guideMotion':{'kind':'rigid-named-parts-not-skinned','jointPivotsBlender':GUIDE_PIVOTS},
    'reviewImages':review_images,'approvalStatus':'pending-form-review','assets':records,'openings':opening_records,
    'exports':[],'totalUniqueTriangles':sum(r['triangles'] for r in records),'exported':False},indent=2),encoding='utf-8')
print('MODEL_REVIEW_READY',len(records),'assets',sum(r['triangles'] for r in records),'unique triangles')
