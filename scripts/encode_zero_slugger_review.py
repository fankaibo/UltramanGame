"""Bind original Zero blade footage to code, unchanged gameplay and Release proof."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/zero-slugger-20261009'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    reports, sources = [], {}
    for hz in (15, 30, 60):
        folder = FOLDER / 'after' / str(hz)
        report = (folder / 'validation.txt').read_text().strip()
        if 'passed' not in report or 'proceduralFlightFrames=0' not in report:
            raise RuntimeError('Original blade verification missing: ' + report)
        reports.append(report)
        for line in (folder / 'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            if sha(ROOT / 'unity/Assets' / name) != expected:
                raise RuntimeError('Stale visual evidence: ' + name)
            sources[name] = expected
    lifecycle = (FOLDER / 'lifecycle.txt').read_text().splitlines()
    if len(lifecycle) != 10 or any('passed' not in line for line in lifecycle):
        raise RuntimeError('Missing mesh integrity or lifecycle tests')
    old, new = (FOLDER / version / '60' for version in ('before', 'after'))
    baseline = dict(line.rsplit(' ', 1) for line in (old / 'sources.txt').read_text().splitlines())
    for name in ('Scripts/Runtime/RiggedActor.cs', 'Scripts/Runtime/HeroProjectile.cs',
                 'Scripts/Runtime/GameWorld.cs', 'Scripts/Runtime/AnimatedActor.cs',
                 'Resources/Characters/Zero/Zero.fbx', 'Resources/Characters/Zero/Textures/Zero_Sluggers.png'):
        content = subprocess.check_output(['git', 'show', 'fb05a70:unity/Assets/' + name], cwd=ROOT)
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
    (FOLDER / 'validation.json').write_text(json.dumps(dict(baseline='fb05a70', sources=sources,
        cases=reports, lifecycle=lifecycle, identical_gameplay_frames=300, native=native, guided=guided,
        build=build, video='Offline Unity render at 30 FPS; not live frame-rate evidence'), ensure_ascii=False, indent=2)+'\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>赛罗原模型双头镖</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#111923;color:#edf4ff;font:16px/1.7 system-ui}video{width:100%}a{color:#6fd8ff}img{max-width:49%}</style>
<h1>双头镖离头飞出，再回到头部</h1><p>左旧右新，依次慢速、常速、快速释放。新版直接移动原模型的专属骨骼，保留头镖形状、贴图和大小；头上不再保留重复物体。双弧线回到动作中的头部。</p>
<video src="comparison.mp4" controls autoplay muted loop></video>
<p>新版头部近景：发射中 / 归位后</p><img src="after/60/head-outbound.png"><img src="after/60/head-reattached.png">
<p>原网格完整性、三种步长与三档速度的防御/暂停/新局/特写/换英雄检查，及 Release 整局、合照和续局通过。离线渲染不表示实机帧率。<a href="validation.json">源码与构建证据</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
