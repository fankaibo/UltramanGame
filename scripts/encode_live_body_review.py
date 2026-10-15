"""Compare actual Unity body-follow captures with the pre-change source baseline."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
folder = ROOT / 'artifacts/live-body-20261010'
for side in ('before', 'after'):
    assert len(list((folder / side / 'frames').glob('*.png'))) == 210
assert (folder / 'before/gameplay.csv').read_bytes() == (folder / 'after/gameplay.csv').read_bytes()
before = list(csv.DictReader((folder / 'before/body.csv').open()))
after = list(csv.DictReader((folder / 'after/body.csv').open()))
assert len(before) == len(after) == 3675
foot_error = max(abs(float(a[key]) - float(b[key])) for a, b in zip(after, before) for key in ('leftY', 'rightY'))
assert foot_error < .00001, foot_error
# Increment in lateral head movement caused by the new layer, separated from
# the existing fast attack/guard transitions. Do not claim the whole clip has
# this much motion; this quantifies only the additive layer.
previous = {}
steps = {}
for a, b in zip(after, before):
    group = (a['hero'], a['hz'])
    assert group == (b['hero'], b['hz']) and a['frame'] == b['frame']
    offset = float(a['headX']) - float(b['headX'])
    if group in previous:
        steps[group] = max(steps.get(group, 0), abs(offset - previous[group]))
    previous[group] = offset
assert max(v for (_, hz), v in steps.items() if hz == '60') < .08, steps
video = folder / 'comparison.mp4'
subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                '-framerate', '30', '-i', str(folder / 'before/frames/%04d.png'),
                '-framerate', '30', '-i', str(folder / 'after/frames/%04d.png'),
                '-filter_complex', 'hstack=inputs=2,scale=1920:540',
                '-an', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                '-movflags', '+faststart', str(video)], check=True)
probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(video)]))['streams'][0]
assert int(probe['nb_frames']) == 210 and abs(float(probe['duration']) - 7) < .01
report = dict(baseline_commit='44de48f4ae8fc833d36c131b25a7aad4711d722b', frames=210, fps=30, seconds=7,
              gameplay_rows=len(after), foot_height_difference=foot_error,
              added_head_step={f'{hero}/{hz}': value for (hero, hz), value in steps.items()},
              sha256=hashlib.sha256(video.read_bytes()).hexdigest())
(folder / 'video.json').write_text(json.dumps(report, indent=2) + '\n')
(folder / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>侧身与重心跟随</title><style>body{background:#08111f;color:#eef4ff;font:18px system-ui;margin:32px}video{width:100%;max-width:1920px}</style><h1>侧身与重心跟随</h1><p>左：原版，躯干固定。右：肩膀倾斜和侧身会带动身体，保持原有脚底位置；正式招式接管动作。</p><video controls loop muted src="comparison.mp4"></video><p>同一输入、实际 Unity 渲染，7 秒、210 帧。编码帧率不代表实机性能；未使用真人摄像头图像。</p></html>''')
print(json.dumps(report, indent=2))
