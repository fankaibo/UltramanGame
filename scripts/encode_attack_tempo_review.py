"""Bind the remote-skill comparison to rendered sources and the actual Release."""
import hashlib
import json
import re
import statistics
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/attack-tempo-20261008'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources = {}
    for line in (FOLDER / 'render-source.txt').read_text().splitlines():
        name, expected = line.rsplit(' ', 1)
        if digest(ROOT / 'unity/Assets' / name) != expected:
            raise RuntimeError('Rendering is stale: ' + name)
        sources[name] = expected
    report = (FOLDER / 'validation.txt').read_text()
    for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'):
        for speed in ('0.65', '1.70'):
            if f'{hero} speed={speed} hits=1 launches=1' not in report:
                raise RuntimeError('Missing actual model/tempo validation')
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    if guided['result'] != 'passed' or native['result'] != 'passed' or not guided['tempo_skills']:
        raise RuntimeError('Missing actual release/pose receiver proof')
    log=(ROOT/'logs/attack-tempo-guided.log').read_text()
    attacks=re.findall(r'\[AttackTempo\] ranged=True speed=([\d.]+) duration=([\d.]+) side=(LeftPunch|RightPunch)',log)
    medians={hand:statistics.median(float(row[0]) for row in attacks if row[2]==hand) for hand in ('LeftPunch','RightPunch')}
    if medians['RightPunch']<medians['LeftPunch']*1.25:
        raise RuntimeError('Actual fast trajectory did not produce faster animation')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(data / path) for value in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Release proof belongs to another build')
    for mode in ('slow', 'fast'):
        if len(list((FOLDER / ('Tiga-' + mode)).glob('[0-9][0-9][0-9][0-9].png'))) != 45:
            raise RuntimeError('Incomplete continuous footage')
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30',
                    '-i', str(FOLDER / 'Tiga-slow/%04d.png'), '-framerate', '30',
                    '-i', str(FOLDER / 'Tiga-fast/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '45', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(sources=sources, models=report.splitlines(), native=native, guided=guided, build=build, tempo_medians=medians,
                 media=dict(frames=45, export_fps=30, kind='offline Unity rendering; not an FPS benchmark'))
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    tempo = guided['tempo_skills']
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>远程光弹与体感攻速</title>
<style>body{background:#101722;color:#eef5ff;font:16px/1.7 system-ui;margin:25px auto;max-width:1500px;padding:20px}p{color:#b9c8da}video{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}img{width:100%}a{color:#81d5ee}select{padding:8px}</style>
<h1>手势决定左右与出招速度，向前挥拳发射光弹</h1>
<p>下方两侧是同一骨骼模型和同一起手时刻，使用允许范围两端的攻速做对照。收手后光弹继续飞行，可以转入防御；侧向挥拳仍为近战。怪兽预警、语音反应时间和必杀持姿时间不随攻速缩短。</p>
<div class="pair"><b>慢速 · 0.65 倍 · 0.65 秒恢复</b><b>快速 · 1.70 倍 · 0.25 秒恢复</b></div>
<video src="comparison.mp4" controls loop autoplay muted playsinline></video>
<p>模型专项为离线逐帧渲染，30 FPS 视频导出不代表游戏性能。真实姿态接收器另用合成前冲轨迹完成发行版整局，实际测得速度范围：SPEED_RANGE；左右发射、护盾、两次必杀、合照、重拍和续局均通过。</p>
<select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德机敏形态</option><option value="Grigio">格力乔</option></select>
<div class="pair"><img id="slow"><img id="fast"></div>
<p>当前按每次有效挥拳测得的速度播放动作，尚需儿童实机验证。角色资产、完整连续演出仍未达到参考街机最终目标。</p>
<a href="validation.json">源码、时序和发行回放证据</a>
<script>const hero=document.getElementById('hero');function show(){for(const mode of ['slow','fast'])document.getElementById(mode).src=hero.value+'-'+mode+'/flight.png'}hero.onchange=show;show()</script></html>'''.replace('SPEED_RANGE', f"{tempo['speed_min']:.2f}–{tempo['speed_max']:.2f} 倍"))
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
