"""Verify unchanged combat/contact positions and encode visible claw articulation."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a176-golza-claw-review'
BASELINE = 'e21295420ead9beb26b8ae0a00e9fa57d7c47b6a'
SOURCE = 'unity/Assets/Scripts/Runtime/RiggedActor.cs'


def movie(left, right, output, count):
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(left), '-framerate', '30', '-i', str(right),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-frames:v', str(count), '-c:v', 'libx264',
                    '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(output)], check=True)
    info = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(output)], text=True))['streams'][0]
    assert int(info['nb_frames']) == count and info['width'] == 2560 and info['height'] == 720


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true', help='Check retained evidence without rebuilding trimmed frame sequences')
    args = parser.parse_args()
    saved = (FOLDER / 'before-RiggedActor.cs').read_bytes()
    assert saved == subprocess.check_output(['git', 'show', BASELINE + ':' + SOURCE], cwd=ROOT)
    max_position = 0
    for label in ('right-hurt-60', 'left-block-60'):
        before = FOLDER / 'continuous/before' / label
        after = FOLDER / 'continuous/after' / label
        assert (before / 'trace.csv').read_bytes() == (after / 'trace.csv').read_bytes()
        old = list(csv.DictReader((before / 'motion.csv').open()))
        new = list(csv.DictReader((after / 'motion.csv').open()))
        assert len(old) == len(new) == 600
        max_position = max(max_position, max(abs(float(a[k]) - float(b[k])) for a, b in zip(old, new) for k in ('side', 'up', 'forward')))
    assert max_position <= .0002
    old_all = list(csv.DictReader((FOLDER / 'before/motion.csv').open()))
    new_all = list(csv.DictReader((FOLDER / 'after/motion.csv').open()))
    assert len(old_all) == len(new_all) == 7706
    assert all(all(a[k] == b[k] for k in ('frame', 'phase', 'attack', 'age', 'side')) for a, b in zip(old_all, new_all))
    full_position = max(abs(float(a[k]) - float(b[k])) for a, b in zip(old_all, new_all) for k in ('handX', 'handY', 'handZ'))
    assert full_position <= .00002
    unchanged = []
    for name in ('Rest-0', 'Attack-2', 'Attack-3', 'Attack-4'):
        a = Image.open(FOLDER / 'before' / (name + '.png')).convert('RGB')
        b = Image.open(FOLDER / 'after' / (name + '.png')).convert('RGB')
        assert ImageChops.difference(a, b).getbbox() is None, name
        unchanged.append(name)
    assert len(list((FOLDER / 'continuous/after').glob('*/validation.txt'))) == 12
    assert len((FOLDER / 'stability.txt').read_text().splitlines()) == 12
    if not args.verify_only:
        movie(FOLDER / 'continuous/before/right-hurt-60/frames/%04d.png', FOLDER / 'continuous/after/right-hurt-60/frames/%04d.png', FOLDER / 'continuous-before-after.mp4', 150)
        movie(FOLDER / 'before/frames/%04d.jpg', FOLDER / 'after/frames/%04d.jpg', FOLDER / 'selected-phases-before-after.mp4', 337)
        sheet = Image.new('RGB', (2560, 1440))
        for row, frame in enumerate(('0030', '0320')):
            for col, version in enumerate(('before', 'after')):
                sheet.paste(Image.open(FOLDER / version / 'frames' / (frame + '.jpg')), (col * 1280, row * 720))
        sheet.save(FOLDER / 'claw-before-after.jpg', quality=92)
    result = dict(baseline=BASELINE, before_sha256=hashlib.sha256(saved).hexdigest(), after_sha256=hashlib.sha256((ROOT / SOURCE).read_bytes()).hexdigest(),
                  identical_combat_rows=600, hand_position_max_delta=max_position, five_attack_rows=7706, five_attack_position_delta=full_position, identical_stage_images=unchanged,
                  continuous_frames=150, selected_frames=337, note='Left old / right new. Five-second continuous battle and separately edited 11.23-second closeups. Offline FPS is not a runtime benchmark.')
    (FOLDER / 'comparison.json').write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
