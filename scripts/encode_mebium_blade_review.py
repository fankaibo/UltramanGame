"""Bind Mebius wrist blade footage to code, unchanged gameplay and Release proof."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/mebium-blade-20261009'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    reports, sources = [], {}
    for hz in (15, 30, 60):
        folder = FOLDER / 'after' / str(hz)
        report = (folder / 'validation.txt').read_text().strip()
        if 'passed' not in report or float(report.split('forearmAngle=')[1].split()[0]) > .1:
            raise RuntimeError('Original blade verification missing: ' + report)
        reports.append(report)
        for line in (folder / 'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            if sha(ROOT / 'unity/Assets' / name) != expected:
                raise RuntimeError('Stale visual evidence: ' + name)
            sources[name] = expected
    lifecycle = (FOLDER / 'lifecycle.txt').read_text().splitlines()
    if len(lifecycle) != 9 or any('passed' not in line for line in lifecycle):
        raise RuntimeError('Missing mesh integrity or lifecycle tests')
    old, new = (FOLDER / version / '60' for version in ('before', 'after'))
    baseline = dict(line.rsplit(' ', 1) for line in (old / 'sources.txt').read_text().splitlines())
    for name in ('Scripts/Runtime/RiggedActor.cs', 'Scripts/Runtime/HeroBlade.cs',
                 'Scripts/Runtime/GameWorld.cs', 'Scripts/Runtime/AnimatedActor.cs',
                 'Resources/Characters/Mebius/Mebius.fbx'):
        content = subprocess.check_output(['git', 'show', '7849cb0:unity/Assets/' + name], cwd=ROOT)
        if hashlib.sha256(content).hexdigest() != baseline[name]:
            raise RuntimeError('Unexpected before source: ' + name)
    traces = [list(csv.DictReader((folder / 'trace.csv').open())) for folder in (old, new)]
    if len(traces[0]) != 300 or traces[0] != traces[1]:
        raise RuntimeError('Combat timing or damage differs')
    native = json.loads((FOLDER / 'native/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_selection_reset']:
        raise RuntimeError('Release round or photo/replay incomplete')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(record[key] != sha(data / name) for record in (native, build['before'], build['after'])):
            raise RuntimeError('Build changed during gameplay: ' + key)
    command = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
    for folder in (old, new):
        if len(list((folder / 'frames').glob('????.png'))) != 150:
            raise RuntimeError('Incomplete continuous render')
        command += ['-framerate', '30', '-i', str(folder / 'frames/%04d.png')]
    command += ['-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')]
    subprocess.run(command, check=True)
    (FOLDER / 'validation.json').write_text(json.dumps(dict(baseline='7849cb0', sources=sources,
        cases=reports, lifecycle=lifecycle, identical_gameplay_frames=300, native=native, guided=guided,
        build=build, video='Offline Unity render at 30 FPS; not live frame-rate evidence'), ensure_ascii=False, indent=2)+'\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>梦比优斯左腕光剑</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#111923;color:#edf4ff;font:16px/1.7 system-ui}video{width:100%}a{color:#6fd8ff}</style>
<h1>左腕出刃、挥斩与收剑</h1><p>左旧右新。慢速左剑、快速左剑、右手普攻保持同样的输入和命中时钟。新版光刃固定在原模型手镯上，方向和转动随前臂变化；短拖尾记录真实刃面扫过的位置。</p>
<video src="comparison.mp4" controls autoplay muted loop></video>
<p>三种步长、三档速度的腕部绑定、手臂长度、防御/第五击/必杀/暂停/换英雄检查，及实际整局和合照续局通过。视频是离线 30 FPS 渲染，不表示实机帧率。<a href="validation.json">源码与构建证据</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
