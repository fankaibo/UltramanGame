"""Compare identical-input guard warning, contact and recovery, then encode it."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a174-shield-readiness'
BASELINE = '55ac94b2582a4cbf751f5783e45b62d7e25a4bf4'


def main():
    sources = [('unity/Assets/Scripts/Runtime/CombatVfx.cs', 'before-CombatVfx.cs'),
               ('unity/Assets/Resources/EnergyShield.shader', 'before-EnergyShield.shader')]
    hashes = {}
    for source, saved in sources:
        old = (FOLDER / saved).read_bytes()
        assert old == subprocess.check_output(['git', 'show', BASELINE + ':' + source], cwd=ROOT)
        hashes[source] = {'before': hashlib.sha256(old).hexdigest(), 'after': hashlib.sha256((ROOT / source).read_bytes()).hexdigest()}
    old = (FOLDER / 'before/state-camera.csv').read_bytes()
    assert old == (FOLDER / 'after/state-camera.csv').read_bytes() and len(old.splitlines()) == 630
    for version in ('before', 'after'):
        report = (FOLDER / version / 'validation.txt').read_text()
        assert report.count('block=1 hurt=0') == 3 and report.count('pause=clear newRound=clear') == 3
        assert len(list((FOLDER / version / 'frames').glob('*.jpg'))) == 180
    gpu = (FOLDER / 'gpu/validation.txt').read_text()
    assert gpu.count('fullError=0 hitError=0 freeze=0 expiry=0') == 3
    movie = FOLDER / 'continuous-before-after.mp4'
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.jpg'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '180', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 180 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, sources=hashes, identical_state_rows=630,
        frames=180, seconds=6, note='Left old / right new; same-input offline capture, not measured runtime FPS'), indent=2) + '\n')
    print('PASS 630 state/camera rows, three update rates and three GPU views; six-second comparison')


if __name__ == '__main__':
    main()
