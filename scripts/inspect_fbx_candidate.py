"""Blender-only, non-destructive candidate audit; never exports a game asset.

Run with --factory-startup --disable-autoexec --background --python this.py --
SOURCE.fbx OUTPUT_DIRECTORY. Images are actual mesh renders, not concept art.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--flip-up', action='store_true', help='Rotate the temporary inspection scene 180 degrees around X')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, output = args.source.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE']
    report = dict(source_name=source.name, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  meshes=[], armatures=[], actions=[a.name for a in bpy.data.actions], images=[], preview_flip_up=args.flip_up)
    for obj in meshes:
        obj.data.calc_loop_triangles()
        report['meshes'].append(dict(name=obj.name, vertices=len(obj.data.vertices),
            triangles=len(obj.data.loop_triangles), vertex_groups=len(obj.vertex_groups),
            armature_modifiers=[m.object.name if m.object else None for m in obj.modifiers if m.type == 'ARMATURE']))
    for obj in rigs:
        report['armatures'].append(dict(name=obj.name, bones=[b.name for b in obj.data.bones]))
    for img in bpy.data.images:
        if img.source != 'FILE':
            continue
        # Resolve only the same-directory texture filename, not arbitrary paths
        # embedded in a downloaded FBX. Keep the candidate archive untouched.
        local = source.parent / Path(img.filepath.replace('\\', '/')).name
        if local.is_file():
            img.filepath = str(local)
            img.reload()
        report['images'].append(dict(name=img.name, size=list(img.size), resolved=local.is_file()))
    if not meshes:
        raise RuntimeError('Candidate contains no mesh')
    points = [obj.matrix_world @ Vector(p) for obj in meshes for p in obj.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    report['source_bounds'] = dict(min=list(low), max=list(high))
    (output / 'model-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print('[CandidateAudit] ' + json.dumps(report), flush=True)
    # Normalize only this temporary inspection scene, with Z up after FBX import.
    center = (low + high) * .5
    scale = 3 / max(high.z - low.z, .0001)
    rotation = Matrix.Rotation(math.pi if args.flip_up else 0, 4, 'X')
    transform = Matrix.Translation(Vector((0, 0, 1.5))) @ Matrix.Scale(scale, 4) @ rotation @ Matrix.Translation(-center)
    roots = [obj for obj in bpy.context.scene.objects if obj.parent is None]
    for obj in roots:
        obj.matrix_world = transform @ obj.matrix_world
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 8
    scene.cycles.use_denoising = True
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 4
    scene.render.resolution_x, scene.render.resolution_y = 540, 720
    scene.render.resolution_percentage = 100
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes['Background']
    background.inputs[0].default_value = (.12, .15, .20, 1)
    background.inputs[1].default_value = .5
    scene.view_settings.view_transform = 'Standard'
    camera = bpy.data.objects.new('CandidateCamera', bpy.data.cameras.new('CandidateCamera'))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type, camera.data.ortho_scale = 'ORTHO', 3.7
    for name, location, energy in [('Key', (3, -4, 5), 500), ('Fill', (-4, -2, 3), 350), ('Back', (1, 4, 4), 500)]:
        light = bpy.data.lights.new(name, 'AREA')
        light.energy, light.size = energy, 4
        obj = bpy.data.objects.new(name, light)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0, 0, 1.5)) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    for index in range(4):
        angle = math.radians(index * 90)
        camera.location = (math.sin(angle) * 7, -math.cos(angle) * 7, 1.85)
        camera.rotation_euler = (Vector((0, 0, 1.5)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(output / f'view-{index * 90:03}.png')
        bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    main()
