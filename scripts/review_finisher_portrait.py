"""Verify same-input finisher framing evidence and encode the paired sequence."""
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a168-finisher-framing'
BASELINE = 'ea3084ef01737de19f342bf4d7cfb320cde5f523'


def main():
    sources = {}
    for version in ('before', 'after'):
        sources[version] = {}
        for row in (FOLDER / version / 'render-source.txt').read_text().splitlines():
            if row.startswith(('UTC:', 'Unity:')):
                continue
            file, digest = row.rsplit(' ', 1)
            path = 'unity/Assets/' + file
            data = (subprocess.check_output(['git', 'show', BASELINE + ':' + path], cwd=ROOT)
                    if version == 'before' else (ROOT / path).read_bytes())
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale render: ' + path)
            sources[version][path] = digest
        if len(list((FOLDER / version / 'frames').glob('*.png'))) != 126:
            raise RuntimeError('Incomplete continuous sequence')
    sequence = (FOLDER / 'before/sequence.csv').read_bytes()
    if sequence != (FOLDER / 'after/sequence.csv').read_bytes():
        raise RuntimeError('Framing changed gameplay or beam origin')
    for hero in ('Mebius', 'Zero', 'Geed', 'Grigio'):
        file = hero + '-60.csv'
        if (FOLDER / 'portrait/before' / file).read_bytes() != (FOLDER / 'portrait/after' / file).read_bytes():
            raise RuntimeError('Another hero changed: ' + hero)
    rows = 0
    for rate in (15, 30, 60):
        traces = [list(csv.DictReader((FOLDER / f'portrait/{v}/Tiga-{rate}.csv').open())) for v in ('before', 'after')]
        if len(traces[0]) != len(traces[1]):
            raise RuntimeError('Missing portrait sample')
        for before, after in zip(*traces):
            for key in ('frame', 'closeup', 'health', 'energy', 'actionAge'):
                if before[key] != after[key]:
                    raise RuntimeError('Portrait changed timing: ' + key)
            # Outside the dedicated shot the original camera must be identical.
            if before['closeup'] == 'False':
                for key in ('x', 'y', 'z', 'rx', 'ry', 'rz', 'rw', 'fov'):
                    if before[key] != after[key]:
                        raise RuntimeError('Changed camera outside cut-in: ' + key)
            rows += 1
    validation = (FOLDER / 'portrait/after/validation.txt').read_text()
    for row in validation.splitlines():
        if row.startswith('Tiga-') and 'clippedFrames=0 ' not in row:
            raise RuntimeError('Helmet or hand cropped')
    flow = (FOLDER / 'after/flow-validation.txt').read_text()
    if flow.count('framing/coverage/zero-time/return=passed') != 7 or flow.count('pause-and-reset=passed') != 3:
        raise RuntimeError('Incomplete finisher lifecycle')
    movie = FOLDER / 'continuous-before-after.mp4'
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER/'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER/'after/frames/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '126', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    if int(media['nb_frames']) != 126 or media['width'] != 2560 or media['height'] != 720:
        raise RuntimeError('Incomplete encoded movie')
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, sources=sources, identical_gameplay_frames=252,
        portrait_state_rows=rows, other_heroes_camera_unchanged=4, outside_cut_camera_unchanged=True, framing=validation,
        media=dict(file=str(movie), frames=126, fps=30, seconds=4.2, note='Offline rendered comparison without HUD; not measured runtime FPS')),
        ensure_ascii=False, indent=2)+'\n')
    print('Verified 252 gameplay frames,', rows, 'portrait rows, unchanged other heroes and camera outside cut; encoded 4.2 seconds')


if __name__ == '__main__':
    main()
