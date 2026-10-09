"""Bake local rest-pose occlusion from existing FBX geometry, per material.

Blender --background --disable-autoexec --python scripts/bake_hero_cavity.py --
  --hero Zero --output artifacts/hero-cavity-20261009/bake/Zero
No source model, animation, UV or color texture is rewritten.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--hero', choices=('Zero', 'Mebius', 'Grigio'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source = Path('unity/Assets/Resources/Characters') / args.hero / (args.hero + '.fbx')
    digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    original = digest(source)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source.resolve()), use_anim=False)
    for obj in bpy.data.objects:
        if obj.type == 'ARMATURE':
            obj.animation_data_clear()
            obj.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    height = max(p.z for p in points) - min(p.z for p in points)
    if height <= 0:
        raise RuntimeError('Invalid imported model height')
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 32
    scene.cycles.seed = 1009
    scene.render.bake.margin = 12
    scene.render.bake.use_selected_to_active = False
    outputs = {}
    distance = height * .025
    scratch = bpy.data.images.new('Emissive material sink', 32, 32, alpha=False)
    for material in list(bpy.data.materials):
        key = material.name
        emissive = any(token in key.lower() for token in ('eyes', 'colortimer', 'crystal'))
        material.use_nodes = True
        nodes = material.node_tree.nodes
        nodes.clear()
        ao = nodes.new('ShaderNodeAmbientOcclusion')
        ao.only_local = True
        ao.samples = 64
        ao.inputs['Distance'].default_value = distance
        emission = nodes.new('ShaderNodeEmission')
        out = nodes.new('ShaderNodeOutputMaterial')
        target = nodes.new('ShaderNodeTexImage')
        if emissive:
            target.image = scratch
        else:
            image = bpy.data.images.new(key + ' local cavity', 1024, 1024, alpha=False)
            image.generated_color = (1, 1, 1, 1)
            image.colorspace_settings.name = 'Non-Color'
            target.image = outputs[key] = image
        nodes.active = target
        material.node_tree.links.new(ao.outputs['AO'], emission.inputs['Color'])
        material.node_tree.links.new(emission.outputs[0], out.inputs['Surface'])
    bpy.ops.object.select_all(action='DESELECT')
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.bake(type='EMIT', use_clear=False)
    args.output.mkdir(parents=True, exist_ok=True)
    maps = []
    for name, image in outputs.items():
        path = args.output / (name + '.png')
        image.filepath_raw = str(path.resolve())
        image.file_format = 'PNG'
        image.save()
        maps.append(dict(material=name, file=path.name, sha256=digest(path)))
    if digest(source) != original:
        raise RuntimeError('Source FBX changed during bake')
    record = dict(character=args.hero, source=str(source), source_sha256=original,
                  blender=bpy.app.version_string, resolution=[1024, 1024],
                  model_height=height, distance=distance, rest_pose=True,
                  only_local=True, samples=32, ao_samples=64, maps=maps)
    (args.output / 'bake.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record), flush=True)


if __name__ == '__main__':
    main()
