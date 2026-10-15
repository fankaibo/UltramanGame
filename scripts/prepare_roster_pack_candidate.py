"""Prepare a private Mebius candidate from the user's 21-hero FBX pack.

Run in Blender with --factory-startup --disable-autoexec --background.
Writes only to .cache or artifacts; never replaces a shipped character.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare_roster import retarget


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, output = args.source.resolve(), args.output.resolve()
    root = Path(__file__).resolve().parents[1]
    if not any(output.is_relative_to(root / folder) for folder in ['.cache', 'artifacts']):
        raise ValueError('Candidate output must stay in .cache or artifacts')
    output.mkdir(parents=True, exist_ok=True)
    texture_dir = output / 'Textures'
    texture_dir.mkdir(exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True, use_image_search=False)
    bpy.context.scene.frame_set(1)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and 'Mebius' in o.name]
    if len(meshes) != 1:
        raise ValueError('Expected exactly one Mebius mesh')
    mesh = meshes[0]
    rig = next(m.object for m in mesh.modifiers if m.type == 'ARMATURE')
    bpy.context.view_layer.update()
    authored_fingers = {bone.name: bone.matrix_basis.to_quaternion().copy()
                        for bone in rig.pose.bones if 'Hand' in bone.name and
                        any(finger in bone.name for finger in ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky'])}
    transforms = {o: o.matrix_world.copy() for o in [mesh, rig]}
    for obj in [mesh, rig]:
        obj.parent = None
        obj.matrix_world = transforms[obj]
        obj.animation_data_clear()
    for obj in list(bpy.data.objects):
        if obj not in [mesh, rig]:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    mapping = {'Hips': 'bip_pelvis', 'Spine': 'bip_spine_0', 'Spine1': 'bip_spine_1',
               'Spine2': 'bip_spine_2', 'Neck': 'bip_neck', 'Head': 'bip_head'}
    for prefix, side in [('Left', 'L'), ('Right', 'R')]:
        for old, new in [('Shoulder', 'collar'), ('Arm', 'upperArm'), ('ForeArm', 'lowerArm'),
                         ('Hand', 'hand'), ('UpLeg', 'hip'), ('Leg', 'knee'), ('Foot', 'foot'), ('ToeBase', 'toe')]:
            mapping[prefix + old] = f'bip_{new}_{side}'
        for old, new in [('Thumb', 'thumb'), ('Index', 'index'), ('Middle', 'middle'), ('Ring', 'ring'), ('Pinky', 'pinky')]:
            for index in range(1, 4):
                mapping[f'{prefix}Hand{old}{index}'] = f'bip_{new}_{index - 1}_{side}'
    closed_fingers = {}
    for bone in list(rig.data.bones):
        old = bone.name
        key = re.sub(r'_\d+$', '', old.removeprefix('mixamorig_'))
        new = mapping.get(key)
        if new:
            bone.name = new
            if old in authored_fingers:
                closed_fingers[new] = authored_fingers[old]
            if old in mesh.vertex_groups:
                mesh.vertex_groups[old].name = new
    if len(closed_fingers) != 30:
        raise ValueError('Expected 30 mapped finger joints for the authored grip')
    rig.name = 'MebiusRig'
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    # Put anatomical left along +X, front towards -Y, then place the feet at Z=0.
    def rest(name):
        return (rig.matrix_world @ rig.data.bones[name].matrix_local).translation
    across = rest('bip_upperArm_L') - rest('bip_upperArm_R')
    rotation = Matrix.Rotation(-math.atan2(across.y, across.x), 4, 'Z')
    points = [rotation @ mesh.matrix_world @ Vector(p) for p in mesh.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center = (low + high) * .5
    transform = Matrix.Scale(1.6 / (high.z - low.z), 4) @ Matrix.Translation(Vector((-center.x, -center.y, -low.z))) @ rotation
    for obj in [mesh, rig]:
        obj.matrix_world = transform @ obj.matrix_world
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001)
    bpy.ops.object.mode_set(mode='OBJECT')
    for poly in mesh.data.polygons:
        poly.use_smooth = True
    sub = mesh.modifiers.new('Silhouette smoothing', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    mesh.modifiers.move(mesh.modifiers.find(sub.name), 0)
    textures = []
    used = set()
    for slot in mesh.material_slots:
        mat = slot.material
        shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        shader.inputs['Metallic'].default_value = .2
        shader.inputs['Roughness'].default_value = .43
        for node in mat.node_tree.nodes:
            if node.type != 'TEX_IMAGE' or not node.image or node.image in used:
                continue
            img = node.image
            used.add(img)
            filename = Path(img.filepath.replace('\\', '/')).name
            path = source.parent / 'tex' / filename
            if not path.is_file():
                raise ValueError('Missing candidate texture: ' + filename)
            img.filepath = str(path)
            img.reload()
            native = list(img.size)
            factor = min(1, 4096 / max(native))
            if factor < 1:
                img.scale(max(1, round(native[0] * factor)), max(1, round(native[1] * factor)))
            img.filepath_raw = str(texture_dir / filename)
            img.file_format = 'PNG'
            img.save()
            textures.append(dict(file=filename, native=native, candidate=list(img.size)))
    # Bone conversion does not read deformed vertices. Restore skinning and
    # subdivision only for the exported candidate, avoiding thousands of
    # unnecessary mesh evaluations while the reference bones are solved.
    visibility = {modifier: modifier.show_viewport for modifier in mesh.modifiers}
    try:
        for modifier in visibility:
            modifier.show_viewport = False
        actions = retarget(rig)
    finally:
        for modifier, enabled in visibility.items():
            modifier.show_viewport = enabled
        bpy.context.view_layer.update()
    required = {'Idle', 'Transform', 'LeftPunch', 'RightPunch', 'Guard', 'Beam', 'Hurt', 'Victory'}
    if set(actions) != required:
        raise ValueError('Unexpected clips: ' + str(list(actions)))
    # Use the source character's closed hand for punches. Retargeting another
    # hero's finger directions can leave a hollow C-shaped grip on this mesh.
    for name in ['LeftPunch', 'RightPunch']:
        for curve in actions[name].fcurves:
            for bone, rotation in closed_fingers.items():
                if curve.data_path == f'pose.bones["{bone}"].rotation_quaternion':
                    for key in curve.keyframe_points:
                        key.co.y = rotation[curve.array_index]
                    curve.update()
                    break
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(output / 'Mebius.fbx'), use_selection=True,
        object_types={'MESH', 'ARMATURE'}, axis_forward='-Z', axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, bake_anim=True,
        bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0, path_mode='RELATIVE', use_mesh_modifiers=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / 'Mebius-candidate.blend'))
    report = dict(character='Mebius', candidate_only=True,
                  source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  clips=list(actions), textures=textures, bones=len(rig.data.bones),
                  punch_fingers='authored first-frame grip', closed_finger_bones=len(closed_fingers),
                  fbx_sha256=hashlib.sha256((output / 'Mebius.fbx').read_bytes()).hexdigest())
    (output / 'candidate.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print('[RosterPackCandidate] ' + json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
