"""Prepare a review-only CMU recovery clip in Unity anatomical coordinates."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from inspect_mocap_bvh import read_bvh


def prepare(source, first, last, stride):
    joints, positions, _, dt = read_bvh(source)
    names = [j['name'] for j in joints]
    if not (0 < first < last < len(positions) and stride > 0):
        raise ValueError('Select motion frames after calibration frame 0, with a positive stride')
    def point(frame, name):
        return positions[frame, names.index(name)]
    toe_forward = sum(point(last, side+'ToeBase')-point(last, side+'Foot') for side in ('Left','Right'))
    if np.linalg.norm(toe_forward[[0,2]]) < 1e-5:
        raise ValueError('Cannot establish the final anatomical forward direction')
    yaw = -np.arctan2(toe_forward[0], toe_forward[2])
    c,s = np.cos(yaw), np.sin(yaw)
    # CMU: left is +X. Unity anatomical coordinates: right is +X.
    conversion = np.diag([-1.,1.,1.]) @ np.array([[c,0,s],[0,1,0],[-s,0,c]])
    origin = point(last, 'Hips').copy();origin[1] = 0
    indices = list(range(first,last+1,stride))
    if indices[-1] != last: indices.append(last)
    samples = (positions[indices]-origin) @ conversion.T
    leg = sum(np.linalg.norm(point(0,a)-point(0,b)) for a,b in [('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot')])
    return dict(joints=names, frames=[dict(positions=x.reshape(-1).round(7).tolist()) for x in samples],
                legLength=float(leg), duration=(last-first)*dt, sourceFirstFrame=first,
                sourceLastFrame=last, sourceFrames=indices, sourceFrameSeconds=dt,
                times=[(f-first)*dt for f in indices],
                sourceSha256=hashlib.sha256(Path(source).read_bytes()).hexdigest(),
                source=f'CMU motion capture {Path(source).stem} / Bruce Hahne BVH conversion',
                sourceUrl='https://mocap.cs.cmu.edu/')


def main():
    parser=argparse.ArgumentParser();parser.add_argument('source',type=Path)
    parser.add_argument('--output',required=True,type=Path)
    parser.add_argument('--first',type=int,default=350);parser.add_argument('--last',type=int,default=780)
    parser.add_argument('--stride',type=int,default=4);args=parser.parse_args()
    capture=prepare(args.source,args.first,args.last,args.stride)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(capture,separators=(',',':')))
    print(f'{len(capture["frames"])} frames, {capture["duration"]:.4f} seconds: {args.output}')


if __name__=='__main__':main()
