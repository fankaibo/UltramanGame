"""Verify the low-ash stage capture and encode its continuous comparison."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a179-ground-ash'
BASELINE = '552a7b954ef1655fe3250d157c0ff040b73de390'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true')
    options = parser.parse_args()
    source = 'unity/Assets/Scripts/Runtime/VolcanoStage.cs'
    baseline = subprocess.check_output(['git', 'show', BASELINE + ':' + source], cwd=ROOT)
    assert baseline == (FOLDER / 'before-VolcanoStage.cs').read_bytes()
    for version in ('before', 'after'):
        lines = (FOLDER / version / 'sources.txt').read_text().splitlines()
        for relative in ['Scripts/Runtime/VolcanoStage.cs', 'Scripts/Runtime/GameWorld.cs'] + (['Resources/LowGroundAsh.shader'] if version == 'after' else []):
            digest = next(x.rsplit(' ', 1)[1] for x in lines if x.startswith(relative + ' '))
            path = 'unity/Assets/' + relative
            data = (ROOT / path).read_bytes() if version == 'after' else subprocess.check_output(['git', 'show', BASELINE + ':' + path], cwd=ROOT)
            assert hashlib.sha256(data).hexdigest() == digest, (version, relative)
        assert 'health=50 blocks=1 hurt=0' in (FOLDER / version / 'validation.txt').read_text()
    rows = (FOLDER / 'before/sequence.csv').read_bytes()
    assert rows == (FOLDER / 'after/sequence.csv').read_bytes()
    assert len(rows.splitlines()) == 481
    gpu = (FOLDER / 'gpu/validation.txt').read_text()
    assert gpu.count('repeat=0') == 3 and 'foregroundLeak=0' in gpu
    movie = FOLDER / 'continuous-before-after.mp4'
    if not options.verify_only:
        for version in ('before', 'after'):
            assert len(list((FOLDER / version / 'frames').glob('*.jpg'))) == 240
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.jpg'),
            '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
            '-map', '[v]', '-frames:v', '240', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 240 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, identical_state_rows=480,
        gpu=gpu, frames=240, seconds=8, note='Left baseline / right candidate; offline capture does not establish runtime FPS'), indent=2) + '\n')
    print('PASS source identity, 480 matching battle rows, depth/clock checks; 8-second comparison')


if __name__ == '__main__':
    main()
