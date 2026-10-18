"""Verify actual skinned Golza recovery-step balance and preserved foot targets."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '3ca1034b575ba3f0c8ace3f55fa8d545dd86f928'
FOLDER = ROOT / 'artifacts/a187-ranged-balance'


def read(path):
    with path.open() as stream:
        return list(csv.DictReader(stream))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    for name in ('Runtime/RiggedActor.cs', 'Core/MonsterStaggerMotion.cs'):
        old = (FOLDER / ('before-' + Path(name).name)).read_bytes()
        assert old == subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/Scripts/' + name], cwd=ROOT)
        for version in ('before', 'after'):
            data = old if version == 'before' else (ROOT / 'unity/Assets/Scripts' / name).read_bytes()
            assert name + ' ' + hashlib.sha256(data).hexdigest() in (FOLDER / version / 'sources.txt').read_text()
    assert (FOLDER / 'before/gameplay.csv').read_bytes() == (FOLDER / 'after/gameplay.csv').read_bytes()
    measurements = []
    for hz in (15, 30, 60):
        before, after = (read(FOLDER / version / str(hz) / 'motion.csv') for version in ('before', 'after'))
        assert len(before) == len(after) == hz * 8
        root = []
        feet = 0
        settled = 0
        for a, b in zip(before, after):
            for k in ('frame', 'punches', 'action', 'shield', 'side', 'stepAge', 'stepLeft'):
                assert a[k] == b[k], (hz, k)
            shift = np.array([float(b['root'+c])-float(a['root'+c]) for c in 'XYZ'])
            horizontal = float(np.linalg.norm(shift[[0, 2]]))
            if float(b['stepAge']) < .88:
                root.append(horizontal)
            else:
                settled = max(settled, horizontal)
            feet = max(feet, *(abs(float(a[k])-float(b[k])) for k in ('leftX','leftY','leftZ','rightX','rightY','rightZ')))
        assert .09 < max(root) <= .121, (hz, max(root))
        assert feet < .0001 and settled < .0001, (hz, feet, settled)
        measurements.append(dict(hz=hz, frames=len(before), max_lateral_carry=max(root), max_foot_target_difference=feet, inactive_root_difference=settled))
    lifecycle = (FOLDER / 'lifecycle/validation.txt').read_text().splitlines()
    assert len(lifecycle) == 18 and all('active=False' in s and 'drift=0.000000' in s for s in lifecycle)
    report = dict(baseline=BASELINE, gameplay_rows=840, gameplay='identical', measurements=measurements, lifecycle_groups=18,
        limitations='Synthetic state-driven Unity rendering; lateral carry is scene units, not a child accuracy or animation quality score.')
    (FOLDER / 'comparison.json').write_text(json.dumps(report, indent=2)+'\n')
    if not args.verify_only:
        for version in ('before','after'):
            assert len(list((FOLDER/version/'60/frames').glob('*.jpg'))) == 240
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y',
            '-framerate','30','-i',str(FOLDER/'before/60/frames/%04d.jpg'),
            '-framerate','30','-i',str(FOLDER/'after/60/frames/%04d.jpg'),
            '-filter_complex','[0:v][1:v]hstack=inputs=2[v]','-map','[v]',
            '-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'continuous-before-after.mp4')],check=True)
        with Image.open(FOLDER/'before/60/step-3.png') as a, Image.open(FOLDER/'after/60/step-3.png') as b:
            sheet=Image.new('RGB',(a.width*2,a.height));sheet.paste(a,(0,0));sheet.paste(b,(a.width,0));sheet.save(FOLDER/'balance-before-after.jpg',quality=92)
    print(json.dumps(report,indent=2))


if __name__ == '__main__':
    main()
