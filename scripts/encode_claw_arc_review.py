"""Bind the actual monster animation comparison to its source and Release proof."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/claw-arc-20261009'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    reports, sources = [], {}
    for hz in (15, 30, 60):
        for side in ('right', 'left'):
            for outcome in ('hurt', 'block'):
                folder = FOLDER / 'after' / f'{side}-{outcome}-{hz}'
                report = (folder / 'validation.txt').read_text().strip()
                if 'passed' not in report or 'zeroError=0.000000' not in report:
                    raise RuntimeError('Missing pose verification: ' + report)
                reports.append(report)
                for line in (folder / 'sources.txt').read_text().splitlines():
                    name, expected = line.rsplit(' ', 1)
                    if sha(ROOT / 'unity/Assets' / name) != expected:
                        raise RuntimeError('Stale source: ' + name)
                    sources[name] = expected
    stability = (FOLDER / 'stability/stability.txt').read_text().splitlines()
    anticipation = (FOLDER / 'anticipation.txt').read_text().splitlines()
    if len(stability) != 12 or any('passed' not in line for line in stability):
        raise RuntimeError('Missing pause/restart/attack stability evidence')
    if len(anticipation) != 15 or any('passed' not in line for line in anticipation):
        raise RuntimeError('Missing voice-extension/interruption evidence')
    clips = []
    for label in ('right-hurt-60', 'left-block-60'):
        old, new = (FOLDER / v / label for v in ('before', 'after'))
        baseline = dict(line.rsplit(' ', 1) for line in (old / 'sources.txt').read_text().splitlines())
        for file in ('RiggedActor.cs', 'AnimatedActor.cs'):
            name = 'Scripts/Runtime/' + file
            content = subprocess.check_output(['git', 'show', '7cfac78:unity/Assets/' + name], cwd=ROOT)
            if hashlib.sha256(content).hexdigest() != baseline[name]:
                raise RuntimeError('Invalid comparison baseline: ' + name)
        traces = [list(csv.DictReader((folder / 'trace.csv').open())) for folder in (old, new)]
        if any(len(rows) != 300 for rows in traces) or traces[0] != traces[1]:
            raise RuntimeError('Gameplay changed in ' + label)
        command = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
        for folder in (old, new):
            frames = folder / 'frames'
            if len(list(frames.glob('????.png'))) != 150:
                raise RuntimeError('Incomplete continuous video: ' + str(frames))
            command += ['-framerate', '30', '-i', str(frames / '%04d.png')]
        output = FOLDER / (label + '.mp4')
        command += ['-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(output)]
        subprocess.run(command, check=True)
        clips.append(output)
    native = json.loads((FOLDER / 'native/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_selection_reset']:
        raise RuntimeError('Release/full-flow evidence incomplete')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(record[key] != sha(data / file) for record in (native, build['before'], build['after'])):
            raise RuntimeError('Release evidence hash differs: ' + key)
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-i', str(clips[0]), '-i', str(clips[1]),
                    '-filter_complex', '[0:v][1:v]concat=n=2:v=1:a=0[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    (FOLDER / 'validation.json').write_text(json.dumps(dict(baseline='7cfac78', sources=sources,
        cases=reports, stability=stability, anticipation=anticipation, identical_gameplay_frames=600,
        native=native, guided=guided, build=build, video='Offline Unity rendering, 30 FPS export; not runtime performance'), ensure_ascii=False, indent=2)+'\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>哥尔赞连贯挥爪</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#111923;color:#edf4ff;font:16px/1.7 system-ui}video{width:100%}a{color:#6fd8ff}</style>
<h1>哥尔赞：蓄力、挥爪、收招</h1><p>左旧右新。前五秒为右爪击中，后五秒为左爪被挡。左爪蓄力使用正确的手，爪尖斜向划过身体前方，再沿低弧线收回。前冲位移分布更均匀，手臂切换从屏幕上实际显示的位置开始。</p>
<video src="comparison.mp4" controls autoplay muted loop></video><p>12 组真实骨骼专项、600 帧玩法状态对照，及发行包整局、合照和续局通过。视频为离线连续渲染，不表示实机帧率。<a href="validation.json">源码与发行包证据</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
