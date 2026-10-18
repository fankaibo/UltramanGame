"""Check same-input rock-throw timing and encode the complete pickup comparison."""
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a169-rock-pickup'
BASELINE = '103ac6e7cbe1e2492e3cd518e52814b6584d1e6b'


def main():
    sources = {}
    for name in ('RiggedActor', 'MonsterRock', 'AnimatedActor', 'GameWorld'):
        file = 'unity/Assets/Scripts/Runtime/' + name + '.cs'
        old = (FOLDER / ('before-' + name + '.cs')).read_bytes()
        assert old == subprocess.check_output(['git', 'show', BASELINE + ':' + file], cwd=ROOT), file
        sources[file] = dict(before=hashlib.sha256(old).hexdigest(), after=hashlib.sha256((ROOT/file).read_bytes()).hexdigest())
    # The baseline wrapper restores candidate sources even if Unity fails.
    baseline_render = json.loads((FOLDER/'baseline-skin-source.json').read_text())
    candidate_render = json.loads((FOLDER/'render-source-candidate.json').read_text())
    for file, hashes in sources.items():
        assert baseline_render[file] == hashes['before'], file
        assert candidate_render[file] == hashes['after'], file
    assert candidate_render['unity/Assets/Editor/RockGripReview.cs'] == hashlib.sha256((ROOT/'unity/Assets/Editor/RockGripReview.cs').read_bytes()).hexdigest()
    traces = [list(csv.DictReader((FOLDER / v / 'block-60/trace.csv').open())) for v in ('before', 'after')]
    assert len(traces[0]) == len(traces[1]) == 360
    for old, new in zip(*traces):
        for key in ('frame', 'enemy', 'age', 'attack', 'action', 'actionAge', 'health', 'blocks', 'hits', 'flying'):
            assert old[key] == new[key], (key, old['frame'])
    for rate in (15, 30, 60):
        for outcome in ('block', 'hurt'):
            assert (FOLDER / f'after/{outcome}-{rate}/validation.txt').read_text().startswith('passed')
    assert (FOLDER/'lifecycle.txt').read_text().count('=passed') == 24
    assert (FOLDER/'delayed-warning.txt').read_text().count('=passed') == 9
    for version in ('before', 'after'):
        assert len(list((FOLDER/version/'block-60/frames').glob('*.jpg'))) == 180
    movie = FOLDER/'continuous-before-after.mp4'
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER/'before/block-60/frames/%04d.jpg'),
                    '-framerate', '30', '-i', str(FOLDER/'after/block-60/frames/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '180', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 180 and media['width'] == 2560 and media['height'] == 720
    (FOLDER/'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, sources=sources, identical_gameplay_frames=360,
        pickup_metrics=(FOLDER/'after/block-60/metrics.txt').read_text(), lifecycle_cases=24, delayed_voice_cases=9,
        media=dict(file=str(movie), frames=180, seconds=6, fps=30, note='Offline same-input comparison without HUD; not runtime FPS')),
        ensure_ascii=False, indent=2)+'\n')
    print('360 gameplay frames unchanged; 6 pickup/contact, 24 lifecycle, 9 delayed-voice cases passed; encoded 6 seconds')


if __name__ == '__main__':
    main()
