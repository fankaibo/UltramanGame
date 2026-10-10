"""Bind Grigio's wrist-crossed finisher film to its runtime and two actual Release runs."""
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/grigio-shot-20261009'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    before, after = [FOLDER / name / '60' for name in ('before', 'after')]
    old = (before / 'trace.csv').read_bytes()
    if old != (after / 'trace.csv').read_bytes() or len(old.splitlines()) != 301:
        raise RuntimeError('Finisher combat state changed')
    sources = dict(line.rsplit(' ', 1) for line in (after / 'sources.txt').read_text().splitlines())
    baseline = dict(line.rsplit(' ', 1) for line in (before / 'sources.txt').read_text().splitlines())
    for name, expected in sources.items():
        if sha(ROOT / 'unity/Assets' / name) != expected:
            raise RuntimeError('Stale runtime rendering: ' + name)
    old_actor = subprocess.check_output(['git', 'show', '92f36f8:unity/Assets/Scripts/Runtime/RiggedActor.cs'], cwd=ROOT)
    if hashlib.sha256(old_actor).hexdigest() != baseline['Scripts/Runtime/RiggedActor.cs']:
        raise RuntimeError('Incorrect before version')
    reports = [(FOLDER / 'after' / str(hz) / 'validation.txt').read_text().strip() for hz in (15, 30, 60)]
    lifecycle = (FOLDER / 'lifecycle.txt').read_text().splitlines()
    if len(lifecycle) != 33 or any(not row.endswith('passed') for row in lifecycle + reports):
        raise RuntimeError('Incomplete lifecycle evidence')
    native = json.loads((FOLDER / 'native/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or native['hero'] != 'Grigio' or guided['result'] != 'passed' or guided['battle_heroes'][0] != 'Grigio':
        raise RuntimeError('Wrong hero or failed player run')
    for name in ('grigio-circle-charge.png', 'grigio-wrist-shot.png', 'grigio-restored.png'):
        if name not in native['screenshots']:
            raise RuntimeError('Missing native pose: ' + name)
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value[key] != sha(data / name) for value in (native, build['before'], build['after'])):
            raise RuntimeError('Mixed Release versions')
    args = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
    for path in (before, after):
        if len(list((path / 'frames').glob('????.png'))) != 150:
            raise RuntimeError('Incomplete continuous footage')
        args += ['-framerate', '30', '-i', str(path / 'frames/%04d.png')]
    args += ['-filter_complex', '[0:v][1:v]hstack[v]', '-map', '[v]', '-c:v', 'libx264', '-crf', '18',
             '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')]
    subprocess.run(args, check=True)
    proof = dict(baseline='92f36f8', sources=sources, reports=reports, lifecycle=lifecycle,
                 identical_gameplay_frames=300, native=native, guided=guided, build=build,
                 source='https://tamashiiweb.com/item/14006/',
                 film='Offline rendering at 30 FPS, 5 seconds; not a runtime FPS measurement.')
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>格力乔射线</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#101925;color:#edf4ff;font:16px/1.7 system-ui}video{width:100%}a{color:#6fd8ff}</style>
<h1>格力乔 · 环形聚能与腕部交叉射线</h1><p>左旧右新。同一段五秒必杀，双臂环形聚能，腕部交叉后从右手发射，结束后回到准备姿势。保留原骨骼长度与贴图，孩子继续使用已有必杀手势。</p>
<video src="comparison.mp4" controls autoplay muted loop></video><p>33 组生命周期检查、三种更新步长及实际发行包整局和合照续局通过。这里是离线渲染，不代表实机帧率或真人识别准确率。<a href="validation.json">验证数据</a> · <a href="https://tamashiiweb.com/item/14006/">万代官方动作参考</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
