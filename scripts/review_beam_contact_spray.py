"""Verify and encode A-181's complete, identical-input finisher comparison."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a181-beam-contact'
BASELINE = '437486bcb3ec6f9e0a854b6c9f5d251cb7952e3d'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true')
    options = parser.parse_args()
    changed = ['Scripts/Runtime/BeamImpactVolume.cs', 'Resources/BeamImpactVolume.shader', 'Scripts/Runtime/CombatVfx.cs']
    added = ['Scripts/Runtime/BeamContactSpray.cs', 'Resources/BeamContactSpray.shader']
    sources = {}
    for name in changed:
        before = (FOLDER / ('before-' + Path(name).name)).read_bytes()
        assert before == subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/' + name], cwd=ROOT)
        sources[name] = {'before': hashlib.sha256(before).hexdigest()}
    after_manifest = (FOLDER / 'after/render-source.txt').read_text().splitlines()
    for name in changed + added:
        digest = hashlib.sha256((ROOT / 'unity/Assets' / name).read_bytes()).hexdigest()
        assert name + ' ' + digest in after_manifest, name
        sources.setdefault(name, {})['after'] = digest
    sequences = []
    for version in ('before', 'after'):
        with (FOLDER / version / 'sequence.csv').open() as stream:
            sequences.append(list(csv.DictReader(stream)))
        if not options.verify_only:
            assert len(list((FOLDER / version / 'frames').glob('*.png'))) == 126
        assert 'contacts=1 releases=1 health=26' in (FOLDER / version / 'validation.txt').read_text()
    assert len(sequences[0]) == len(sequences[1]) == 252
    assert sequences[0] == sequences[1], 'Gameplay, beam origin or visibility clock changed'
    gpu = (FOLDER / 'gpu/validation.txt').read_text()
    assert gpu.count('depthLeak=0 expired=passed') == 4 and gpu.count('frozen=0') == 4
    assert gpu.count('vertical=passed reset=passed') == 3
    flow = (FOLDER / 'after/flow-validation.txt').read_text().splitlines()
    assert len(flow) == 10 and all('passed' in row for row in flow)
    movie = FOLDER / 'continuous-before-after.mp4'
    if not options.verify_only:
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                        '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                        '-map', '[v]', '-frames:v', '126', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 126 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, sources=sources, identical_state_rows=252,
        gpu_angles=[0, 45, 90, 135], flow_cases=10, frames=126, seconds=4.2,
        note='Left baseline / right candidate; offline capture is not measured runtime FPS'), indent=2) + '\n')
    print('PASS 252 identical states/origins, four GPU views, three frame rates, ten lifecycle cases; 4.2-second comparison')


if __name__ == '__main__':
    main()
