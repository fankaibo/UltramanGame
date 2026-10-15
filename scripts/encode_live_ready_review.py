"""Encode side-by-side actual Unity captures: baseline left, live ready right."""
import hashlib
import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = root / 'artifacts/live-ready-20261010'
frames = {name: sorted((folder / name / 'frames').glob('*.png')) for name in ('before', 'after')}
assert len(frames['before']) == len(frames['after']) == 270
assert (folder / 'before/trace.csv').read_bytes() == (folder / 'after/trace.csv').read_bytes()
output = folder / 'comparison.mp4'
subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                '-framerate', '30', '-i', str(folder / 'before/frames/%04d.png'),
                '-framerate', '30', '-i', str(folder / 'after/frames/%04d.png'),
                '-filter_complex', 'hstack=inputs=2,scale=1920:540',
                '-an', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                '-movflags', '+faststart', str(output)], check=True)
probe = json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-of','json',str(output)]))
assert int(probe['streams'][0]['nb_frames']) == 270
(folder / 'video.json').write_text(json.dumps(dict(frames=270,fps=30,seconds=9,
    left='Existing fixed ready pose, live input disabled',right='New continuous hand preparation',
    sha256=hashlib.sha256(output.read_bytes()).hexdigest()),indent=2)+'\n')
(folder / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>准备动作实时跟随</title><style>body{background:#111a2b;color:#edf3ff;font:18px system-ui;margin:32px}video{width:100%;max-width:1920px}p{max-width:1000px}</style><h1>准备动作实时跟随</h1><p>左：原版固定待战姿势。右：左右手在招式触发前分别跟随，并衔接出拳、防御、暂停和恢复。</p><video controls loop src="comparison.mp4"></video><p>真实 Unity 骨骼／场景渲染，输入相同，270 帧，30 FPS，9 秒。不是生成的概念动画。相机使用合成姿态，未使用真人图像。</p></html>''')
print(output)
