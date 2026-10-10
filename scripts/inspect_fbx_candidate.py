"""Blender-only, non-destructive candidate audit; never exports a game asset.

Run with --factory-startup --disable-autoexec --background --python this.py --
SOURCE.fbx OUTPUT_DIRECTORY. Images are actual mesh renders, not concept art.
"""
import argparse
import hashlib
import json
import math
import struct
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--object-filter', help='Inspect only mesh names containing this text')
    parser.add_argument('--audit-only', action='store_true', help='Write inventory without rendering a multi-character pack')
    parser.add_argument('--smooth-preview', action='store_true', help='Preview one subdivision level in memory; do not export or alter source')
    parser.add_argument('--clip', help='Preview a named imported animation clip')
    parser.add_argument('--frame', type=int, default=1, help='Frame within the imported clip')
    parser.add_argument('--flip-up', action='store_true', help='Rotate the temporary inspection scene 180 degrees around X')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source, output = args.source.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.render.fps = 60
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True, use_image_search=False)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if args.object_filter:
        selected = [obj for obj in meshes if args.object_filter.lower() in obj.name.lower()]
        if not selected:
            raise RuntimeError('No mesh matches object filter')
        for obj in meshes:
            if obj not in selected:
                bpy.data.objects.remove(obj, do_unlink=True)
        meshes = selected
    rigs = list({modifier.object for obj in meshes for modifier in obj.modifiers
                 if modifier.type == 'ARMATURE' and modifier.object})
    if args.clip:
        for rig in rigs:
            matching = [a for a in bpy.data.actions if a.name.split('|')[-1] == args.clip]
            if len(matching) != 1:
                raise ValueError('Expected one action for clip: ' + args.clip)
            rig.animation_data_create()
            rig.animation_data.use_nla = False
            rig.animation_data.action = matching[0]
            if len(matching[0].slots):
                rig.animation_data.action_slot = matching[0].slots[0]
    bpy.context.scene.frame_set(args.frame)
    bpy.context.view_layer.update()
    used_images = {node.image for obj in meshes for slot in obj.material_slots
                   if slot.material and slot.material.use_nodes for node in slot.material.node_tree.nodes
                   if node.type == 'TEX_IMAGE' and node.image}
    report = dict(source_name=source.name, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  meshes=[], armatures=[], actions=[a.name for a in bpy.data.actions], images=[], preview_flip_up=args.flip_up,
                  preview_subdivision_levels=1 if args.smooth_preview else 0,
                  preview_clip=args.clip, preview_frame=args.frame, preview_fps=60)
    for obj in meshes:
        obj.data.calc_loop_triangles()
        report['meshes'].append(dict(name=obj.name, vertices=len(obj.data.vertices),
            triangles=len(obj.data.loop_triangles), vertex_groups=len(obj.vertex_groups),
            weighted_vertices=sum(any(g.weight > 0 for g in vertex.groups) for vertex in obj.data.vertices),
            armature_modifiers=[m.object.name if m.object else None for m in obj.modifiers if m.type == 'ARMATURE']))
    for obj in rigs:
        report['armatures'].append(dict(name=obj.name, bones=[b.name for b in obj.data.bones]))
    for img in bpy.data.images:
        if img.source != 'FILE' or img not in used_images:
            continue
        # Resolve only the same-directory texture filename, not arbitrary paths
        # embedded in a downloaded FBX. Keep the candidate archive untouched.
        filename = Path(img.filepath.replace('\\', '/')).name
        candidates = [source.parent / filename, source.parent / 'tex' / filename,
                      source.parent / 'Textures' / filename]
        local = next((path for path in candidates if path.is_file()), candidates[0])
        size = [0, 0]
        if local.is_file():
            # Reading PNG dimensions needs 24 bytes, not a multi-gigabyte
            # decode of every 11K atlas in a downloaded roster.
            with local.open('rb') as handle:
                header = handle.read(24)
            if header.startswith(b'\x89PNG\r\n\x1a\n'):
                size = list(struct.unpack('>II', header[16:24]))
            if not args.audit_only:
                img.filepath = str(local)
                img.reload()
                size = list(img.size)
        report['images'].append(dict(name=img.name, size=size, resolved=local.is_file()))
    if not meshes:
        raise RuntimeError('Candidate contains no mesh')
    points = [obj.matrix_world @ Vector(p) for obj in meshes for p in obj.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    report['source_bounds'] = dict(min=list(low), max=list(high))
    (output / 'model-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print('[CandidateAudit] meshes=%d armatures=%d images=%d output=%s' % (len(meshes), len(rigs), len(report['images']), output), flush=True)
    if args.audit_only:
        return
    # Freeze the imported first-frame pose before resizing the inspection
    # scene. Otherwise animated root scale/location is reapplied on render,
    # making identically framed candidates appear at different sizes.
    for obj in bpy.context.scene.objects:
        obj.animation_data_clear()
    if args.smooth_preview:
        for obj in meshes:
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.mesh.remove_doubles(threshold=.00001)
            bpy.ops.object.mode_set(mode='OBJECT')
            for face in obj.data.polygons:
                face.use_smooth = True
            modifier = obj.modifiers.new('Inspection silhouette smoothing', 'SUBSURF')
            modifier.levels = modifier.render_levels = 1
            obj.modifiers.move(obj.modifiers.find(modifier.name), 0)
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
