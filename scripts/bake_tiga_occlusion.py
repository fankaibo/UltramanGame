"""Bake short-range costume cavity lighting from the original mesh in Blender.

Run with --background --disable-autoexec --python this_file -- source.blend output.png.
This does not change the source mesh, UVs, color atlas or animations. Only the
BODY material writes to the 2K data map. Other material islands use a scratch
target, so overlapping helmet/trim UVs cannot overwrite the costume atlas.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    original = hashlib.sha256(args.source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(args.source.resolve()), load_ui=False, use_scripts=False)
    body = bpy.data.objects['TigaBody']
    # Do not bake the source IK pose or shadows cast by a posed hand. Keep
    # the same subdivision level as the FBX export, in the mesh rest pose.
    for modifier in body.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.show_viewport = modifier.show_render = False
        elif modifier.type == 'SUBSURF':
            modifier.levels = modifier.render_levels = 1
    for obj in bpy.data.objects:
        obj.hide_render = obj != body
        obj.select_set(False)
    body.hide_set(False)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 32
    scene.cycles.seed = 929
    scene.render.bake.margin = 16
    scene.render.bake.use_selected_to_active = False
    image = bpy.data.images.new('Tiga costume local occlusion', width=2048, height=2048, alpha=False)
    image.generated_color = (1, 1, 1, 1)
    image.colorspace_settings.name = 'Non-Color'
    scratch = bpy.data.images.new('Non-costume UV sink', width=32, height=32, alpha=False)
    distance = .11
    for slot in body.material_slots:
        costume = slot.material.name == 'BODY'
        mat = bpy.data.materials.new('Local cavity '+slot.material.name)
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        nodes.clear()
        ao = nodes.new('ShaderNodeAmbientOcclusion')
        ao.only_local = True
        ao.samples = 64
        ao.inputs['Distance'].default_value = distance
        emission = nodes.new('ShaderNodeEmission')
        out = nodes.new('ShaderNodeOutputMaterial')
        target = nodes.new('ShaderNodeTexImage')
        target.image = image if costume else scratch
        nodes.active = target
        mat.node_tree.links.new(ao.outputs['AO'], emission.inputs['Color'])
        mat.node_tree.links.new(emission.outputs[0], out.inputs['Surface'])
        slot.material = mat
    bpy.ops.object.bake(type='EMIT', use_clear=False)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(args.output.resolve())
    image.file_format = 'PNG'
    image.save()
    assert original == hashlib.sha256(args.source.read_bytes()).hexdigest(), 'Source was modified'
    record = dict(source_sha256=original, blender=bpy.app.version_string, resolution=[2048, 2048],
                  output_sha256=hashlib.sha256(args.output.read_bytes()).hexdigest(), distance=distance,
                  samples=32, ao_samples=64, rest_pose=True, only_local=True, material='BODY')
    args.output.with_suffix('.json').write_text(json.dumps(record, indent=2)+'\n')
    print(json.dumps(record))


if __name__ == '__main__':
    main()
