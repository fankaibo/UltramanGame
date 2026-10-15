"""Read the original Tiga hand rig in Blender, with embedded scripts disabled."""
import hashlib
import json
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[1]
source = root/'artifacts/character-sources/Ultraman Tiga.blend'
output = root/'artifacts/fist-shape-20261012'
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
rig = bpy.data.objects['Armature.001']
report = {'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'bones': [], 'weights': {}}
for bone in rig.pose.bones:
    if not any(key in bone.name for key in ('Hand', 'Finger', 'finger', 'Index', 'Mid', 'Ring', 'Pinky', 'Thumb', 'Carpal')):
        continue
    constraints = []
    for c in bone.constraints:
        row = {'name': c.name, 'type': c.type, 'influence': c.influence}
        for key in ('subtarget', 'chain_count', 'use_tail', 'use_stretch', 'target_space', 'owner_space'):
            if hasattr(c, key):
                row[key] = getattr(c, key)
        constraints.append(row)
    report['bones'].append({'name': bone.name, 'parent': bone.parent.name if bone.parent else None,
                            'deform': bone.bone.use_deform, 'head': list(bone.head), 'tail': list(bone.tail),
                            'location': list(bone.location), 'constraints': constraints})
mesh = bpy.data.objects['TigaBody']
report['shape_keys'] = [key.name for key in mesh.data.shape_keys.key_blocks] if mesh.data.shape_keys else []
report['shape_key_regions'] = {}
if mesh.data.shape_keys:
    basis = mesh.data.shape_keys.key_blocks[0]
    for key in list(mesh.data.shape_keys.key_blocks)[1:]:
        affected = {}
        maximum = 0
        count = 0
        for vertex, base, changed in zip(mesh.data.vertices, basis.data, key.data):
            distance = (changed.co-base.co).length
            if distance < .00001:
                continue
            count += 1
            maximum = max(maximum, distance)
            if vertex.groups:
                group = max(vertex.groups, key=lambda item: item.weight)
                name = mesh.vertex_groups[group.group].name
                affected[name] = affected.get(name, 0)+1
        report['shape_key_regions'][key.name] = {'value': key.value, 'changed_vertices': count, 'max_delta': maximum,
                                                 'dominant_bones': affected}
report['modifiers'] = [{'name': modifier.name, 'type': modifier.type,
                        'preserve_volume': getattr(modifier, 'use_deform_preserve_volume', None)}
                       for modifier in mesh.modifiers]
report['drivers'] = []
for owner in (rig, rig.data, mesh, mesh.data.shape_keys):
    animation = getattr(owner, 'animation_data', None)
    if animation:
        for curve in animation.drivers:
            report['drivers'].append({'owner': owner.name, 'path': curve.data_path,
                                       'expression': curve.driver.expression,
                                       'targets': [{'bone': target.bone_target, 'path': target.data_path,
                                                    'transform': target.transform_type, 'space': target.transform_space}
                                                   for variable in curve.driver.variables for target in variable.targets]})
for group in mesh.vertex_groups:
    if any(key in group.name for key in ('Hand', 'Index', 'Mid', 'Ring', 'Pinky', 'Thumb', 'Carpal')):
        weights = [g.weight for v in mesh.data.vertices for g in v.groups if g.group == group.index]
        report['weights'][group.name] = {'vertices': len(weights), 'sum': sum(weights)}
(output/'source-rig.json').write_text(json.dumps(report, indent=2)+'\n')
print('Tiga hand rig inspected without modifying source:', len(report['bones']), 'bones')
