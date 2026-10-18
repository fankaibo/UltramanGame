"""Read original Tiga lens nodes without enabling embedded Blender scripts.
Run Blender --background --disable-autoexec --python scripts/inspect_tiga_lens.py.
This is diagnostic extraction; it never saves or edits the source blend.
"""
import hashlib
import json
from pathlib import Path
import bpy

root = Path(__file__).resolve().parent.parent
source = root / 'artifacts/character-sources/Ultraman Tiga.blend'
output = root / 'artifacts/tiga-lens-20261011'
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
material = bpy.data.materials['EYESEMI']
report = {'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'nodes': [],
          'links': [(link.from_node.name, link.from_socket.name, link.to_node.name, link.to_socket.name)
                    for link in material.node_tree.links]}
for node in material.node_tree.nodes:
    item = {'name': node.name, 'type': node.type}
    if node.type == 'VALTORGB':
        item['ramp'] = [(point.position, list(point.color)) for point in node.color_ramp.elements]
        item['interpolation'] = node.color_ramp.interpolation
    if node.type == 'MATH':
        item['operation'] = node.operation
        item['inputs'] = [socket.default_value for socket in node.inputs]
    if node.type == 'GROUP':
        item['group'] = node.node_tree.name
    if node.type in ('FRESNEL', 'LAYER_WEIGHT'):
        item['inputs'] = {socket.name: socket.default_value for socket in node.inputs
                          if isinstance(getattr(socket, 'default_value', None), (int, float))}
    report['nodes'].append(item)
(output / 'source-inspection.json').write_text(json.dumps(report, indent=2) + '\n')
print('Tiga source lens inspected without modification:', len(report['nodes']), 'nodes')
