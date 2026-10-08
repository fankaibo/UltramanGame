"""Verify melee/guard continuity evidence and encode the two hero comparisons."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/melee-guard-20261009'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    before, after = (FOLDER / name for name in ('before', 'after'))
    records = [list(csv.DictReader((p / 'cases.csv').open())) for p in (before, after)]
    if any(len(r) != 360 for r in records):
        raise RuntimeError('Five-hero guard cases incomplete')
    old_trace, new_trace = [(p / 'trace.csv').read_bytes() for p in (before, after)]
    if old_trace != new_trace:
        raise RuntimeError('Combat behavior differs across the same 360 cases')
    baseline = dict(line.rsplit(' ', 1) for line in (before / 'sources.txt').read_text().splitlines())
    old_actor = subprocess.check_output(['git', 'show', '3f98e54:unity/Assets/Scripts/Runtime/RiggedActor.cs'], cwd=ROOT)
    if hashlib.sha256(old_actor).hexdigest() != baseline['Scripts/Runtime/RiggedActor.cs']:
        raise RuntimeError('Unexpected baseline animation source')
    sources = dict(line.rsplit(' ', 1) for line in (after / 'sources.txt').read_text().splitlines())
    for name, expected in sources.items():
        if sha(ROOT / 'unity/Assets' / name) != expected:
            raise RuntimeError('Stale visual evidence: ' + name)
    lifecycle = (FOLDER / 'lifecycle.txt').read_text().splitlines()
    if len(lifecycle) != 15 or any(not line.endswith('passed') for line in lifecycle):
        raise RuntimeError('Guard/counter/pause/new-round evidence incomplete')
    native = json.loads((FOLDER / 'native/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if (native['result'] != 'passed' or not native.get('guard_handoff') or
            guided['result'] != 'passed' or not guided['replay_selection_reset']):
        raise RuntimeError('Release proof does not cover the transition and full guided loop')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(record[key] != sha(data / name) for record in (native, build['before'], build['after'])):
            raise RuntimeError('Player evidence belongs to a different build')
    args = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
    for hero in ('Tiga', 'Mebius'):
        for p in (before, after):
            frames = p / hero / 'frames'
            if len(list(frames.glob('????.png'))) != 60:
                raise RuntimeError('Incomplete comparison frames')
            args += ['-framerate', '15', '-i', str(frames / '%04d.png')]
    args += ['-filter_complex', '[0:v][1:v]hstack[a];[2:v][3:v]hstack[b];[a][b]concat=n=2:v=1:a=0,fps=30[v]',
             '-map', '[v]', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
             str(FOLDER / 'comparison.mp4')]
    subprocess.run(args, check=True)
    metrics = {}
    for label, rows in zip(('before', 'after'), records):
        metrics[label] = {hz: {key: max(float(r[key]) for r in rows if r['hz'] == hz)
                              for key in ('firstStep', 'maxStep', 'firstRoot', 'maxRoot', 'maxFoot')}
                          for hz in ('15', '30', '60')}
    proof = dict(baseline='3f98e54', sources=sources, cases=360, metrics=metrics, lifecycle=lifecycle,
                 identical_gameplay_frames=len(old_trace.splitlines())-1, native=native, guided=guided, build=build,
                 video='Offline sequence, half speed, 30 FPS export. Not runtime FPS evidence.')
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>近战转防御连续动作</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#101925;color:#edf4ff;font:16px/1.7 system-ui}video{width:100%}a{color:#6fd8ff}</style>
<h1>出拳、收手、举盾连续衔接</h1><p>左旧右新，先迪迦后梦比优斯，均为二分之一速度便于观察。护盾按原规则立即生效，身体与手臂从上一帧实际姿势接续；前脚收回时短暂抬起。</p>
<video src="comparison.mp4" controls autoplay muted loop></video>
<p>五英雄 360 个切换场景，实际蒙皮、暂停恢复、反击与新局检查，以及实际发行包整局和合照续局。视频为离线输出，不代表实机帧率或真人识别准确率。<a href="validation.json">源码与构建证据</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
