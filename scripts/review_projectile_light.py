"""Verify the same-input flight and show illumination on the actual character meshes."""
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a175-projectile-light'
BASELINE = 'df2bdc67a8b5604ce5b267c2aa0e887b68666a8c'
SOURCE = 'unity/Assets/Scripts/Runtime/HeroProjectile.cs'


def main():
    prior = (FOLDER / 'before-HeroProjectile.cs').read_bytes()
    assert prior == subprocess.check_output(['git', 'show', BASELINE + ':' + SOURCE], cwd=ROOT)
    before = (FOLDER / 'before/60/trace.csv').read_bytes()
    assert before == (FOLDER / 'after/60/trace.csv').read_bytes()
    assert len(before.splitlines()) == 241
    report = (FOLDER / 'lifecycle.txt').read_text()
    assert len(report.splitlines()) == 30 and report.count('=pass') == 30
    changed = {}
    sheet = Image.new('RGB', (2560, 1440))
    for index, name in enumerate(('depart', 'middle', 'approach')):
        on = Image.open(FOLDER / f'surface/{name}-on.png').convert('RGB')
        off = Image.open(FOLDER / f'surface/{name}-off.png').convert('RGB')
        diff = ImageChops.difference(on, off)
        changed[name] = {}
        for label, rect in [('hero', (250, 110, 580, 700)), ('monster', (640, 110, 920, 610))]:
            changed[name][label] = sum(max(pixel) > 6 for pixel in diff.crop(rect).getdata())
        if name == 'depart':
            assert changed[name]['hero'] > 100
        if name == 'approach':
            assert changed[name]['monster'] > 100
        if name != 'middle':
            row = 0 if name == 'depart' else 720
            sheet.paste(off, (0, row));sheet.paste(on, (1280, row))
    sheet.save(FOLDER / 'surface-before-after.jpg', quality=92)
    movie = FOLDER / 'continuous-before-after.mp4'
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/60/%04d.jpg'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/60/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '120', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 120 and media['width'] == 2560 and media['height'] == 720
    result = dict(baseline=BASELINE, source=SOURCE, before_sha256=hashlib.sha256(prior).hexdigest(),
                  after_sha256=hashlib.sha256((ROOT / SOURCE).read_bytes()).hexdigest(), identical_state_rows=240,
                  lifecycle_cases=30, actual_mesh_changed_pixels=changed, frames=120, seconds=4,
                  note='Left old / right new. Offline capture rate does not measure game FPS. Surface pairs toggle only the travelling light in one frozen frame.')
    (FOLDER / 'comparison.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
