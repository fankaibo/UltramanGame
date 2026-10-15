"""Decode authored Source texture data; no synthesis or model/UV editing.

Run using Blender --background --disable-autoexec --python this.py -- --output DIR.
SourceIO's normal conversion and Unity both use Y+ tangent-space normals;
the original Source Y- map needs exactly one green-channel inversion.
"""
import argparse
import hashlib
import json
import os
import sys
from pathlib import Path

import bpy
import numpy as np


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    os.environ['NO_BPY'] = '1'
    sys.path.insert(0, str(Path('.cache/character-tools').resolve()))
    from SourceIO.library.source1.vtf import load_texture
    from SourceIO.library.utils import FileBuffer
    from SourceIO.library.utils.tiny_path import TinyPath

    source = Path('artifacts/character-sources/roster/Zero/Ultimate_Zero/materials/models/ultimo/Ultraman_Zero')
    args.output.mkdir(parents=True, exist_ok=True)
    records = []
    for suffix, name in [('nm', 'ZeroBodyNormal'), ('spec', 'ZeroBodySpecular')]:
        path = source / f'Ultraman_Zero_{suffix}.vtf'
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        with FileBuffer(TinyPath(str(path.resolve()))) as stream:
            pixels, height, width = load_texture(stream)
        if pixels is None or not np.isfinite(pixels).all():
            raise ValueError(f'Invalid Source pixels: {path}')
        pixels = pixels.copy()
        if suffix == 'nm':
            pixels[:, :, 1] = 1 - pixels[:, :, 1]
        image = bpy.data.images.new(name, width=width, height=height, alpha=True)
        image.colorspace_settings.name = 'Non-Color'
        image.pixels.foreach_set(pixels[::-1].ravel())
        image.update()
        target = args.output / f'{name}.png'
        image.filepath_raw = str(target.resolve())
        image.file_format = 'PNG'
        image.save()
        assert hashlib.sha256(path.read_bytes()).hexdigest() == digest
        records.append(dict(source=str(path), source_sha256=digest, output=target.name,
                            output_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                            size=[width, height], green_inverted=suffix == 'nm',
                            channels_min=pixels.min(axis=(0, 1)).tolist(),
                            channels_max=pixels.max(axis=(0, 1)).tolist(),
                            channels_mean=pixels.mean(axis=(0, 1), dtype=np.float64).tolist()))
    record = dict(source_url='https://sfmlab.com/project/0068ba82-4091-460a-a601-73ff6c5700ba/',
                  source_author='TengenGenesic / ultimo; enhancements credited to Perceptor',
                  blender=bpy.app.version_string, maps=records,
                  notes='Source texture decoding only. Existing UVs, albedo, FBX and rig remain unchanged.')
    (args.output / 'source.json').write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record), flush=True)


if __name__ == '__main__':
    main()
