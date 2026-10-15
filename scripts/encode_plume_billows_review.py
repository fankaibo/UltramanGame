"""Validate source/build identity and encode the A-20261009-64 smoke comparison."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/volcano-billows-20261009'
BASELINE = '82103abd5a54c9c84b05719b12932489193e0f3b'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, required=True)
    args = parser.parse_args()
    sources = {}
    for side in ('before', 'after'):
        capture = FOLDER / side
        report = (capture / 'validation.txt').read_text()
        if 'ventFrames=120 repeat=passed health=50 blocks=1 hurt=0' not in report:
            raise RuntimeError('Incomplete render: ' + side)
        for subfolder, count in [('frames', 240), ('vent-frames', 120)]:
            if len(list((capture / subfolder).glob('????.png'))) != count:
                raise RuntimeError('Incomplete frames: ' + side + '/' + subfolder)
        sources[side] = dict(line.rsplit(' ', 1) for line in (capture / 'sources.txt').read_text().splitlines())
        for name, expected in sources[side].items():
            if side == 'before' and name in ('Resources/VolcanicPlume.shader', 'Scripts/Runtime/VolcanoStage.cs'):
                data = subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/' + name], cwd=ROOT)
            elif side == 'before' and name == 'Editor/VolcanoStageReview.cs':
                data = (capture / 'VolcanoStageReview.cs').read_bytes()
            else:
                data = (ROOT / 'unity/Assets' / name).read_bytes()
            if digest(data) != expected:
                raise RuntimeError('Stale render: ' + side + '/' + name)
    before = (FOLDER / 'before/sequence.csv').read_bytes()
    if before != (FOLDER / 'after/sequence.csv').read_bytes() or len(before.splitlines()) != 481:
        raise RuntimeError('Battle state differs')
    volume = (FOLDER / 'volume-validation.txt').read_text().strip()
    if 'foregroundLeakPixels=0' not in volume or 'zeroTime=passed' not in volume:
        raise RuntimeError('Missing volume depth/clock checks')
    native = json.loads((args.player / 'validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Release round or guided replay failed')
    data_folder = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        expected = digest((data_folder / name).read_bytes())
        if any(record[key] != expected for record in (native, build['before'], build['after'])):
            raise RuntimeError('Evidence is from another Release build')
    (FOLDER / 'native').mkdir(exist_ok=True)
    for name in ('battle-entry.png', 'guard-impact.png', 'beam-sustain.png'):
        shutil.copy2(args.player / 'native' / name, FOLDER / 'native' / name)
    for subfolder, movie in [('frames', 'battle-comparison.mp4'), ('vent-frames', 'vent-comparison.mp4')]:
        command = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
        for side in ('before', 'after'):
            command += ['-framerate', '30', '-i', str(FOLDER / side / subfolder / '%04d.png')]
        command += ['-filter_complex', '[0:v][1:v]hstack[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / movie)]
        subprocess.run(command, check=True)
    proof = dict(baseline=BASELINE, sources=sources, identical_gameplay_samples=480,
                 volume=volume, player=native, guided=guided, build=build,
                 film='Offline render: 8s battle and 4s vent, 30 FPS. Not live performance evidence.')
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>火山烟团：修改前后</title><style>body{max-width:1600px;margin:24px auto;padding:20px;background:#101925;color:#edf4ff;font:16px/1.7 system-ui}video,img{width:100%}a{color:#6fd8ff}.labels{display:flex;justify-content:space-around;padding:8px;background:#263242}h2{font-size:22px}</style>
<h1>火山烟团：上升、膨胀与消散</h1><p>左旧右新。烟团从喷口连续上升，扩大后在顶部消散；内部遮光压暗褶皱，暖光集中在喷口。</p>
<h2>火山口近景 · 4 秒</h2><div class="labels"><b>修改前</b><b>修改后</b></div><video src="vent-comparison.mp4" controls autoplay muted loop playsinline></video>
<h2>同机位战斗 · 8 秒</h2><div class="labels"><b>修改前</b><b>修改后</b></div><video src="battle-comparison.mp4" controls muted loop playsinline></video>
<p>前后 480 条战斗状态相同；静止时钟、前景遮挡和体积内视角通过。以上为离线 30 FPS 渲染，不代表实机帧率。</p>
<h2>实际发行包 · 1080P</h2><img src="native/battle-entry.png"><p>同一发行包完成战斗和无键鼠合照续局，使用合成输入，未调用真人摄像头。<a href="validation.json">源码、构建与验证记录</a></p>
<p>这是场景表现的一轮改进；精细模型、影视原声和真人／电视现场验收仍未完成。</p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
