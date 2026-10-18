"""Verify A183 source provenance and unchanged slam rules; encode continuous comparison."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a183-slam-dust'
BASELINE = '414dd0d3bfb152a2078bacadcbb97531d9a736f6'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true')
    parser.add_argument('--baseline-video', type=Path, help='Use the preserved baseline left half after intermediate JPEG cleanup')
    args = parser.parse_args()
    old = (FOLDER / 'before-GroundImpact.cs').read_bytes()
    assert old == subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/Scripts/Runtime/GroundImpact.cs'], cwd=ROOT)
    for version in ('before', 'after'):
        for entry in (FOLDER / version / 'sources.txt').read_text().splitlines():
            name, digest = entry.split()
            data = old if version == 'before' and name.endswith('GroundImpact.cs') else (ROOT / 'unity/Assets' / name).read_bytes()
            assert hashlib.sha256(data).hexdigest() == digest, (version, name)
    count = 0
    for hz in (15, 30, 60):
        for mode in ('guard', 'hurt'):
            name = f'{hz}-{mode}.csv'
            assert (FOLDER / 'before' / name).read_bytes() == (FOLDER / 'after' / name).read_bytes(), name
            with (FOLDER / 'after' / name).open() as stream:
                rows = list(csv.DictReader(stream))
            assert len(rows) == hz * 6
            count += len(rows)
    movie = FOLDER / 'continuous-before-after.mp4'
    evidence = {}
    if not args.verify_only:
        assert len(list((FOLDER / 'after/frames').glob('*.jpg'))) == 360
        if args.baseline_video:
            baseline_video = args.baseline_video.resolve()
            probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(baseline_video)], text=True))['streams'][0]
            assert int(probe['nb_frames']) == 360 and probe['width'] == 2560 and probe['height'] == 720
            baseline_input = ['-i', str(baseline_video)]
            filter_graph = '[0:v]crop=1280:720:0:0[old];[old][1:v]hstack=inputs=2[v]'
            evidence = dict(baseline_video=str(baseline_video.relative_to(ROOT)), baseline_video_sha256=hashlib.sha256(baseline_video.read_bytes()).hexdigest())
        else:
            assert len(list((FOLDER / 'before/frames').glob('*.jpg'))) == 360
            baseline_input = ['-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.jpg')]
            filter_graph = '[0:v][1:v]hstack=inputs=2[v]'
        subprocess.run(['ffmpeg', '-y', '-v', 'error'] + baseline_input +
                       ['-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.jpg'), '-filter_complex', filter_graph,
                        '-map', '[v]', '-frames:v', '360', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    else:
        prior = json.loads((FOLDER / 'comparison.json').read_text())
        if 'baseline_video' in prior:
            evidence = {key: prior[key] for key in ('baseline_video', 'baseline_video_sha256')}
            assert hashlib.sha256((ROOT / evidence['baseline_video']).read_bytes()).hexdigest() == evidence['baseline_video_sha256']
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 360 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(**evidence, baseline=BASELINE, identical_state_rows=count, frames=360, seconds=12,
        note='Left baseline / right candidate; accepted synthetic guard then unguarded slam. No claim of recognition accuracy or measured FPS.'), indent=2) + '\n')
    print(f'PASS {count} identical states/contact events; twelve-second comparison')


if __name__ == '__main__':
    main()
