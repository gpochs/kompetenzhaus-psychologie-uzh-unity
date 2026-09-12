"""Create measured Function-stage review geometry. Not production asset export.
Run: blender --background --python art/blender/build_review_plan.py -- <repo>
"""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
OUT = ROOT / 'art' / 'review'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, shade):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (shade, shade, shade, 1)
    return m
stone, floor, ink, pale = [material(n,c) for n,c in [('Walls',.30),('Floor',.87),('Ink',.06),('Circulation',.97)]]

def box(name, center, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    o=bpy.context.object; o.name=name
    o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    return o

def label(text, x, y, size=.40):
    bpy.ops.object.text_add(location=(x,y,.025))
    o=bpy.context.object; o.name='Label_'+text
    o.data.body=text; o.data.align_x='CENTER'; o.data.size=size
    o.data.materials.append(ink)
    return o

surfaces=[]; openings=[]
def wall(name, center, size, doors=()):
    o=box(name,center,size,stone)
    surfaces.append({'id':name,'type':'wall','center':list(center),'dimensions':list(size)})
    for ident,pos,dim,purpose,destination in doors:
        # Match the interchangeable production portal: 2.4 m wide, 2.7 m high.
        pos=tuple(pos[:2])+(1.35,); dim=tuple(dim[:2])+(2.7,)
        cut=box('CUT_'+ident,pos,dim,pale)
        mod=o.modifiers.new('Opening_'+ident,'BOOLEAN'); mod.operation='DIFFERENCE'; mod.object=cut
        bpy.context.view_layer.objects.active=o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cut,do_unlink=True)
        openings.append({'id':ident,'wallId':name,'type':'door','center':list(pos),'dimensions':list(dim),'purpose':purpose,'destination':destination,'state':'true-through-opening'})
    return o

box('Ground slab',(0,1,-.18),(28,24,.32),floor)
box('Courtyard walking surface',(0,0,.0),(13.6,13.6,.025),pale)
H=3.2; T=.24
wall('Bachelor outer',(-13.8,2,H/2),(T,16,H))
wall('Master outer',(13.8,2,H/2),(T,16,H))
wall('Bachelor rear',(-10.4,10,H/2),(6.8,T,H))
wall('Master rear',(10.4,10,H/2),(6.8,T,H))
wall('Workshop rear',(0,12.8,H/2),(8,T,H))
wall('Workshop west',(-4,9.4,H/2),(T,6.8,H))
wall('Workshop east',(4,9.4,H/2),(T,6.8,H))
wall('Main arrival south',(0,-10.8,.45),(28,T,.9),[
    ('arrival',(0,-10.8,1.3),(2.4,.6,2.6),'Level accessible arrival','Courtyard')])
wall('West inner',(-7,2,H/2),(T,16,H),[
    ('west-research',(-7,-3,1.3),(.6,2.4,2.6),'Research room access','Methods studio'),
    ('west-social',(-7,5,1.3),(.6,2.4,2.6),'Conversation room access','Discussion studio')])
wall('East inner',(7,2,H/2),(T,16,H),[
    ('east-cognition',(7,-3,1.3),(.6,2.4,2.6),'Experiment room access','Cognition studio'),
    ('east-library',(7,5,1.3),(.6,2.4,2.6),'Evidence archive access','Library')])
wall('North workshop front',(0,6,H/2),(8,T,H),[
    ('ai-workshop',(0,6,1.3),(2.4,.6,2.6),'Hero workshop entry','AI evidence workshop')])
wall('West partition',(-10.4,1,H/2),(6.8,T,H),[
    ('west-passage',(-10.4,1,1.3),(2.4,.6,2.6),'Connected studio circulation','West studios')])
wall('East partition',(10.4,1,H/2),(6.8,T,H),[
    ('east-passage',(10.4,1,1.3),(2.4,.6,2.6),'Connected studio circulation','East studios')])
wall('West front',(-10.4,-6,H/2),(6.8,T,H),[
    ('west-front',(-10.4,-6,1.3),(2.4,.6,2.6),'Secondary arrival','Methods studio')])
wall('East front',(10.4,-6,H/2),(6.8,T,H),[
    ('east-front',(10.4,-6,1.3),(2.4,.6,2.6),'Secondary arrival','Cognition studio')])

# Full-size furniture footprint geometry supports actual clearance review.
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=1.3,depth=.10,location=(0,9.4,.15))
bpy.context.object.name='Evidence table footprint diameter 2.6m'; bpy.context.object.data.materials.append(stone)
for x,y in [(-11.5,-2.8),(-9,-2.8),(11.5,-2.8),(9,-2.8)]:
    box('Workstation footprint',(x,y,.06),(1.4,.65,.10),stone)
for x in [-11.8,11.8]:
    box('Bookshelf footprint',(x,6,.06),(.6,3,.10),stone)
box('Discussion sofa footprint',(-11,3.8,.06),(2.2,.9,.10),stone)
for x,y in [(-3.5,2.5),(3.5,2.5)]:
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.8,depth=.10,location=(x,y,.06))
    bpy.context.object.name='Tree planter footprint'; bpy.context.object.data.materials.append(stone)

# Render plan at low clipping height. Label objects sit above the floor only.
for text,x,y,size in [
    ('KOMPETENZHAUS / STARTVORLAGE',0,15,.70),
    ('28 m',0,-13,.45),('24 m',-16,1,.45),
    ('BACHELOR\nGrundlagen / Methoden',-10.4,-4.8,.34),('Anwendung\nExperimente / Team',-10.4,7.1,.34),
    ('MASTER\nVertiefung / Forschung',10.4,-4.8,.34),('Transfer\nVerantwortung',10.4,7.1,.34),
    ('KI-WERKSTATT',0,11.9,.4),('INNENHOF',0,4.7,.5),
    ('Freie Wege: mind. 1.8 m',0,-1,.38),('Eingang 2.4 m / ebenerdig',0,-9.4,.38),
    ('Vogelperspektive: Module bauen  |  Ich-Perspektive: erkunden',0,-14.8,.40),
    ('Optionale Startvorlage. Eigene Grundrisse bleiben frei gestaltbar.',0,-15.6,.34)]: label(text,x,y,size)

scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='FLAT'; scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=False; scene.display.shading.show_cavity=True
scene.display.shading.background_type='WORLD'; scene.world.color=(1,1,1)
scene.render.resolution_x=1400; scene.render.resolution_y=1400; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
bpy.ops.object.camera_add(location=(0,0,50))
cam=bpy.context.object; cam.name='MeasuredPlanCamera'; cam.data.type='ORTHO'; cam.data.ortho_scale=35
cam.rotation_euler=(0,0,0); scene.camera=cam
# Viewing down camera local -Z: zero rotation.
cam.data.clip_start=48.8; cam.data.clip_end=51
scene.render.filepath=str(OUT/'floor-plan.png'); bpy.ops.render.render(write_still=True)

# Reflected ceiling schedule: ground-floor roof/ceiling solids and an open courtyard.
# Separate visualization; not exported production geometry.
roofmat=material('Ceiling panels',.68)
roof_specs=[('West ceiling',(-10.4,2,3.35),(6.8,16,.30)),
            ('East ceiling',(10.4,2,3.35),(6.8,16,.30)),
            ('Workshop ceiling',(0,9.4,3.35),(8,6.8,.30))]
for name,pos,dim in roof_specs:
    box(name,pos,dim,roofmat)
    surfaces.append({'id':name,'type':'ceiling','center':list(pos),'dimensions':list(dim)})
for o in bpy.data.objects:
    if o.type=='FONT':
        o.location.z=3.65
        if o.data.body=='KOMPETENZHAUS / STARTVORLAGE':o.data.body='STARTVORLAGE / DECKENPLAN'
        if o.data.body=='INNENHOF':o.data.body='OFFEN ZUM HIMMEL'
        if o.data.body.startswith('Optionale Startvorlage'):o.data.body='Deckenhoehe 3.2 m | abnehmbare Daecher fuer Bauen / freie Sicht'
cam.data.clip_start=.1
scene.render.filepath=str(OUT/'ceiling-plan.png'); bpy.ops.render.render(write_still=True)

metadata={'schema':'kompetenzhaus.plan.v1','units':'meters','stage':'Function review / not final composition','bounds':[28,24,3.2],
 'role':'optional-starter','fixedRuntimeLayout':False,'openingClearHeightMetres':2.7,
 'roomId':'kompetenzhaus-campus','productionView':'bird view + first person','minimumClearanceMeters':1.8,'mainArrivalWidthMeters':2.4,
 'floorPlan':'floor-plan.png','reflectedCeilingPlan':'ceiling-plan.png','geometrySource':'../blender/build_review_plan.py','planCutHeightMeters':1.2,'paidServiceBudget':0}
(OUT/'plan-metadata.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'room-layout.json').write_text(json.dumps({'schema':'game-room.layout.v1','roomId':'kompetenzhaus-campus','units':'meters','surfaces':surfaces,'instances':[],
 'productionCamera':{'location':[28,-34,30],'target':[0,1,0],'verticalFovDegrees':45}},indent=2),encoding='utf-8')
(OUT/'openings.json').write_text(json.dumps({'schema':'game-room.opening-schedule.v1','roomId':'kompetenzhaus-campus','primaryArrival':{'openingId':'arrival','exception':''},'openings':openings},indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'function-layout.blend'))
print('FUNCTION_REVIEW_CREATED',str(OUT))
