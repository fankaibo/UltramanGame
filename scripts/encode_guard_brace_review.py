"""Verify the captured sources and build a local full-body guard comparison."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', required=True, type=Path)
    parser.add_argument('--guided', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/guard-brace'
    baseline = '8beaffab3db8de93d38e000c53f6aa52c07476f1'
    reports = sorted((folder/'after').glob('*/validation.txt'))
    if len(reports) != 57:
        raise RuntimeError('Missing roster, rate or input-handoff cases')
    proof = {'baseline': baseline, 'reports': [], 'sources': [], 'offline_fps': 30,
             'comparison_seconds': 3, 'counterpunch_seconds': 3}
    for capture in [folder/'before/Tiga-30-hold'] + [p.parent for p in reports]:
        before = 'before' in capture.parts
        report = (capture/'validation.txt').read_text()
        if 'passed' not in report:
            raise RuntimeError('Failed capture '+str(capture))
        proof['reports'].append(report)
        for line in (capture/'sources.txt').read_text().splitlines():
            file, expected = line.rsplit(' ', 1)
            if not before:
                data = (root/'unity/Assets'/file).read_bytes()
            elif file == 'Editor/GuardBraceReview.cs':
                data = (folder/'before/GuardBraceReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':unity/Assets/'+file], cwd=root)
            if hashlib.sha256(data).hexdigest() != expected:
                raise RuntimeError('Stale capture '+str(capture)+' '+file)
            proof['sources'].append({'capture': str(capture.relative_to(folder)), 'path': file, 'sha256': expected})
    if (folder/'before/Tiga-30-hold/sequence.csv').read_bytes() != (folder/'after/Tiga-30-hold/sequence.csv').read_bytes():
        raise RuntimeError('Combat clock or result changed')
    proof['shield_regression'] = (folder/'shield-regression/validation.txt').read_text()
    if len(proof['shield_regression'].splitlines()) != 10:
        raise RuntimeError('Missing shield reflection/settle regression')
    for line in (folder/'shield-regression/render-source.txt').read_text().splitlines()[2:]:
        file, digest = line.rsplit(' ', 1)
        if hashlib.sha256((root/'unity/Assets'/file).read_bytes()).hexdigest() != digest:
            raise RuntimeError('Shield regression source changed: '+file)
    player = json.loads((args.player/'validation.json').read_text())
    guided = json.loads((args.guided/'guided-validation.json').read_text())
    builds = json.loads((args.guided/'build.json').read_text())
    if player['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Playable round or photo replay did not pass')
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        digest = hashlib.sha256((app/name).read_bytes()).hexdigest()
        if player[key] != digest or any(builds[stage][key] != digest for stage in ('before', 'after')):
            raise RuntimeError('Player/guided evidence belongs to another build')
    proof['player'], proof['guided'] = player, guided
    ffmpeg = ['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y']
    for name in ['before/Tiga-30-hold', 'after/Tiga-30-hold', 'after/Tiga-30-left']:
        if len(list((folder/name/'frames').glob('*.png'))) != 90:
            raise RuntimeError('Incomplete three-second film: '+name)
    encoding = ['-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart']
    subprocess.run(ffmpeg + ['-framerate', '30', '-i', str(folder/'before/Tiga-30-hold/frames/%04d.png'),
                            '-framerate', '30', '-i', str(folder/'after/Tiga-30-hold/frames/%04d.png'),
                            '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]'] + encoding + [str(folder/'comparison.mp4')], check=True)
    subprocess.run(ffmpeg + ['-framerate', '30', '-i', str(folder/'after/Tiga-30-left/frames/%04d.png')] + encoding + [str(folder/'counterpunch.mp4')], check=True)
    shutil.copy2(args.player/'native/guard-impact.png', folder/'native-guard.png')
    roster = ''.join(f'<figure><img loading="lazy" src="after/{id}-30-hold/impact.png"><figcaption>{label}</figcaption></figure>'
                     for id, label in [('Tiga', '迪迦'), ('Mebius', '梦比优斯'), ('Zero', '赛罗'), ('Geed', '捷德'), ('Grigio', '格力乔')])
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>格挡承重与反击</title><style>body{background:#101821;color:#ecf2f9;margin:0;font:16px/1.7 system-ui}main{max-width:1440px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b8c6d4}video,img{display:block;width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}figcaption{padding:8px}.labels{display:flex;justify-content:space-around;background:#263848;padding:10px}button{font:inherit;background:#273f53;color:white;border:1px solid #536d82;border-radius:6px;padding:8px 14px;margin:10px 8px 0 0}a{color:#88dbe7}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>格挡承重与反击</h1><p>护盾挡住挥爪后，胸口先吸收冲击，腰腿随后屈膝、后移，再恢复站姿。双脚保持着地；立即出拳或放光线时，承重动作自然退去，输入照常生效。</p>
<h2>同一段攻击 · 修改前后</h2><p>左侧为 8beaffa，右侧为本轮修改，战斗时钟及结果逐帧一致。每段为 3 秒离线渲染，30 FPS 仅为对照视频的采样率。</p>
<div class="labels"><b>修改前 · 腰腿反应较少</b><b>修改后 · 全身承重</b></div>
<video id="comparison" src="comparison.mp4" controls loop playsinline></video><button onclick="document.querySelector('#comparison').playbackRate=1">正常速度</button><button onclick="document.querySelector('#comparison').playbackRate=.5">半速观察</button>
<h2>挡住后立刻反击</h2><video src="counterpunch.mp4" controls loop playsinline></video>
<h2>五位角色</h2><div class="pair">'''+roster+'''</div>
<h2>实际游戏构建</h2><img src="native-guard.png"><p>使用合成动作完成实战、自动合照、重拍和下一局检查，未使用真实摄像头照片。五角色承重、松手、左右反击、大招、暂停与新局共 57 组检查通过，另复查护盾反光及恢复。</p>
<p>这次改善格挡动作的重量感；动作种类、角色材质和场景丰富度仍有提升空间，整体尚未达到参考街机效果。</p><a href="provenance.json">查看来源、验证和构建记录</a></main></html>''')
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    print(folder/'index.html')


if __name__ == '__main__':
    main()
