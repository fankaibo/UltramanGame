"""Source-checked lunge/guard/fall comparison with actual player and guided evidence."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', required=True, type=Path)
    parser.add_argument('--guided', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/enemy-exchange'
    baseline = '084a611e76dac15260458363fb7062673a2315b4'
    segments = [f'Tiga-30-{side}-{mode}' for side in (1, 2) for mode in ('guard', 'hurt')]
    proof = {'baseline': baseline, 'segments': segments, 'offline_fps': 30, 'segment_seconds': 4,
             'sources': [], 'framing': [], 'flow': (folder/'flow-validation.txt').read_text()}
    if len(proof['flow'].splitlines()) != 21 or any('passed' not in s for s in proof['flow'].splitlines()):
        raise RuntimeError('Camera cancellation validation incomplete')
    captures = [('before', folder/'before'/name) for name in segments]
    after = sorted((folder/'after').glob('*/validation.txt'))
    if len(after) != 28:
        raise RuntimeError('Missing roster/side/guard/rate combinations')
    captures += [('after', p.parent) for p in after]
    for version, capture in captures:
        report = (capture/'validation.txt').read_text()
        if 'passed' not in report:
            raise RuntimeError('Incomplete capture '+str(capture))
        if version == 'after' and ('headerOverlaps=0' not in report or 'guideAttackOverlaps=0' not in report):
            raise RuntimeError('HUD obscures an actor')
        proof['framing'].append({'version': version, 'report': report})
        for line in (capture/'sources.txt').read_text().splitlines():
            file, digest = line.rsplit(' ', 1)
            if version == 'after':
                data = (root/'unity/Assets'/file).read_bytes()
            elif file == 'Editor/EnemyExchangeReview.cs':
                data = (folder/'before/EnemyExchangeReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':unity/Assets/'+file], cwd=root)
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale source '+version+'/'+file)
            proof['sources'].append({'version': version, 'path': file, 'sha256': digest})
    for name in segments:
        if (folder/'before'/name/'sequence.csv').read_bytes() != (folder/'after'/name/'sequence.csv').read_bytes():
            raise RuntimeError('Combat clock or outcomes changed: '+name)
    for version in ('before', 'after'):
        output = folder/'movie'/version
        output.mkdir(parents=True, exist_ok=True)
        frame = 0
        for name in segments:
            images = sorted((folder/version/name/'frames').glob('*.png'))
            if len(images) != 120:
                raise RuntimeError('Incomplete 4-second segment')
            for source in images:
                destination = output/f'{frame:04d}.png'
                if destination.exists():
                    destination.unlink()
                os.link(source, destination)
                frame += 1
    record = json.loads((args.player/'validation.json').read_text())
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if hashlib.sha256((app/file).read_bytes()).hexdigest() != record[key]:
            raise RuntimeError('Native player changed after verification')
    if record['result'] != 'passed':
        raise RuntimeError('Native round did not pass')
    proof['player'] = record
    if args.guided:
        guided = json.loads((args.guided/'guided-validation.json').read_text())
        builds = json.loads((args.guided/'build.json').read_text())
        if guided['result'] != 'passed' or not guided['replay_battle_started']:
            raise RuntimeError('Guided round did not restart after the photo')
        for stage in ('before', 'after'):
            for key in ('assembly_sha256', 'resources_sha256'):
                if builds[stage][key] != record[key]:
                    raise RuntimeError('Guided proof belongs to another build')
        proof['guided'] = guided
    native = ''
    (folder/'native').mkdir(exist_ok=True)
    for name, label in [('monster-threat', '蓄力近景'), ('monster-rush-left', '挥爪接触'),
                        ('guard-impact', '挡住攻击'), ('hero-landed', '受击倒地')]:
        shutil.copy2(args.player/'native'/(name+'.png'), folder/'native'/(name+'.png'))
        native += f'<figure><figcaption>{label}</figcaption><img loading="lazy" src="native/{name}.png"></figure>'
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder/'movie/before/%04d.png'),
                    '-framerate', '30', '-i', str(folder/'movie/after/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(folder/'comparison.mp4')], check=True)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>怪兽前冲 · 格挡与倒地镜头</title><style>body{background:#101821;color:#edf3fa;margin:0;font:16px/1.7 system-ui}main{max-width:1480px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b7c4d0}video,img{width:100%;display:block;border-radius:7px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:0}figcaption{padding:8px}.labels{display:flex;justify-content:space-around;background:#223547;padding:9px}button{font:inherit;padding:7px 14px;border:1px solid #536f85;border-radius:6px;background:#253b4b;color:white;margin:8px 8px 8px 0}a{color:#79d9e8}@media(max-width:650px){.pair{grid-template-columns:1fr}}</style>
<main><h1>怪兽前冲 · 格挡与倒地镜头</h1><p>攻击前提前留出构图空间，随后镜头跟随挥爪侧移。头部避开顶部状态栏，脚部和地面接触避开引导条；未防御时保留倒地与起身，随后平顺回到普通战斗。</p>
<h2>相同输入、相同战斗时钟</h2><p>左边为 084a611，右边为本轮修改。四段各 4 秒：第一侧挥爪格挡、第一侧受击、另一侧格挡、另一侧受击；不是一次完整战斗。每侧共 480 帧，战斗状态逐字一致。离线 30 FPS 不代表游戏运行帧率。</p>
<div class="labels"><b>修改前</b><b>修改后</b></div><video id="film" src="comparison.mp4" controls loop playsinline></video>
<button data-time="0">第一侧 · 格挡</button><button data-time="4">第一侧 · 倒地</button><button data-time="8">另一侧 · 格挡</button><button data-time="12">另一侧 · 倒地</button>
<h2>实际 macOS 游戏</h2><p>当前构建的合成输入实战截图，保留真实 HUD。</p><div class="pair">'''+native+'''</div>
<h2>检查范围</h2><p>五位英雄、两侧挥爪、格挡／受击，共 28 组构图采样；迪迦另覆盖 15、30、60 Hz。顶部面板与攻击时底部引导区域未发现模型顶点侵入。暂停、提示打断／延长、必杀、展示和新局共 21 组镜头退出检查。</p>
<p>'''+('同一构建的无键鼠流程完成自动合照、重拍、预览和第二局；测试只使用合成人物与姿势。' if args.guided else '本页面尚未附加当前构建的自动合照验证。')+'''</p>
<p>本轮改善防御交锋的可读性；角色动作种类和完整街机演出仍需继续提升。</p><p><a href="provenance.json">源码与构建记录</a> · <a href="flow-validation.txt">中断与回位检查</a></p>
<script>const film=document.querySelector('#film');document.querySelectorAll('[data-time]').forEach(button=>button.onclick=()=>{film.currentTime=Number(button.dataset.time);film.play()});</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
