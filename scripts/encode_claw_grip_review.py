"""Verify source/clock/build evidence and publish the Golza hand comparison."""
import argparse
import csv
import hashlib
import json
import math
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, required=True)
    parser.add_argument('--guided', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root / 'artifacts/claw-grip'
    baseline = '2bfc364c0f3aa70e488ac48a6a8b06a4f9ad669d'
    proof = {'baseline': baseline, 'sources': {}, 'offline_fps': 30}
    rows = []
    for version in ['before', 'after']:
        capture = folder / version
        report = (capture / 'validation.txt').read_text()
        if 'attacks=2 blocks=2' not in report or len(list((capture / 'frames').glob('*.png'))) != 144:
            raise RuntimeError('Incomplete claw sequence')
        manifest = (capture / 'render-source.txt').read_text()
        for line in manifest.splitlines()[2:]:
            file, expected = line.rsplit(' ', 1)
            if version == 'after':
                data = (root / 'unity/Assets' / file).read_bytes()
            elif file == 'Editor/ClawPoseReview.cs':
                data = (capture / 'ClawPoseReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline + ':unity/Assets/' + file], cwd=root)
            if hashlib.sha256(data).hexdigest() != expected:
                raise RuntimeError('Stale hand capture: ' + version + '/' + file)
        proof['sources'][version] = manifest
        proof[version] = report
        rows.append(list(csv.DictReader((capture / 'motion.csv').open())))
    if len(rows[0]) != len(rows[1]) or not all(all(a[k] == b[k] for k in ['frame', 'phase', 'attack', 'age', 'side']) for a, b in zip(*rows)):
        raise RuntimeError('Attack clocks changed')
    proof['matched_samples'] = len(rows[0])
    proof['max_wrist_position_change'] = max(math.dist([float(a[k]) for k in ['handX', 'handY', 'handZ']], [float(b[k]) for k in ['handX', 'handY', 'handZ']]) for a, b in zip(*rows))
    proof['stability'] = (folder / 'stability.txt').read_text().splitlines()
    if len(proof['stability']) != 12 or any('passed' not in line for line in proof['stability']):
        raise RuntimeError('Missing claw stability checks')
    release = (root / 'logs/claw-grip-release.log').read_text()
    if release.count('[ClawTiming] rate=') != 6 or 'Exception:' in release or 'error CS' in release:
        raise RuntimeError('Claw contact checks failed')
    player = json.loads((args.player / 'validation.json').read_text())
    guided = json.loads((args.guided / 'guided-validation.json').read_text())
    build = json.loads((args.guided / 'build.json').read_text())
    if player['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Native battle or guided replay failed')
    app = root / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        actual = hashlib.sha256((app / file).read_bytes()).hexdigest()
        if player[key] != actual or any(build[stage][key] != actual for stage in ['before', 'after']):
            raise RuntimeError('Different native builds')
    proof['player'], proof['guided'] = player, guided
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(folder / 'comparison.mp4')], check=True)
    (folder / 'native').mkdir(exist_ok=True)
    shots = ''
    for name, label in [('monster-threat', '蓄力'), ('monster-rush-right', '右爪前冲'), ('monster-rush-left', '左爪前冲'), ('guard-impact', '护盾接触')]:
        shutil.copy2(args.player / 'native' / (name + '.png'), folder / 'native' / (name + '.png'))
        shots += f'<figure><img src="native/{name}.png" loading="lazy"><figcaption>{label}</figcaption></figure>'
    (folder / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>格尔赞 · 收爪与手臂转动</title><style>body{background:#101821;color:#edf3fa;margin:0;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b8c5d2}video,img{display:block;width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}figcaption{padding:8px}.labels{display:flex;justify-content:space-around;background:#273848;padding:10px}button{font:inherit;color:white;background:#254155;border:1px solid #4e6a81;border-radius:6px;padding:8px 14px;margin:10px 10px 0 0}a{color:#88e0ec}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>格尔赞 · 收爪与手臂转动</h1><p>双手由平摊改为侧向收爪；四指分别弯曲，拇指向掌心对合，转动由前臂和手腕共同承担。另修正上一帧身体朝向对手臂计算的反馈，使暂停和重复渲染时手部保持稳定。</p>
<div class="labels"><b>修改前 · 2bfc364</b><b>修改后 · 侧向收爪</b></div><video src="comparison.mp4" controls loop playsinline></video>
<button onclick="document.querySelector('video').playbackRate=.5">半速</button><button onclick="document.querySelector('video').playbackRate=1">正常速度</button>
<p>4.8 秒、144 帧近景对照，剪去等待，包含待机和两次交替爪击。背景及英雄隐藏以便检查手部；离线 30 FPS 不代表游戏帧率。两侧 3176 个手部采样的攻击阶段与时点一致。</p>
<h2>当前游戏中的爪击</h2><div class="pair">'''+shots+'''</div><p>12 组连续攻防、受击、必杀、暂停与换局检查，以及 6 组接触光迹时序检查通过。当前构建完成完整战斗及无键鼠合照、重拍、下一局验证。</p>
<p>旧 RiggedReview 的英雄手部位移检查在修改前后都失败，已保存独立基准日志，没有将其计入通过项。本轮修复手部姿态与计算反馈，角色源模型精细度、整体动作丰富度仍需提升。</p><a href="provenance.json">源码、构建与验证记录</a></main></html>''')
    print(folder / 'index.html')


if __name__ == '__main__':
    main()
