"""Read BVH joint trajectories and render a source-motion contact sheet.

Input is numeric motion data only; no embedded scripts are executed.
"""
import argparse
import json
import re
from pathlib import Path

import numpy as np


def read_bvh(path):
    hierarchy, motion = Path(path).read_text().split('MOTION', 1)
    tokens = iter(re.findall(r'[^\s]+', hierarchy))
    assert next(tokens) == 'HIERARCHY'
    joints = []
    channel_count = 0

    def joint(parent, kind):
        nonlocal channel_count
        name = next(tokens)
        if kind == 'End':
            assert name == 'Site'
            name = joints[parent]['name'] + 'End'
        index = len(joints)
        entry = dict(name=name, parent=parent, offset=None, channels=[], first=channel_count)
        joints.append(entry)
        assert next(tokens) == '{'
        while True:
            token = next(tokens)
            if token == '}':
                break
            if token == 'OFFSET':
                entry['offset'] = [float(next(tokens)) for _ in range(3)]
            elif token == 'CHANNELS':
                count = int(next(tokens));entry['channels'] = [next(tokens) for _ in range(count)]
                entry['first'] = channel_count;channel_count += count
            elif token in ('JOINT', 'End'):
                joint(index, token)
            else:
                raise ValueError('Unexpected BVH token: ' + token)
        return index

    assert next(tokens) == 'ROOT'
    joint(-1, 'ROOT')
    lines = motion.strip().splitlines()
    frames = int(lines[0].split(':')[1]);dt = float(lines[1].split(':')[1])
    if frames < 1 or not np.isfinite(dt) or dt <= 0:
        raise ValueError('BVH must have motion frames and a positive finite frame interval')
    values = np.array([[float(n) for n in line.split()] for line in lines[2:] if line.strip()])
    if values.shape != (frames, channel_count) or not np.isfinite(values).all():
        raise ValueError('BVH frame/channel count mismatch or nonfinite motion')
    positions = np.zeros((frames, len(joints), 3))
    rotations = np.zeros((frames, len(joints), 3, 3))
    identity = np.broadcast_to(np.eye(3), (frames, 3, 3)).copy()
    for index, entry in enumerate(joints):
        matrix = identity.copy()
        position = np.broadcast_to(entry['offset'], (frames, 3)).copy()
        for column, channel in enumerate(entry['channels']):
            axis = 'XYZ'.index(channel[0]);data = values[:, entry['first'] + column]
            if channel.endswith('position'):
                position[:, axis] += data
            else:
                angle = np.radians(data);c = np.cos(angle);s = np.sin(angle)
                rotation = identity.copy();a, b = (axis + 1) % 3, (axis + 2) % 3
                rotation[:, a, a] = rotation[:, b, b] = c
                rotation[:, a, b] = -s;rotation[:, b, a] = s
                matrix = matrix @ rotation
        parent = entry['parent']
        if parent < 0:
            positions[:, index] = position;rotations[:, index] = matrix
        else:
            positions[:, index] = positions[:, parent] + np.einsum('fij,fj->fi', rotations[:, parent], position)
            rotations[:, index] = rotations[:, parent] @ matrix
    return joints, positions, rotations, dt


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    joints, positions, rotations, dt = read_bvh(args.source)
    args.output.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(args.output/'motion.npz', positions=positions, rotations=rotations, dt=dt,
                        names=[j['name'] for j in joints], parents=[j['parent'] for j in joints])
    report = dict(source=str(args.source), frames=len(positions), seconds=(len(positions)-1)*dt,
                  dt=dt, joints=joints, hips_height_min=float(positions[1:, 0, 1].min()),
                  hips_height_max=float(positions[1:, 0, 1].max()))
    (args.output/'source.json').write_text(json.dumps(report, indent=2))
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig = plt.figure(figsize=(18, 12), facecolor='#141d28')
    chosen = np.linspace(1, len(positions)-1, 20).astype(int)
    points = positions[1:];bounds = np.stack([points.min((0, 1)), points.max((0, 1))]);centre = bounds.mean(0)
    radius = np.max(bounds[1]-bounds[0])*.53
    for panel, frame in enumerate(chosen):
        ax = fig.add_subplot(4, 5, panel+1, projection='3d');ax.set_facecolor('#141d28')
        for index, entry in enumerate(joints):
            if entry['parent'] < 0 or any(x in entry['name'] for x in ('Finger', 'Thumb')):
                continue
            line = positions[frame, [entry['parent'], index]]
            colour = '#6ad5f5' if 'Left' in entry['name'] or entry['name'].startswith('LHip') else '#ffbd68'
            ax.plot(line[:, 0], line[:, 2], line[:, 1], color=colour, lw=2)
        ax.set_xlim(centre[0]-radius, centre[0]+radius);ax.set_ylim(centre[2]-radius, centre[2]+radius)
        ax.set_zlim(0, radius*2);ax.view_init(elev=12, azim=-55);ax.set_axis_off()
        ax.set_title(f'{frame*dt:.2f}s / frame {frame}', color='white', fontsize=10)
    fig.tight_layout();fig.savefig(args.output/'contact-sheet.png', dpi=100);plt.close(fig)
    print(json.dumps({k:v for k,v in report.items() if k!='joints'}))


if __name__ == '__main__':
    main()
