"""Compare A-182 guarded torso follow, including unchanged combat outcomes."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a182-guard-body'
BASELINE = '1c418c7d05a3a22f73cfc2205c36b50053293e31'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true')
    options = parser.parse_args()
    for name in ('Runtime/RiggedActor.cs', 'Core/LiveReadyPose.cs'):
        old = (FOLDER / ('before-' + Path(name).name)).read_bytes()
        assert old == subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/Scripts/' + name], cwd=ROOT)
        for version in ('before', 'after'):
            data = old if version == 'before' else (ROOT / 'unity/Assets/Scripts' / name).read_bytes()
            assert 'Scripts/' + name + ' ' + hashlib.sha256(data).hexdigest() in (FOLDER / version / 'sources.txt').read_text()
    frames = 0
    for hz in (15, 30, 60):
        assert (FOLDER / f'before/{hz}/rules.csv').read_bytes() == (FOLDER / f'after/{hz}/rules.csv').read_bytes()
        with (FOLDER / f'after/{hz}/rules.csv').open() as stream:
            rows = list(csv.DictReader(stream))
        assert len(rows) == hz * 9
        frames += len(rows)
        with (FOLDER / f'after/{hz}/trace.csv').open() as stream:
            trace = list(csv.DictReader(stream))
        assert max(float(row['footError']) for row in trace) < .035
        assert min(float(row['ground']) for row in trace) > -.055
    transitions = (FOLDER / 'transitions/transitions.txt').read_text().splitlines()
    assert len(transitions) == 15 and all('rangeToMeleeGuard=pass' in row for row in transitions)
    movie = FOLDER / 'continuous-before-after.mp4'
    if not options.verify_only:
        for version in ('before', 'after'):
            assert len(list((FOLDER / version / '60/frames').glob('*.jpg'))) == 270
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/60/frames/%04d.jpg'),
                        '-framerate', '30', '-i', str(FOLDER / 'after/60/frames/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                        '-map', '[v]', '-frames:v', '270', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 270 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, identical_state_rows=frames,
        transition_groups=15, frames=270, seconds=9,
        note='Left baseline / right candidate. Scripted accepted defence and counterattack with synthetic torso input; not recognition accuracy or measured FPS.'), indent=2) + '\n')
    print(f'PASS {frames} identical states, planted feet, 15 transition groups; nine-second comparison')


if __name__ == '__main__':
    main()
