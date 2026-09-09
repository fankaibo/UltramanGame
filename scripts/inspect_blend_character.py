"""Inspect a character without running scripts embedded in its Blender file.

Run with Blender --background --factory-startup --disable-autoexec --python
scripts/inspect_blend_character.py -- INPUT.blend OUTPUT.json
"""
import json
import sys
from pathlib import Path

import bpy


def vec(value):
    return [round(float(v), 6) for v in value]


def main():
    source, destination = map(Path, sys.argv[sys.argv.index("--") + 1:])
    bpy.ops.wm.open_mainfile(filepath=str(source.resolve()), load_ui=False, use_scripts=False)
    report = {"source": source.name, "blender": bpy.app.version_string,
              "units": bpy.context.scene.unit_settings.scale_length,
              "objects": [], "materials": [], "images": [], "actions": []}
    for obj in bpy.data.objects:
        row = {"name": obj.name, "type": obj.type,
               "parent": obj.parent.name if obj.parent else None,
               "location": vec(obj.location), "rotation": vec(obj.rotation_euler),
               "scale": vec(obj.scale), "dimensions": vec(obj.dimensions),
               "hidden": obj.hide_render, "modifiers": []}
        for mod in obj.modifiers:
            row["modifiers"].append({"name": mod.name, "type": mod.type,
                                     "target": getattr(getattr(mod, "object", None), "name", None)})
        if obj.type == "MESH":
            row.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons),
                       materials=[m.name if m else None for m in obj.data.materials],
                       vertex_groups=[g.name for g in obj.vertex_groups],
                       shape_keys=list(obj.data.shape_keys.key_blocks.keys()) if obj.data.shape_keys else [])
        elif obj.type == "ARMATURE":
            row["bones"] = [{"name": b.name, "parent": b.parent.name if b.parent else None,
                              "head": vec(b.head_local), "tail": vec(b.tail_local),
                              "deform": b.use_deform,
                              "constraints": [{"type": c.type, "name": c.name} for c in obj.pose.bones[b.name].constraints]}
                             for b in obj.data.bones]
        report["objects"].append(row)
    for mat in bpy.data.materials:
        row = {"name": mat.name, "color": vec(mat.diffuse_color), "nodes": []}
        if mat.use_nodes:
            for node in mat.node_tree.nodes:
                inputs = {}
                for socket in node.inputs:
                    if hasattr(socket, "default_value"):
                        value = socket.default_value
                        inputs[socket.name] = value if isinstance(value, (str, int, float, bool)) else vec(value) if hasattr(value, "__iter__") else str(value)
                row["nodes"].append({"name": node.name, "type": node.type, "inputs": inputs,
                                     "image": getattr(getattr(node, "image", None), "name", None)})
            row["links"] = [{"from": f"{l.from_node.name}:{l.from_socket.name}",
                              "to": f"{l.to_node.name}:{l.to_socket.name}"} for l in mat.node_tree.links]
        report["materials"].append(row)
    report["images"] = [{"name": im.name, "size": list(im.size), "packed": bool(im.packed_file),
                          "filepath": im.filepath} for im in bpy.data.images]
    report["actions"] = [{"name": a.name, "frame_range": vec(a.frame_range)} for a in bpy.data.actions]
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Character report: {destination}; objects={len(report['objects'])}; actions={len(report['actions'])}")


if __name__ == "__main__":
    main()
