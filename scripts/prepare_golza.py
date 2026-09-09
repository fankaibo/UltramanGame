"""Convert the user-downloaded Source Golza into a review-only skeletal asset.

Uses REDxEYE/SourceIO from its official repository. No source-file scripts run.
Source model: https://sfmlab.com/project/5e6c0302-9b99-4e34-819f-cd0c5fbcc041/
"""
import argparse
import json
import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector, Quaternion


def import_source(source, output, sourceio):
    os.environ['NO_BPY'] = '1'
    sys.path.insert(0, str(sourceio.resolve().parent))
    from SourceIO.blender_bindings.models import import_model
    from SourceIO.blender_bindings.models.common import put_into_collections
    from SourceIO.blender_bindings.operators.import_settings_base import ModelOptions
    from SourceIO.library.shared.content_manager import ContentManager
    from SourceIO.library.shared.content_manager.providers.loose_files import LooseFilesContentProvider
    from SourceIO.library.utils import FileBuffer
    from SourceIO.library.utils.tiny_path import TinyPath
    from SourceIO.library.source1.vtf import load_texture
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    manager = ContentManager()
    manager.add_child(LooseFilesContentProvider(TinyPath(str(source.resolve()))))
    options = ModelOptions.default()
    options.import_textures = options.import_animations = options.create_flex_drivers = False
    mdl = TinyPath(str((source/'models/ultimo/golza_human_size.mdl').resolve()))
    with FileBuffer(mdl) as stream:
        container = import_model(mdl, stream, manager, options)
    put_into_collections(container, 'Golza')
    source_counts={'source_vertices':sum(len(o.data.vertices) for o in container.objects),
                   'source_triangles':sum(len(o.data.polygons) for o in container.objects)}
    images = {}
    for name, exported in [('Golzatex', 'GolzaBody'), ('Golzaeyes', 'GolzaEyes')]:
        path = TinyPath(str((source/f'materials/models/ultimo/Golza/{name}.vtf').resolve()))
        with FileBuffer(path) as stream:
            pixels, height, width = load_texture(stream)
        image = bpy.data.images.new(exported, width=width, height=height, alpha=True)
        # VTF decoder rows start at the top; Blender's pixel buffer starts at the bottom.
        image.pixels.foreach_set(pixels[::-1].ravel())
        image.update()
        image.filepath_raw = str(output/(exported+'.png'))
        image.file_format = 'PNG'
        image.save()
        images[exported] = image
    for mesh in container.objects:
        # Source splits vertices along UV/material seams. Weld positions before
        # subdivision so those seams do not shrink into visible ankle/arm gaps.
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.select_all(action='DESELECT')
        mesh.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.remove_doubles(threshold=.00001)
        bpy.ops.object.mode_set(mode='OBJECT')
        for polygon in mesh.data.polygons:
            polygon.use_smooth = True
        # One subdivision softens the silhouette; it does not invent texture detail.
        subdivision = mesh.modifiers.new('Silhouette smoothing', 'SUBSURF')
        subdivision.levels = subdivision.render_levels = 1
        for slot in mesh.material_slots:
            eye = 'eyes' in slot.material.name.lower()
            mat = bpy.data.materials.new('GolzaEyes' if eye else 'GolzaHide')
            mat.use_nodes = True
            shader = mat.node_tree.nodes.get('Principled BSDF')
            shader.inputs['Roughness'].default_value = .54 if eye else .68
            texture = mat.node_tree.nodes.new('ShaderNodeTexImage')
            texture.image = images['GolzaEyes' if eye else 'GolzaBody']
            mat.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
            if eye:
                mat.node_tree.links.new(texture.outputs['Color'], shader.inputs['Emission Color'])
                shader.inputs['Emission Strength'].default_value = .4
            slot.material = mat
    rig = container.armature
    rig.name = 'GolzaRig'
    return rig, container.objects, source_counts


def rig_controls(rig):
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bones = rig.data.edit_bones
    targets = {}
    for side, sign in [('L', 1), ('R', -1)]:
        for a, b in [(f'bip_upperArm_{side}', f'bip_lowerArm_{side}'),
                     (f'bip_lowerArm_{side}', f'bip_hand_{side}'),
                     (f'bip_hip_{side}', f'bip_knee_{side}'),
                     (f'bip_knee_{side}', f'bip_foot_{side}')]:
            bones[a].tail = bones[b].head
            bones[a].align_roll(Vector((0, 1, 0)))
        for name, head in [(f'HandIK_{side}', bones[f'bip_hand_{side}'].head.copy()),
                           (f'FootIK_{side}', bones[f'bip_foot_{side}'].head.copy()),
                           (f'Elbow_{side}', Vector((sign*.65, .10, 1.05))),
                           (f'Knee_{side}', Vector((sign*.25, -.6, .4)))]:
            bone = bones.new(name)
            bone.head = head
            bone.tail = head + Vector((0, 0, .07))
            bone.use_deform = False
            if name.startswith('FootIK'):
                bone.matrix = bones[f'bip_foot_{side}'].matrix.copy()
            targets[name] = head.copy()
    bpy.ops.object.mode_set(mode='POSE')
    for side in ['L', 'R']:
        for segment, target, pole in [('lowerArm', 'HandIK', 'Elbow'), ('knee', 'FootIK', 'Knee')]:
            bone = rig.pose.bones[f'bip_{segment}_{side}']
            ik = bone.constraints.new('IK')
            ik.target = rig
            ik.subtarget = f'{target}_{side}'
            ik.pole_target = rig
            ik.pole_subtarget = f'{pole}_{side}'
            ik.chain_count = 2
            ik.use_stretch = False
            ik.pole_angle = 0
        foot = rig.pose.bones[f'bip_foot_{side}']
        planted = foot.constraints.new('COPY_ROTATION')
        planted.target = rig
        planted.subtarget = f'FootIK_{side}'
        planted.target_space = planted.owner_space = 'POSE'
    bpy.ops.object.mode_set(mode='OBJECT')
    return targets


def author(rig, targets, live_combat=False):
    scene = bpy.context.scene
    scene.render.fps = 60
    rest = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
    control_names = list(targets) + ['bip_pelvis', 'bip_spine_1', 'bip_spine_2', 'bip_head', 'jaw'] + [f'tail_{i}' for i in range(1, 7)]

    def rotate(name, axis, angle):
        bone = rig.pose.bones[name]
        bone.rotation_mode = 'QUATERNION'
        local_axis = bone.bone.matrix_local.to_3x3().inverted() @ Vector(axis)
        bone.rotation_quaternion = Quaternion(local_axis, math.radians(angle))

    def key(action, t, pose):
        rig.animation_data_create().action = None
        for b in rig.pose.bones:
            b.matrix_basis = rest[b.name]
        bpy.context.view_layer.update()
        p = dict(sink=.018, lean=5, yaw=0, jaw=3, head=0,
                 left=(.30, -.23, 1.07), right=(-.30, -.23, 1.07), sway=0)
        p.update(pose)
        pelvis = rig.pose.bones['bip_pelvis']
        rotate('bip_pelvis', (0, 0, 1), p['yaw'])
        pelvis.location += pelvis.bone.matrix_local.to_3x3().inverted() @ Vector((0, 0, -p['sink']))
        rotate('bip_spine_1', (1, 0, 0), p['lean'])
        rotate('bip_spine_2', (0, 0, 1), p['yaw']*.55)
        rotate('bip_head', (1, 0, 0), p['head'])
        rotate('jaw', (1, 0, 0), p['jaw'])
        for i in range(1, 7):
            rotate(f'tail_{i}', (0, 0, 1), p['sway']*math.sin(i*.65+t*2))
        # The tail begins close to the floor. Lift its base as the pelvis sinks,
        # so crouching does not drag the distal skin through the plaza.
        tail=rig.pose.bones['tail_1']
        lift=math.atan2(p['sink'],.75)
        axis=tail.bone.matrix_local.to_3x3().inverted() @ Vector((1,0,0))
        tail.rotation_quaternion=Quaternion(axis,lift) @ tail.rotation_quaternion
        bpy.context.view_layer.update()
        for name in targets:
            bone = rig.pose.bones[name]
            matrix = bone.matrix.copy()
            point = targets[name].copy()
            if name.startswith('HandIK'):
                point = Vector(p['left' if name.endswith('L') else 'right'])
            elif name.startswith('FootIK'):
                point += Vector(p.get('foot_l' if name.endswith('L') else 'foot_r', (0, 0, 0)))
            matrix.translation = point
            bone.matrix = matrix
        bpy.context.view_layer.update()
        rig.animation_data.action = action
        for name in control_names:
            bone = rig.pose.bones[name]
            bone.keyframe_insert('location', frame=1+round(t*60), group=name)
            bone.keyframe_insert('rotation_quaternion' if bone.rotation_mode=='QUATERNION' else 'rotation_euler', frame=1+round(t*60), group=name)

    motions = {
        'Idle': [(0, {}), (.5, dict(sink=.025, sway=3)), (1, dict(sink=.018, sway=-3)), (1.5, dict(sink=.012,sway=2)), (2, {})],
        'Windup': [(0, {}), (.4, dict(sink=.035, lean=-5, jaw=18, right=(-.40,.02,1.25), sway=9)),
                   (1.1, dict(sink=.06, lean=12, jaw=24,right=(-.37,.00,1.20),left=(.32,-.30,1.02),sway=-8)), (1.5, dict(sink=.05,lean=14,jaw=24,right=(-.38,.01,1.22),sway=9))],
        'Attack': [(0, dict(sink=.05,lean=14,right=(-.38,.01,1.22),jaw=20)),
                   (.16, dict(sink=.02,lean=15,foot_l=(0,-.10,.10),foot_r=(0,.04,0),right=(-.39,-.05,1.20),jaw=24,sway=-8)),
                   (.30, dict(sink=.04,lean=16,foot_l=(0,-.16,0),foot_r=(0,.04,0),right=(-.20,-.40,1.20),jaw=20,sway=10)),
                   (.4, dict(sink=.065,lean=18,yaw=-14,foot_l=(0,-.15,0),right=(.05,-.39,.93),left=(.27,-.17,1.08),jaw=24,sway=12)),
                   (.55, dict(sink=.07,lean=16,yaw=-12,right=(.07,-.35,.9),foot_l=(0,-.12,0),sway=9)),
                   (.8, dict(sink=.035,lean=6,foot_l=(0,-.05,.04),sway=-6)), (1.05,{})],
        'Hurt': [(0,{}),(.1,dict(lean=-14,sink=.05,yaw=10,jaw=18,right=(-.40,-.07,1.08),left=(.37,-.10,1.12),sway=14)),(.23,dict(lean=-7,sink=.04,jaw=8,sway=-7)),(.4,{})],
        'Defeat': [(0,{}),(.25,dict(lean=-18,jaw=25,sink=.03,sway=14)),(.8,dict(lean=16,sink=.12,jaw=12,right=(-.28,-.25,.92),left=(.29,-.25,.92),sway=-8)),
                   (1.5,dict(lean=25,sink=.18,head=12,jaw=5,right=(-.24,-.27,.86),left=(.24,-.27,.86))), (2.4,dict(lean=25,sink=.18,head=12,jaw=5,right=(-.24,-.27,.86),left=(.24,-.27,.86)))],
    }
    # A slow approach with planted feet, separate from the quick attack clip.
    walk = []
    for i in range(17):
        t=i/8
        phase=t*math.tau
        stride=.12*math.sin(phase)
        walk.append((t,dict(sink=.028+.009*math.cos(phase*2),lean=7,sway=5,
            foot_l=(0,stride,.055*max(0,math.cos(phase))),foot_r=(0,-stride,.055*max(0,-math.cos(phase))))))
    motions['Walk']=walk
    if live_combat:
        # Match the shorter live-game rush. Right foot counters root travel;
        # left foot takes the step, then lifts during recovery.
        offsets=[((0,0,0),(0,0,0)),((0,.04,.06),(0,.03,0)),
                 ((0,0,.025),(0,.24,.02)),((0,0,0),(0,.30,.02)),
                 ((0,0,0),(0,.30,.02)),((0,-.10,.04),(0,.12,0)),((0,0,0),(0,0,0))]
        for (_,pose),(left,right) in zip(motions['Attack'],offsets):
            pose.update(foot_l=left,foot_r=right)
    actions={}
    for name, poses in motions.items():
        action=bpy.data.actions.new(name);action.use_fake_user=True
        for t,pose in poses:key(action,t,pose)
        for curve in action.fcurves:
            for point in curve.keyframe_points:
                point.interpolation='BEZIER';point.handle_left_type=point.handle_right_type='AUTO_CLAMPED'
        actions[name]=action
    rig.animation_data.action=actions['Idle'];scene.frame_set(1)
    return actions


def render(output, rig, actions):
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Review world');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.11,.16,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
    camera=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));scene.collection.objects.link(camera);scene.camera=camera
    camera.data.type='ORTHO';camera.data.ortho_scale=2.25
    camera.location=(2.4,-4,2);camera.rotation_euler=(Vector((0,.15,.8))-camera.location).to_track_quat('-Z','Y').to_euler()
    for name,loc,power,color,size in [('Key',(2,-3,4),240,(1,.82,.65),3),('Fill',(-2,-1,2),160,(.45,.68,1),3),('Rim',(1,3,3),350,(.6,.8,1),2)]:
        data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
        obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=loc
        obj.rotation_euler=(Vector((0,0,.9))-obj.location).to_track_quat('-Z','Y').to_euler()
    for name,t in [('Idle',0),('Windup',1.1),('Attack',.4),('Hurt',.1),('Defeat',1.5)]:
        rig.animation_data.action=actions[name];scene.frame_set(1+round(t*60))
        scene.render.filepath=str(output/(name+'.png'));bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('source',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--sourceio',type=Path,default=Path('.cache/character-tools/SourceIO'))
    parser.add_argument('--review',action='store_true');parser.add_argument('--export',action='store_true')
    parser.add_argument('--live-combat',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
    rig,meshes,source_counts=import_source(args.source,output,args.sourceio)
    targets=rig_controls(rig);actions=author(rig,targets,args.live_combat)
    report=dict(source_counts,bones=len(rig.data.bones),welded_vertices=sum(len(o.data.vertices) for o in meshes),
                clips={n:(a.frame_range[1]-a.frame_range[0])/60 for n,a in actions.items()})
    (output/'conversion.json').write_text(json.dumps(report,indent=2))
    if args.export:
        bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
        for o in meshes:o.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(output/'Golza.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=.1,path_mode='RELATIVE',use_mesh_modifiers=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'Golza-review.blend'))
    if args.review:render(output,rig,actions)
    print('GOLZA_REVIEW',json.dumps(report))

if __name__=='__main__':main()
