"""Convert Extrazhang's Tiga into portable game assets; run inside Blender.

Source: https://blendswap.com/blend/26877 (CC-BY-NC, Extrazhang).
The downloaded source stays untouched. Embedded scripts are never enabled.
"""
import argparse
import math
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector, Quaternion


CHARACTER_OBJECTS = ('Armature.001', 'TigaBody', 'TigaEyes', 'TigaEnergy',
                     'TigaEnergyContainer', 'Cube.002')


def surface(name, color, metallic=0, roughness=.4, emission=0, texture=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Emission Color'].default_value = (*color, 1)
    shader.inputs['Emission Strength'].default_value = emission
    if texture:
        image = mat.node_tree.nodes.new('ShaderNodeTexImage')
        image.image = texture
        mat.node_tree.links.new(image.outputs['Color'], shader.inputs['Base Color'])
    mat.diffuse_color = (*color, 1)
    return mat


def prepare(source, output):
    bpy.ops.wm.open_mainfile(filepath=str(source.resolve()), load_ui=False, use_scripts=False)
    output.mkdir(parents=True, exist_ok=True)
    texture = bpy.data.images['Tiga_body_Multi.jpg.002']
    texture.filepath_raw = str((output / 'TigaBody.png').resolve())
    texture.file_format = 'PNG'
    texture.save()
    mats = {
        'BODY': surface('TigaSuit', (1, 1, 1), .22, .38, texture=texture),
        'ARMOR1.001': surface('TigaSilver', (.72, .76, .82), .75, .28),
        'ARMOR1': surface('TigaSilverTrim', (.72, .76, .82), .75, .28),
        'ARMOR2.001': surface('TigaGold', (.68, .40, .09), .7, .3),
        'EYESEMI': surface('TigaEyesGlow', (1, .83, .44), .05, .25, 2),
        'B': surface('TigaEyeRim', (.025, .03, .04), .4, .3),
        'E2': surface('TigaCrystal', (.18, .6, 1), .2, .2, 1),
        'E3S': surface('TigaTimer', (.07, .55, 1), .2, .2, 2),
    }
    for obj in bpy.data.objects:
        obj.hide_render = obj.name not in CHARACTER_OBJECTS
        if obj.type == 'MESH' and obj.name in CHARACTER_OBJECTS:
            for slot in obj.material_slots:
                if slot.material.name in mats:
                    slot.material = mats[slot.material.name]
            for mod in obj.modifiers:
                if mod.type == 'SUBSURF':
                    mod.levels = mod.render_levels = 1
    return bpy.data.objects['Armature.001']


def animate(rig, combat_sample=False, live_combat=False):
    """Author combat keys on the supplied IK rig; FBX export bakes the deforming bones."""
    scene = bpy.context.scene
    scene.frame_set(1)
    baseline = {p.name: p.matrix_basis.copy() for p in rig.pose.bones}
    rest_matrices = {p.name: p.matrix.copy() for p in rig.pose.bones}
    rig.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action, do_unlink=True)
    scene.render.fps = 60
    controls = ('Main', 'hip', 'TopCol', 'armIK_R', 'armIK_L', 'armIK_T_R',
                'armIK_T_L', 'HeelIKC_R', 'HeelIKC_L', 'FingerCol_R', 'FingerCol_L',
                'fingerRot_R', 'fingerRot_L', 'Thumb1_R', 'Thumb2_R', 'Thumb3_R',
                'Thumb1_L', 'Thumb2_L', 'Thumb3_L')
    idle = dict(right=(-.17, -.30, 1.30), left=(.20, -.30, 1.30), yaw=0, sink=0)
    beam = dict(right=(-.16, -.29, 1.54), left=(-.10, -.31, 1.23), yaw=-.04, sink=-.018, open=1,
                right_pole=(-.48, -.13, 1.12), left_pole=(.43, -.12, 1.13))

    def key(action, t, changes):
        # Keep already keyed curves from overwriting the next authored pose during IK evaluation.
        rig.animation_data_create().action = None
        for p in rig.pose.bones:
            p.matrix_basis = baseline[p.name]
        pose = dict(idle, **changes)
        opened = pose.get('open', 0)
        for side, sign in [('R', 1), ('L', -1)]:
            closed = Vector((sign * .1495, .05686, .00765))
            extended = Vector((sign * .02228, -.02039, .00133))
            rig.pose.bones['FingerCol_' + side].location = closed.lerp(extended, opened)
            curl = rig.pose.bones['fingerRot_' + side]
            curl.rotation_mode = 'XYZ'
            curl.rotation_euler = (0, 0, -sign * 1.25 * (1-opened))
            for joint, rotation in [(1, (.37, -.10*sign, -.49*sign)), (2, (.07, 0, 0)), (3, (.54, 0, 0))]:
                thumb = rig.pose.bones[f'Thumb{joint}_{side}']
                thumb.rotation_mode = 'XYZ'
                thumb.rotation_euler = Vector(rotation) * (1-opened)

        main = rig.pose.bones['Main']
        main_matrix = rest_matrices["Main"].copy()
        main_matrix.translation.z += pose['sink']
        main.matrix = main_matrix
        hip = rig.pose.bones['hip']
        hip.rotation_mode = 'QUATERNION'
        hip.rotation_quaternion = baseline['hip'].to_quaternion() @ Quaternion((0, 0, 1), pose['yaw'])
        bpy.context.view_layer.update()
        for name, target in (('armIK_R', pose['right']), ('armIK_L', pose['left']),
                             ('armIK_T_R', pose.get('right_pole', (-.6, .15, 1.05))),
                             ('armIK_T_L', pose.get('left_pole', (.65, .15, 1.05)))):
            p = rig.pose.bones[name]
            matrix = p.matrix.copy()
            matrix.translation = target
            p.matrix = matrix
        if combat_sample or live_combat:
            for side in ('L', 'R'):
                bone = rig.pose.bones['HeelIKC_' + side]
                matrix = rest_matrices[bone.name].copy()
                matrix.translation += Vector(pose.get('foot_' + side.lower(), (0, 0, 0)))
                bone.matrix = matrix
        bpy.context.view_layer.update()
        rig.animation_data.action = action
        frame = 1 + t * 60
        for name in controls:
            p = rig.pose.bones[name]
            p.keyframe_insert('location', frame=frame, group=name)
            p.keyframe_insert('rotation_quaternion' if p.rotation_mode == 'QUATERNION' else 'rotation_euler', frame=frame, group=name)
            p.keyframe_insert('scale', frame=frame, group=name)

    clips = {
        'Idle': [(0, {}), (.5, dict(sink=-.009, right=(-.175, -.30, 1.29))),
                 (1, dict(sink=0, left=(.195, -.30, 1.31))), (1.5, dict(sink=.006)), (2, {})],
        'LeftPunch': [(0, {}), (.045, dict(left=(.26, -.18, 1.23), yaw=-.12, sink=-.02)),
                      (.12, dict(left=(.12, -.53, 1.34), right=(-.22, -.25, 1.29), yaw=.13, sink=-.04)),
                      (.18, dict(left=(.12, -.52, 1.34), yaw=.12, sink=-.035)), (.38, {})],
        'RightPunch': [(0, {}), (.045, dict(right=(-.26, -.18, 1.23), yaw=.12, sink=-.02)),
                       (.12, dict(right=(-.12, -.53, 1.34), left=(.22, -.25, 1.29), yaw=-.13, sink=-.04)),
                       (.18, dict(right=(-.12, -.52, 1.34), yaw=-.12, sink=-.035)), (.38, {})],
        'Guard': [(0, {}), (.15, dict(right=(-.06, -.28, 1.39), left=(.06, -.33, 1.36), sink=-.035)),
                  (.8, dict(right=(-.06, -.28, 1.39), left=(.06, -.33, 1.36), sink=-.025))],
        'Beam': [(0, {}), (.18, dict(right=(.12, -.30, 1.27), left=(-.12, -.34, 1.22), sink=-.04)),
                 (.6, beam), (1.22, beam), (1.3, dict(beam, sink=-.035)), (1.95, beam), (2.3, {})],
        'Hurt': [(0, {}), (.12, dict(right=(-.30, -.13, 1.17), left=(.32, -.13, 1.2), yaw=.08, sink=-.07)),
                 (.32, dict(right=(-.24, -.22, 1.23), left=(.25, -.22, 1.22), sink=-.04)), (.55, {})],
        'Transform': [(0, dict(right=(-.3, -.1, 1.13), left=(.3, -.1, 1.13))),
                      (.4, dict(right=(.08, -.3, 1.35), left=(-.08, -.35, 1.30), sink=-.04)),
                      (1.0, dict(right=(-.29, -.12, 1.73), left=(.29, -.12, 1.73))),
                      (1.6, dict(right=(-.29, -.12, 1.73), left=(.29, -.12, 1.73))), (2.2, {})],
        'Victory': [(0, {}), (.5, dict(right=(-.2, -.08, 1.77), left=(.3, -.08, 1.13))),
                    (1.1, dict(right=(-.23, -.08, 1.75), left=(.3, -.08, 1.13), sink=.008)),
                    (2.0, dict(right=(-.2, -.08, 1.77), left=(.3, -.08, 1.13)))],
    }
    if combat_sample:
        # Separate review clips: weight transfer and a slower recovery are tested
        # before changing the timing of the camera-controlled game.
        for side, sign in [('Left', 1), ('Right', -1)]:
            hand = side.lower()
            foot = 'foot_l' if side == 'Left' else 'foot_r'
            clips[side+'Punch'] = [
                (0, {}), (.14, {hand:(sign*.28,-.18,1.24), 'yaw':-sign*.20, 'sink':-.04, foot:(0,.025,.025)}),
                (.28, {hand:(sign*.10,-.55,1.34), 'yaw':sign*.25, 'sink':-.055, foot:(0,-.09,0)}),
                (.36, {hand:(sign*.11,-.54,1.33), 'yaw':sign*.23, 'sink':-.05, foot:(0,-.09,0)}),
                (.50, {hand:(sign*.22,-.35,1.29), 'yaw':sign*.10, 'sink':-.025, foot:(0,-.04,.035)}), (.68,{})]
        walk=[]
        for i in range(33):
            t=i/16;phase=t*math.tau
            walk.append((t, dict(sink=-.018+.008*math.cos(phase*2), yaw=.045*math.sin(phase),
                foot_l=(0,.10*math.sin(phase),.055*max(0,math.cos(phase))),
                foot_r=(0,-.10*math.sin(phase),.055*max(0,-math.cos(phase))))))
        clips['Walk']=walk
    if live_combat:
        # Preserve Battle's .12 s contact and .38 s recovery. The rear foot
        # offsets the .75-unit root advance; the lead foot lifts and plants.
        for side in ('Left', 'Right'):
            lead='foot_l' if side=='Left' else 'foot_r'
            rear='foot_r' if side=='Left' else 'foot_l'
            existing=clips[side+'Punch']
            existing[1][1].update({lead:(0,.025,.065),rear:(0,.11,.01)})
            existing[2][1].update({lead:(0,0,0),rear:(0,.35,.04)})
            existing[3][1].update({lead:(0,-.055,0),rear:(0,.30,.025)})
            hand=side.lower();sign=1 if side=='Left' else -1
            existing.insert(-1,(.27,{hand:(sign*.21,-.34,1.29),'yaw':sign*.05,'sink':-.025,
                                    lead:(0,-.10,.065),rear:(0,.11,0)}))
    actions = {}
    for name, keys in clips.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        for t, pose in keys:
            key(action, t, pose)
        for curve in action.fcurves:
            for point in curve.keyframe_points:
                point.interpolation = 'BEZIER'
                point.handle_left_type = point.handle_right_type = 'AUTO_CLAMPED'
        actions[name] = action
    rig.animation_data.action = actions['Idle']
    scene.frame_set(1)
    return actions


def export(rig, output, actions):
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    for name in CHARACTER_OBJECTS:
        bpy.data.objects[name].select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str((output / 'Tiga.fbx').resolve()), use_selection=True,
                             object_types={'MESH', 'ARMATURE'}, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_ALL',
                             add_leaf_bones=False, use_armature_deform_only=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=.1,
                             path_mode='RELATIVE', use_mesh_modifiers=True)
    (output / 'clips.json').write_text(json.dumps({name: (a.frame_range[1]-a.frame_range[0])/60
                                                 for name, a in actions.items()}, indent=2))


def render_review(output, rig, actions):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 20
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 560
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new('Review world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.09, .11, .16, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .6
    camera = bpy.data.objects.new('ReviewCamera', bpy.data.cameras.new('ReviewCamera'))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 3.8
    for name, loc, power, color, size in (
        ('Key', (3, -4, 5), 450, (1, .85, .72), 4),
        ('Fill', (-4, -2, 2.5), 250, (.55, .72, 1), 3),
        ('Rim', (1, 3, 4), 550, (.55, .8, 1), 3),
    ):
        light = bpy.data.lights.new(name, 'AREA')
        light.energy, light.color, light.shape, light.size = power, color, 'DISK', size
        obj = bpy.data.objects.new(name, light)
        scene.collection.objects.link(obj)
        obj.location = loc
        obj.rotation_euler = (Vector((0, 0, 1.6)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    review = output / 'review'
    review.mkdir(exist_ok=True)
    for name, seconds in [('Idle', 0), ('LeftPunch', .12), ('RightPunch', .12), ('Guard', .25),
                          ('Beam', .8), ('Transform', 1.2), ('Victory', .7)]:
        rig.animation_data.action = actions[name]
        scene.frame_set(1 + int(seconds * 60))
        camera.location = (3, -7, 3)
        camera.rotation_euler = (Vector((0, 0, 1.55)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str((review / f'{name}.png').resolve())
        bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--review', action='store_true')
    parser.add_argument('--export', action='store_true')
    parser.add_argument('--combat-sample', action='store_true')
    parser.add_argument('--live-combat', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    rig = prepare(args.source, args.output)
    if args.live_combat and args.combat_sample:parser.error('choose sample or live combat, not both')
    actions = animate(rig, args.combat_sample, args.live_combat)
    if args.export:
        export(rig, args.output, actions)
    if args.review:
        render_review(args.output, rig, actions)


if __name__ == '__main__':
    main()
