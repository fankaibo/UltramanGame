"""Bind continuous-motion footage to its sources and tested Release package."""
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/engagement-20261008'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources = {}
    for row in (FOLDER / 'after/sources.txt').read_text().splitlines():
        name, expected = row.rsplit(' ', 1)
        if sha(ROOT / 'unity/Assets' / name) != expected:
            raise RuntimeError('Stale rendered source: ' + name)
        sources[name] = expected
    traces = {}
    for version in ('before', 'after'):
        with (FOLDER / version / 'Tiga-60.csv').open() as stream:
            traces[version] = list(csv.DictReader(stream))
        if len(traces[version]) != 480:
            raise RuntimeError('Incomplete eight-second trace: ' + version)
    game_keys = ('frame', 'action', 'age', 'punches', 'health', 'energy')
    for old, new in zip(traces['before'], traces['after']):
        if any(old[key] != new[key] for key in game_keys):
            raise RuntimeError('Gameplay/timing differs at frame ' + new['frame'])
    reports = [(FOLDER / 'after' / f'{hero}-{hz}.txt').read_text()
               for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio') for hz in (15, 30, 60)]
    for report in reports:
        if 'hits=6 ' not in report:
            raise RuntimeError('Model sequence failed: ' + report)
    transitions = (FOLDER / 'transitions/validation.txt').read_text().splitlines()
    if len(transitions) != 15 or any('=pass ' not in line for line in transitions):
        raise RuntimeError('Incomplete transition review')
    before = (FOLDER / 'before/Tiga-60.txt').read_text()
    after = (FOLDER / 'after/Tiga-60.txt').read_text()
    old_path = float(re.search(r'path=([\d.]+)', before)[1])
    new_path = float(re.search(r'path=([\d.]+)', after)[1])
    reduction = 1 - new_path / old_path
    if reduction < .30:
        raise RuntimeError('Repeated approach has not been reduced enough')
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or guided['result'] != 'passed':
        raise RuntimeError('Release regression failed')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(item[key] != sha(data / file) for item in (native, build['before'], build['after'])):
            raise RuntimeError('Release evidence mismatch: ' + key)
    for version in ('before', 'after'):
        frames = FOLDER / version / 'frames'
        if len(list(frames.glob('[0-9][0-9][0-9][0-9].png'))) != 240:
            raise RuntimeError('Missing continuous footage: ' + version)
        subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30',
                        '-i', str(frames / '%04d.png'), '-c:v', 'libx264', '-crf', '18',
                        '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / (version + '.mp4'))], check=True)
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-i', str(FOLDER / 'before.mp4'),
                    '-i', str(FOLDER / 'after.mp4'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    report = dict(sources=sources, gameplay_identical_frames=480, model_checks=reports,
                  transitions=transitions, before=before, after=after, root_path_reduction=reduction,
                  native=native, guided=guided, build=build,
                  media=dict(frames=240, export_fps=30, kind='offline Unity render, not a runtime FPS benchmark'))
    (FOLDER / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    curves = []
    for version, color in [('before', '#ffbe76'), ('after', '#6fd8ff')]:
        points = ' '.join(f'{20 + i * 2:.1f},{210 - float(row["rootTravel"]) * 55:.1f}'
                          for i, row in enumerate(traces[version]))
        curves.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="2"/>')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>连击接近与退步连续性</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#111923;color:#edf4ff;font:16px/1.7 system-ui}p{color:#bfcbdb}video{width:100%}a{color:#6fd8ff}svg{width:100%;background:#182333}</style>
<h1>连击接近与退步连续性</h1><p>左侧：每拳回到远处。右侧：连击保持接近，停手后分两步退开。相同输入、伤害和时钟；六击根节点累计位移从 31.92 降至 18.10 Unity 单位，减少约 43.3%。</p>
<video src="comparison.mp4" controls autoplay muted loop></video>
<p>八秒内角色沿战斗方向的位置，黄色为修改前、蓝色为修改后；更少的往返波动代表接近距离被保留，曲线仍包含原来的拳脚推进。</p>
<svg viewBox="0 0 1000 250" role="img" aria-label="六次连击的站位变化对照">''' + ''.join(curves) + '''</svg>
<p>五位实际骨骼英雄在 15/30/60 Hz 步长下通过近战、护盾、远程、大招、倒地、暂停和新局检查；发行包另跑了整局及合成体感合照续局。缺失模型和真实儿童／电视体验未计入验收。</p>
<p>视频为实际 Unity 代码的连续离线渲染，30 FPS 导出不代表实机性能。<a href="validation.json">源码、状态轨迹与发行包验证记录</a> · <a href="after.mp4">修改后单独视频</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
