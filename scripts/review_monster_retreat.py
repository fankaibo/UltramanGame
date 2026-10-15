"""Encode A-124's actual Unity frames and verify unchanged battle outcomes."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'artifacts/monster-retreat-20261012'
BASELINE = 'd5ea74038552c66043c5c81bae4c62caa4affe3b'


def encode():
    reports = {}
    rows = 0
    for attack in (1, 5):
        old, new = [BASE / version / f'{attack}-60' for version in ('before', 'after')]
        assert (old / 'states.csv').read_bytes() == (new / 'states.csv').read_bytes()
        rows += len(list(csv.DictReader((old / 'states.csv').open())))
        for folder in (old, new):
            if folder == new: assert len(list((folder / 'frames').glob('*.jpg'))) == 120
            motion = list(csv.DictReader((folder / 'motion.csv').open()))
            retreat = [r for r in motion if (r['enemy'] == 'Attack' and float(r['age']) > .72)
                       or (r['enemy'] == 'Recover' and float(r['age']) < .65)]
            reports[str(folder.relative_to(BASE))] = {
                field: max(abs(float(b[field])-float(a[field]))*60
                           for a, b in zip(retreat, retreat[1:]))
                for field in ('rootForward', 'leftForward', 'rightForward')}
        full_baseline = len(list((old/'frames').glob('*.jpg'))) == 120
        old_input = ['-framerate', '30', '-i', str(old/'frames/%04d.jpg')] if full_baseline else [
            '-ss', str(0 if attack == 1 else 4), '-t', '4', '-i', str(BASE/'first-comparison.mp4')]
        old_filter = '[0:v]scale=960:540[l];' if full_baseline else '[0:v]crop=960:540:0:0[l];'
        subprocess.run(['ffmpeg', '-y', '-hide_banner', '-loglevel', 'error', *old_input,
                        '-framerate', '30', '-i', str(new/'frames/%04d.jpg'),
                        '-filter_complex', old_filter+'[1:v]scale=960:540[r];[l][r]hstack',
                        '-c:v', 'libx264', '-crf', '20', '-pix_fmt', 'yuv420p',
                        str(BASE/f'claw-{attack}.mp4')], check=True)
    (BASE/'concat.txt').write_text("file 'claw-1.mp4'\nfile 'claw-5.mp4'\n")
    subprocess.run(['ffmpeg', '-y', '-hide_banner', '-loglevel', 'error', '-f', 'concat',
                    '-safe', '0', '-i', str(BASE/'concat.txt'), '-c', 'copy', '-movflags',
                    '+faststart', str(BASE/'comparison.mp4')], check=True)
    subprocess.run(['ffmpeg', '-y', '-hide_banner', '-loglevel', 'error', '-framerate', '30',
                    '-i', str(BASE/'counter-frames/%04d.jpg'), '-c:v', 'libx264', '-crf', '20',
                    '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(BASE/'counter.mp4')], check=True)
    sources = ['Scripts/Core/MonsterStepMotion.cs', 'Scripts/Core/MonsterRetreatMotion.cs',
               'Scripts/Runtime/RiggedActor.cs', 'Scripts/Runtime/AnimatedActor.cs',
               'Scripts/Runtime/CombatVfx.cs', 'Scripts/Runtime/GuidedProof.cs',
               'Editor/MonsterRetreatReview.cs']
    hashes = {p: hashlib.sha256((ROOT/'unity/Assets'/p).read_bytes()).hexdigest() for p in sources}
    baseline_hashes = {}
    for p in sources:
        path = 'unity/Assets/'+p
        if subprocess.run(['git', 'cat-file', '-e', BASELINE+':'+path], cwd=ROOT,
                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
            baseline_hashes[p] = hashlib.sha256(subprocess.check_output(
                ['git', 'show', BASELINE+':'+path], cwd=ROOT)).hexdigest()
    (BASE/'identity.json').write_text(json.dumps(dict(baseline=BASELINE,
        equal_gameplay_rows=rows, retreat_peak_speed=reports, sources=hashes,
        baseline_sources=baseline_hashes, baseline_video_sha256=hashlib.sha256((BASE/'first-comparison.mp4').read_bytes()).hexdigest() if (BASE/'first-comparison.mp4').exists() else None, frames=240, fps=30,
        note='Offline rendering rate, not measured game FPS'), indent=2)+'\n')
    print(json.dumps(dict(equal_gameplay_rows=rows, retreat_peak_speed=reports), indent=2))


if __name__ == '__main__':
    encode()
