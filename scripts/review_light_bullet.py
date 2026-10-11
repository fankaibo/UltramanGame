"""Verify identical gameplay/body samples and encode the actual Unity renders."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/light-bullet-20261012'
BASELINE = '4c66497'


def main():
    sha = lambda data: hashlib.sha256(data).hexdigest()
    before = FOLDER / 'before/Tiga-60'
    after = FOLDER / 'after/Tiga-60'
    old_sources = dict(row.rsplit(' ', 1) for row in (before/'sources.txt').read_text().splitlines())
    for name in ('Scripts/Runtime/HeroProjectile.cs', 'Resources/ProjectileWake.shader'):
        source = subprocess.check_output(['git', 'show', f'{BASELINE}:unity/Assets/{name}'], cwd=ROOT)
        if sha(source) != old_sources[name]:
            raise RuntimeError('Wrong baseline source: '+name)
    for name in ('trace.csv', 'body.csv', 'extension.csv'):
        if (before/name).read_bytes() != (after/name).read_bytes():
            raise RuntimeError('Gameplay or body changed: '+name)
    if len(list(csv.DictReader((after/'trace.csv').open()))) != 300:
        raise RuntimeError('Incomplete five-second trace')
    sources = {}
    for hz in (15, 30, 60):
        folder = FOLDER / 'after' / f'Tiga-{hz}'
        report = (folder/'validation.txt').read_text()
        for phrase in ('hits=3 launches=3 impacts=3', 'launchVolume=True impactVolume=True', 'pool=stable pause=pass'):
            if phrase not in report:
                raise RuntimeError('Missing lifecycle result: '+report)
        for row in (folder/'sources.txt').read_text().splitlines():
            name, expected = row.rsplit(' ', 1)
            if sha((ROOT/'unity/Assets'/name).read_bytes()) != expected:
                raise RuntimeError('Rendered source changed: '+name)
            sources[name] = expected
    gpu = (FOLDER/'gpu/validation.txt').read_text()
    if sum(line.startswith('angle=') for line in gpu.splitlines()) != 5 or gpu.count('15/30/60Hz material/flight/zeroTime/pause/pool=pass') != 6:
        raise RuntimeError('Incomplete GPU or hero-switch checks')
    for version in ('before', 'after'):
        frames = FOLDER/version/'Tiga-60/frames'
        if len(list(frames.glob('[0-9][0-9][0-9][0-9].jpg'))) != 150:
            raise RuntimeError('Missing 30 FPS rendered sequence: '+version)
    command = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
    for version in ('before', 'after'):
        command += ['-framerate', '30', '-i', str(FOLDER/version/'Tiga-60/frames/%04d.jpg')]
    for name, filters in (
        ('comparison.mp4', '[0:v]scale=960:540[a];[1:v]scale=960:540[b];[a][b]hstack=inputs=2[v]'),
        ('detail-comparison.mp4', '[0:v]crop=420:236:430:125,scale=840:472[a];[1:v]crop=420:236:430:125,scale=840:472[b];[a][b]hstack=inputs=2[v]'),
    ):
        subprocess.run(command+['-filter_complex', filters, '-map', '[v]', '-c:v', 'libx264', '-crf', '18',
                               '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER/name)], check=True)
    result = dict(baseline=BASELINE, gameplay_identical_frames=300, body_and_elbows_identical=True,
                  rates=[15, 30, 60], gpu=gpu, sources=sources,
                  media='Offline Unity renders, 5 seconds, left before/right after; not runtime FPS evidence')
    (FOLDER/'validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    print(FOLDER/'comparison.mp4')


if __name__ == '__main__':
    main()
