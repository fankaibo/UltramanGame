"""Build a source-checked uppercut comparison and full left/right sequences."""
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
    folder = root/'artifacts/uppercut'
    baseline = '1277b59e95deed0538a6c5b10d63d870c2f95602'
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30,
             'comparison_seconds': 7, 'complete_sequence_seconds': 8}
    for version in ('before', 'after'):
        capture = folder/version
        if 'passed' not in (capture/'validation.txt').read_text():
            raise RuntimeError('Incomplete combo capture')
        if len(list((capture/'frames').glob('*.png'))) != 210:
            raise RuntimeError('Missing comparison frames')
        for line in (capture/'sources.txt').read_text().splitlines():
            file, expected = line.rsplit(' ', 1)
            data = ((root/'unity/Assets'/file).read_bytes() if version == 'after'
                    else subprocess.check_output(['git', 'show', baseline+':unity/Assets/'+file], cwd=root))
            if hashlib.sha256(data).hexdigest() != expected:
                raise RuntimeError('Stale capture '+version+'/'+file)
            proof['sources'].append({'version': version, 'path': file, 'sha256': expected})
    if (folder/'before/sequence.csv').read_bytes() != (folder/'after/sequence.csv').read_bytes():
        raise RuntimeError('Combat rules or clock changed')
    for line in (folder/'validation-sources.txt').read_text().splitlines():
        file, digest = line.rsplit(' ', 1)
        if hashlib.sha256((root/'unity/Assets'/file).read_bytes()).hexdigest() != digest:
            raise RuntimeError('Launch validation is stale')
    validation = (folder/'launch-validation.txt').read_text().splitlines()
    if len(validation) != 40 or any('passed' not in row for row in validation):
        raise RuntimeError('Incomplete airborne/interrupt/rapid-follow-up validation')
    proof['validation'] = validation
    proof['combo_contacts'] = (folder/'validation/validation.txt').read_text()
    proof['guard_interruptions'] = (folder/'validation/interruptions.txt').read_text()
    proof['backstep'] = (root/'artifacts/monster-backstep/step-validation.txt').read_text()
    if len(proof['combo_contacts'].splitlines()) != 7 or len(proof['guard_interruptions'].splitlines()) != 10 or len(proof['backstep'].splitlines()) != 7:
        raise RuntimeError('Missing contact or recovery regression')
    player = json.loads((args.player/'validation.json').read_text())
    guided = json.loads((args.guided/'guided-validation.json').read_text())
    builds = json.loads((args.guided/'build.json').read_text())
    if player['result'] != 'passed' or player['launch_landings'] < 1 or guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Native round or guided replay failed')
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        digest = hashlib.sha256((app/name).read_bytes()).hexdigest()
        if player[key] != digest or any(builds[stage][key] != digest for stage in ('before', 'after')):
            raise RuntimeError('Evidence belongs to a different build')
    proof['player'], proof['guided'] = player, guided
    ffmpeg = ['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y']
    encoding = ['-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart']
    subprocess.run(ffmpeg + ['-framerate', '30', '-i', str(folder/'before/frames/%04d.png'),
                            '-framerate', '30', '-i', str(folder/'after/frames/%04d.png'),
                            '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]'] + encoding + [str(folder/'comparison.mp4')], check=True)
    movie = folder/'movie';movie.mkdir(exist_ok=True)
    for side, offset in [('left', 0), ('right', 120)]:
        frames = sorted((folder/'shots'/side/'frames').glob('*.png'))
        if len(frames) != 120:
            raise RuntimeError('Incomplete full uppercut sequence')
        for number, source in enumerate(frames):
            destination = movie/f'{offset+number:04d}.png'
            destination.unlink(missing_ok=True);os.link(source, destination)
    subprocess.run(ffmpeg + ['-framerate', '30', '-i', str(movie/'%04d.png')] + encoding + [str(folder/'uppercuts.mp4')], check=True)
    native = ''
    (folder/'native').mkdir(exist_ok=True)
    for name, label in [('uppercut-airborne', '腾空'), ('uppercut-land', '落地'), ('uppercut-recover', '恢复'), ('ground-uppercut-land', '地面扬尘')]:
        shutil.copy2(args.player/'native'/(name+'.png'), folder/'native'/(name+'.png'))
        native += f'<figure><img loading="lazy" src="native/{name}.png"><figcaption>{label}</figcaption></figure>'
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>上挑重击 · 腾空与落地</title><style>body{background:#101821;color:#edf3fa;margin:0;font:16px/1.7 system-ui}main{max-width:1440px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b8c5d2}video,img{display:block;width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}figcaption{padding:8px}.labels{display:flex;justify-content:space-around;background:#273848;padding:10px}button{font:inherit;color:white;background:#254155;border:1px solid #4e6a81;border-radius:6px;padding:8px 14px;margin:10px 10px 0 0}a{color:#88e0ec}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>上挑重击 · 腾空与落地</h1><p>每十次普攻，角色用上挑拳将格尔赞短暂打离地面。怪兽屈膝落地、扬尘，再分两步回到站位。正在空中时仍可追击，角色会跨步接近目标；普通伤害和蓄能规则保持一致。</p>
<h2>左右上挑的完整连续动作</h2><video id="full" src="uppercuts.mp4" controls loop playsinline></video>
<button onclick="document.querySelector('#full').currentTime=0">左手</button><button onclick="document.querySelector('#full').currentTime=4">右手</button><button onclick="document.querySelector('#full').playbackRate=.5">半速</button><button onclick="document.querySelector('#full').playbackRate=1">正常速度</button>
<p>两段独立演示，各 4 秒。包含命中、腾空、落地、踏步恢复与回到战斗姿态。</p>
<h2>十拳连击前后对照</h2><div class="labels"><b>修改前 · 1277b59</b><b>修改后 · 第十拳上挑</b></div><video src="comparison.mp4" controls loop playsinline></video>
<p>同一组输入、210 帧，伤害、能量和战斗时钟逐字一致。末尾可看到第十拳的变化；完整恢复过程见上方视频。离线视频为 30 FPS，不代表游戏运行帧率。</p>
<h2>实际 macOS 游戏</h2><div class="pair">'''+native+'''</div>
<p>当前构建完成完整实战及无键鼠自动合照、重拍和下一局验证。使用合成动作和测试照片，未保存真人图像。</p>
<p>本轮增加重击变化与腾空追击。角色细节、更多攻击动作和场景丰富度仍需提升；普通镜头回归后的底部引导与角色脚部间距也需继续优化，不能据此宣称达到最终街机效果。</p><a href="provenance.json">源码、构建与验证记录</a></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
