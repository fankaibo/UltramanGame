"""Blender-only read-only comparison of Tiga's source and shipped FBX geometry.

Run with --background --factory-startup --disable-autoexec --python THIS --
rest|subdivision. Outputs stay under artifacts; original models are never saved.
Subdivison images are source diagnostic renders, not game screenshots.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'scripts'))
import prepare_tiga

def compare_rest(source, fbx_path, out):
    rig=prepare_tiga.prepare(source,out/'source-reproduction')
    actions=prepare_tiga.animate(rig,live_combat=True)
    rig.animation_data.action=actions['Idle']
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    mesh=bpy.data.objects['TigaBody']
    keys=mesh.data.shape_keys
    report={'original_fbx_sha256':hashlib.sha256((fbx_path).read_bytes()).hexdigest(),'idle_shapes':{k.name:k.value for k in keys.key_blocks}}
    keys.animation_data_clear()
    rig.data.pose_position='REST'
    arrays={}
    for name,values in [('idle',list(report['idle_shapes'].values())[1:]),('zero',[0,0]),('one',[1,1])]:
     for k,value in zip(list(keys.key_blocks)[1:],values):k.value=value
     bpy.context.view_layer.update()
     evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());m=evaluated.to_mesh()
     arrays[name]=np.array([list(mesh.matrix_world@v.co) for v in m.vertices])
     evaluated.to_mesh_clear()
    report['source_vertices']=len(arrays['idle'])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path),use_anim=False,use_image_search=False)
    obj=bpy.data.objects['TigaBody'];fbx=np.array([list(obj.matrix_world@v.co) for v in obj.data.vertices])
    report['fbx_vertices']=len(fbx)
    report['fbx_bounds']=[fbx.min(axis=0).tolist(),fbx.max(axis=0).tolist()]
    report['comparisons']={}
    for name,a in arrays.items():
     tree=KDTree(len(a))
     for i,v in enumerate(a):tree.insert(v,i)
     tree.balance()
     distances=np.array([tree.find(v)[2] for v in fbx])
     report['comparisons'][name]={'bounds':[a.min(axis=0).tolist(),a.max(axis=0).tolist()], 'nearest_max':float(distances.max()),'nearest_mean':float(distances.mean()),'over_1e-5':int((distances>1e-5).sum())}
    np.savez_compressed(out/'rest-meshes.npz',fbx=fbx,**arrays)
    (out/'rest-mesh-comparison.json').write_text(json.dumps(report,indent=2))
    print('REST_COMPARE',json.dumps(report))


def compare_subdivision(source, out):
    rig=prepare_tiga.prepare(source,out)
    actions=prepare_tiga.animate(rig,live_combat=True)
    rig.animation_data.action=actions['Idle'];scene=bpy.context.scene;scene.frame_set(1)
    mesh=bpy.data.objects['TigaBody'];mesh.data.shape_keys.animation_data_clear()
    for k in mesh.data.shape_keys.key_blocks:k.value=0
    bpy.context.view_layer.update()
    scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True
    scene.render.resolution_x=800;scene.render.resolution_y=600;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Neutral review');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.15,.18,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
    cam=bpy.data.objects.new('ReviewCamera',bpy.data.cameras.new('ReviewCamera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.lens=55
    side='L';wrist=rig.matrix_world@rig.pose.bones['HandBase_'+side].head
    mid=rig.matrix_world@rig.pose.bones['Mid1_'+side].head
    across=(rig.matrix_world@rig.pose.bones['Index1_'+side].head-rig.matrix_world@rig.pose.bones['Pinky1_'+side].head).normalized()
    up=(mid-wrist);up=(up-across*up.dot(across)).normalized()
    normal=up.cross(across).normalized()
    if normal.dot(rig.matrix_world@rig.pose.bones['Mid2_'+side].head-mid)<0:normal=-normal
    center=wrist.lerp(mid,.65)
    cam.location=center+normal*.65+up*.10+across*.05
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    light=bpy.data.lights.new('Key','AREA');light.energy=100;light.shape='DISK';light.size=.5
    lamp=bpy.data.objects.new('Key',light);scene.collection.objects.link(lamp);lamp.location=center+normal*.55+up*.65
    lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
    bodygroup={g.index for g in mesh.vertex_groups if any(g.name.startswith(k) for k in ('Thumb','Index','Mid','Pinky','Ring','Carpal'))}
    arrays={};mask=None
    for mode in ['source-order','subdivide-before-skin']:
     if mode=='subdivide-before-skin':
      index=next(i for i,m in enumerate(mesh.modifiers) if m.type=='ARMATURE')
      mesh.modifiers.move(index,len(mesh.modifiers)-1)
     bpy.context.view_layer.update()
     obj=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());m=obj.to_mesh(preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
     arrays[mode]=np.array([list(mesh.matrix_world@v.co) for v in m.vertices])
     if mask is None:mask=np.array([sum(g.weight for g in v.groups if g.group in bodygroup)>.8 for v in m.vertices])
     obj.to_mesh_clear();scene.render.filepath=str(out/(mode+'.png'));bpy.ops.render.render(write_still=True)
    lengths=np.linalg.norm(arrays['source-order']-arrays['subdivide-before-skin'],axis=1)
    report={'sample':'live Idle frame 1, shape keys zero','vertices':len(lengths),'hand_vertices':int(mask.sum()),'body_max_delta':float(lengths.max()),'hand_max_delta':float(lengths[mask].max()),'hand_mean_delta':float(lengths[mask].mean()),'hand_over_1mm':int((lengths[mask]>.001).sum())}
    (out/'comparison.json').write_text(json.dumps(report,indent=2));print('SUBDIVISION_COMPARE',json.dumps(report))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('rest', 'subdivision'))
    parser.add_argument('--output', type=Path, default=ROOT/'artifacts/fist-shape-20261012')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    folder = args.output.resolve()
    folder.relative_to((ROOT/'artifacts').resolve())
    folder.mkdir(parents=True, exist_ok=True)
    source = ROOT/'artifacts/character-sources/Ultraman Tiga.blend'
    fbx = ROOT/'unity/Assets/Resources/Characters/Tiga/Tiga.fbx'
    if args.mode == 'rest':
        compare_rest(source, fbx, folder)
    else:
        folder = folder/'subdivision'
        folder.mkdir(exist_ok=True)
        compare_subdivision(source, folder)


if __name__ == '__main__':
    main()
