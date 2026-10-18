"""Verify identical combat receipts and encode the full ranged-smoke comparison."""
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a171-ranged-smoke'
BASELINE = 'f4f5a42865b46425680121ca473a1549fcbb58cc'


def main():
    source = 'unity/Assets/Scripts/Runtime/RangedSkillAccent.cs'
    before = (FOLDER / 'before-RangedSkillAccent.cs').read_bytes()
    assert before == subprocess.check_output(['git', 'show', BASELINE + ':' + source], cwd=ROOT)
    def trace(version, hz):
        with (FOLDER / version / str(hz) / 'trace.csv').open() as stream:
            return list(csv.DictReader(stream))
    old, new = trace('before', 60), trace('after', 60)
    assert len(old) == len(new) == 240
    for a, b in zip(old, new):
        # The cooling tail intentionally extends visual lifetime, never damage.
        assert {k: v for k, v in a.items() if k != 'visible'} == {k: v for k, v in b.items() if k != 'visible'}
    for hz in (15, 30, 60):
        rows = trace('after', hz)
        assert len(rows) == hz * 4
        assert rows[-1]['hits'] == '3' and rows[-1]['health'] == '47' and rows[-1]['energy'] == '3'
        assert rows[-1]['visible'] == 'False'
        assert f'PASS hz={hz} hits=3 health=47 pool=stable pause=clear' in (FOLDER / 'final-render.log').read_text()
    gpu = (FOLDER / 'gpu/validation.txt').read_text()
    assert gpu.count('repeat=0 expiration=passed') == 3 and 'depthLeak=0 rates=15/30/60 pool=4 objects=stable clear=passed' in gpu
    lifecycle = (FOLDER / 'lifecycle/lifecycle.txt').read_text().splitlines()
    assert len(lifecycle) == 12 and all('heroSwitch=pass' in row and 'beam=pass' in row for row in lifecycle)
    discharge = (FOLDER / 'discharge/gpu/validation.txt').read_text()
    assert 'occlusion=pass' in discharge and 'otherHeroes=restored' in discharge
    for version in ('before', 'after'):
        assert len(list((FOLDER / version / '60').glob('*.jpg'))) == 120
    movie = FOLDER / 'continuous-before-after.mp4'
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/60/%04d.jpg'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/60/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '120', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 120 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, before_sha256=hashlib.sha256(before).hexdigest(),
        after_sha256=hashlib.sha256((ROOT / source).read_bytes()).hexdigest(), identical_state_rows=240,
        rates=[15, 30, 60], lifecycle_cases=12, frames=120, seconds=4,
        note='Left baseline / right candidate; offline sequence is not measured runtime FPS'), indent=2) + '\n')
    print('PASS 240 identical combat rows, three update rates, depth/pooling/lifecycle, four-second comparison')


if __name__ == '__main__':
    main()
