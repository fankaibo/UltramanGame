"""Publish the camera/HUD comparison after validating capture and build provenance."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, required=True)
    parser.add_argument('--guided', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root / 'artifacts/playfield'
    baseline = 'd7ef992e4cdaba869cf4690523012925d59d7b25'
    modes = ['ordinary', 'combo', 'uppercut', 'beam']
    proof = {'baseline': baseline, 'validation': [], 'sources': [], 'offline_fps': 30}
    for version in ['before', 'after']:
        cases = sorted((folder / version).glob('*-*-*'))
        if len(cases) != (4 if version == 'before' else 28):
            raise RuntimeError('Missing framing cases')
        for case in cases:
            line = (case / 'validation.txt').read_text()
            if not line.endswith('passed'):
                raise RuntimeError('Failed framing check: ' + case.name)
            proof['validation'].append(version + ' ' + line)
            for source in (case / 'sources.txt').read_text().splitlines():
                file, expected = source.rsplit(' ', 1)
                if version == 'after':
                    data = (root / 'unity/Assets' / file).read_bytes()
                elif file == 'Editor/PlayfieldReview.cs':
                    data = (folder / 'before/PlayfieldReview.cs').read_bytes()
                else:
                    data = subprocess.check_output(['git', 'show', baseline + ':unity/Assets/' + file], cwd=root)
                if hashlib.sha256(data).hexdigest() != expected:
                    raise RuntimeError('Stale framing evidence: ' + version + '/' + file)
            proof['sources'].append({'case': str(case.relative_to(folder)), 'manifest': (case / 'sources.txt').read_text()})
        movie = folder / version / 'movie'
        movie.mkdir(exist_ok=True)
        for index, mode in enumerate(modes):
            capture = folder / version / ('Tiga-30-' + mode)
            frames = sorted((capture / 'frames').glob('*.png'))
            if len(frames) != 150:
                raise RuntimeError('Incomplete film: ' + str(capture))
            if (folder / 'before' / capture.name / 'sequence.csv').read_bytes() != (folder / 'after' / capture.name / 'sequence.csv').read_bytes():
                raise RuntimeError('Battle state differs between camera versions')
            for number, source in enumerate(frames):
                target = movie / f'{index * 150 + number:04d}.png'
                target.unlink(missing_ok=True)
                os.link(source, target)
    for name, count in [('combo-camera', 24), ('enemy-exchange', 21), ('threat-camera', 18)]:
        lines = (root / 'artifacts' / name / 'flow-validation.txt').read_text().splitlines()
        if len(lines) != count or any('passed' not in line for line in lines):
            raise RuntimeError('Incomplete camera interruption check: ' + name)
        proof[name] = lines
    combo = (root / 'artifacts/combo-camera/camera-validation.txt').read_text().splitlines()
    if len(combo) != 7 or any('headerOverlap=0' not in line for line in combo):
        raise RuntimeError('Missing combo framing regression')
    proof['combo-framing'] = combo
    player = json.loads((args.player / 'validation.json').read_text())
    guided = json.loads((args.guided / 'guided-validation.json').read_text())
    builds = json.loads((args.guided / 'build.json').read_text())
    if player['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Native round or hands-free replay failed')
    app = root / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        actual = hashlib.sha256((app / file).read_bytes()).hexdigest()
        if player[key] != actual or any(builds[stage][key] != actual for stage in ['before', 'after']):
            raise RuntimeError('Native evidence belongs to a different build')
    proof['player'], proof['guided'] = player, guided
    ffmpeg = ['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y']
    encoding = ['-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart']
    subprocess.run(ffmpeg + ['-framerate', '30', '-i', str(folder / 'before/movie/%04d.png'),
                            '-framerate', '30', '-i', str(folder / 'after/movie/%04d.png'),
                            '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]'] + encoding + [str(folder / 'comparison.mp4')], check=True)
    native = ''
    (folder / 'native').mkdir(exist_ok=True)
    for name, label in [('combo-camera-peak', '连击推进'), ('uppercut-recover', '追击恢复'), ('beam-contact', '必杀命中'), ('ground-uppercut-land', '腾空落地')]:
        shutil.copy2(args.player / 'native' / (name + '.png'), folder / 'native' / (name + '.png'))
        native += f'<figure><img loading="lazy" src="native/{name}.png"><figcaption>{label}</figcaption></figure>'
    (folder / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>战斗构图 · 完整动作与清楚引导</title><style>body{background:#101821;color:#edf3fa;margin:0;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b8c5d2}video,img{display:block;width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}figcaption{padding:8px}.labels{display:flex;justify-content:space-around;background:#273848;padding:10px}button{font:inherit;color:white;background:#254155;border:1px solid #4e6a81;border-radius:6px;padding:8px 14px;margin:10px 10px 0 0}a{color:#88e0ec}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>战斗构图 · 完整动作与清楚引导</h1><p>普通出拳、连击、腾空追击和必杀返回镜头都为脚步预留空间。底部动作提示压薄并下移，角色仍占画面高度约七成。动作识别、伤害和蓄能规则保持一致。</p>
<div class="labels"><b>修改前 · d7ef992</b><b>修改后 · 全身战斗构图</b></div><video id="comparison" src="comparison.mp4" controls loop playsinline></video>
<button onclick="document.querySelector('video').currentTime=0">普通出拳</button><button onclick="document.querySelector('video').currentTime=5">连击</button><button onclick="document.querySelector('video').currentTime=10">腾空追击</button><button onclick="document.querySelector('video').currentTime=15">必杀</button><button onclick="document.querySelector('video').playbackRate=.5">半速</button><button onclick="document.querySelector('video').playbackRate=1">正常速度</button>
<p>20 秒离线对照，四段各 5 秒；两侧输入与战斗状态逐帧一致。该视频不含 HUD，下面是真实游戏 HUD 的截图。离线采样 30 FPS，不代表运行帧率。</p>
<h2>实际 macOS 游戏画面</h2><div class="pair">'''+native+'''</div>
<p>五位英雄的 28 组蒙皮投影检查通过：普通战斗画面内未裁切角色，未进入顶部血量板、底部引导及取景窗口；专属特写单独保留。另验证 63 组镜头切换、暂停与打断，以及完整战斗、自动合照、重拍和下一局。</p>
<p>本轮改善构图和动作可读性。角色细节、动作种类与场景丰富度仍需继续提升；帧率专项后置。</p><a href="provenance.json">源码、构建和验证记录</a></main></html>''')
    print(folder / 'index.html')


if __name__ == '__main__':
    main()
