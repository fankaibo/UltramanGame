"""Create small UV-preserving fragments from the project's CC0 rock scans.

Run with Blender --background --factory-startup --disable-autoexec --python
scripts/bake_ground_debris.py. Only the two generated FBX files are replaced.
"""
import hashlib
import json
from pathlib import Path

import bpy
from mathutils import Vector


root = Path(__file__).resolve().parents[1]
source_dir = root / 'unity/Assets/Resources/Environment/ScannedRocks'
output_dir = root / 'unity/Assets/Resources/Environment/GroundDebris'
output_dir.mkdir(parents=True, exist_ok=True)
records = []
for name in ('rock_07', 'rock_09'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = source_dir / (name + '_2k.fbx')
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False)
    candidates = [o for o in bpy.data.objects if o.type == 'MESH' and name in o.name]
    scan = min(candidates, key=lambda o: len(o.data.polygons))
    original_faces = len(scan.data.polygons)
    bpy.ops.object.select_all(action='DESELECT')
    scan.select_set(True)
    bpy.context.view_layer.objects.active = scan
    scan.parent = None
    scan.matrix_world.identity()
    # Preserve original UVs so the existing diffuse/normal/ARM maps still fit.
    decimate = scan.modifiers.new('Bounded ground fragment', 'DECIMATE')
    decimate.ratio = min(1, 320 / original_faces)
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    triangulate = scan.modifiers.new('Triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    lo = Vector(tuple(min(v.co[i] for v in scan.data.vertices) for i in range(3)))
    hi = Vector(tuple(max(v.co[i] for v in scan.data.vertices) for i in range(3)))
    centre = (lo + hi) / 2
    radius = max((v.co - centre).length for v in scan.data.vertices)
    for vertex in scan.data.vertices:
        vertex.co = (vertex.co - centre) * (.90 / radius)
    scan.data.update()
    scan.name = name + '_ground_fragment'
    scan.data.name = scan.name
    assert 200 <= len(scan.data.polygons) <= 340
    assert scan.data.uv_layers.active is not None
    # FBX carries only the mesh; textures remain shared with stage boulders.
    scan.data.materials.clear()
    output = output_dir / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True,
                            object_types={'MESH'}, bake_anim=False,
                            add_leaf_bones=False, use_mesh_modifiers=True,
                            axis_forward='-Z', axis_up='Y', global_scale=1)
    records.append(dict(name=name, source=str(source.relative_to(root)),
                        source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                        output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
                        triangles=len(scan.data.polygons), vertices=len(scan.data.vertices),
                        source_lod_triangles=original_faces,
                        source_url='https://polyhaven.com/a/' + name,
                        licence='CC0-1.0', author='Jenelle van Heerden'))
manifest = dict(blender=bpy.app.version_string, script='scripts/bake_ground_debris.py',
                note='Derived geometry; original UVs and shared scan textures retained.', assets=records)
(output_dir / 'sources.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps(manifest, indent=2))
