"""Compare the full Tiga finisher, using the actual pre-change beam source."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a178-beam-strands'
BASELINE = '32b2f23fd42091d598ac8a4213ab23e221b27dd0'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify-only", action="store_true", help="Verify retained evidence without regenerating the video")
    options = parser.parse_args()
    source = 'unity/Assets/Resources/ZeperionVolume.shader'
    old = (FOLDER / 'before-ZeperionVolume.shader').read_bytes()
    assert old == subprocess.check_output(['git', 'show', BASELINE + ':' + source], cwd=ROOT)
    for version in ('before', 'after'):
        line = next(x for x in (FOLDER / version / 'render-source.txt').read_text().splitlines() if x.startswith('Resources/ZeperionVolume.shader '))
        actual = old if version == 'before' else (ROOT / source).read_bytes()
        assert line.split()[-1] == hashlib.sha256(actual).hexdigest(), version
    sequences = []
    for version in ('before', 'after'):
        with (FOLDER / version / 'sequence.csv').open() as stream:
            sequences.append(list(csv.DictReader(stream)))
        if not options.verify_only:
            assert len(list((FOLDER / version / 'frames').glob('*.png'))) == 126
        assert 'contacts=1 releases=1 health=26' in (FOLDER / version / 'validation.txt').read_text()
    assert len(sequences[0]) == len(sequences[1]) == 252
    assert sequences[0] == sequences[1], 'Gameplay, emission origin or visibility clock changed'
    gpu = (FOLDER / 'gpu/validation.txt').read_text()
    assert gpu.count('endpoints=passed clear=passed') == 5 and 'depthLeak=0' in gpu
    flow = (FOLDER / 'after/flow-validation.txt').read_text().splitlines()
    assert len(flow) == 10 and all('passed' in row for row in flow)
    movie = FOLDER / 'continuous-before-after.mp4'
    if not options.verify_only:
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                        '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                        '-map', '[v]', '-frames:v', '126', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 126 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, before_sha256=hashlib.sha256(old).hexdigest(),
        after_sha256=hashlib.sha256((ROOT / source).read_bytes()).hexdigest(), identical_state_rows=252,
        gpu_angles=[0, 45, 75, 90, 135], flow_cases=10, frames=126, seconds=4.2,
        note='Left baseline / right candidate; offline capture is not measured runtime FPS'), indent=2) + '\n')
    print('PASS 252 identical states/origins, five GPU views, ten finisher lifecycle cases; 4.2-second comparison')


if __name__ == '__main__':
    main()
