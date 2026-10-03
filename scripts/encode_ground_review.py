"""Source-checked ground-contact comparison and current-player evidence."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path)
    parser.add_argument('--guided', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/ground-impact'
    baseline = '1018b8edd3b27e8a9125bb6b9fa83cc59e67dd6a'
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30, 'segments': ['rush 3s', 'stagger 4s', 'defeat 3s']}
    for version in ('before', 'after'):
        capture = folder/version
        if len(list((capture/'frames').glob('*.png'))) != 300 or 'passed' not in (capture/'validation.txt').read_text():
            raise RuntimeError('Incomplete contact sequence '+version)
        for line in (capture/'sources.txt').read_text().splitlines():
            file, digest = line.rsplit(' ', 1)
            if version == 'after':
                data = (root/'unity/Assets'/file).read_bytes()
            elif file == 'Editor/GroundImpactReview.cs':
                data = (capture/'GroundImpactReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':unity/Assets/'+file], cwd=root)
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale source '+version+'/'+file)
            proof['sources'].append({'version': version, 'path': file, 'sha256': digest})
    if (folder/'before/sequence.csv').read_bytes() != (folder/'after/sequence.csv').read_bytes():
        raise RuntimeError('Battle inputs or results differ')
    for name in ('effect-validation.txt', 'roster-validation.txt', 'audio-validation.txt'):
        proof[name] = (folder/name).read_text()
        if 'passed' not in proof[name]:
            raise RuntimeError('Missing validation '+name)
    native = ''
    if args.player:
        record = json.loads((args.player/'validation.json').read_text())
        app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
        for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
            if hashlib.sha256((app/path).read_bytes()).hexdigest() != record[key]:
                raise RuntimeError('Native proof is from another build')
        if record['result'] != 'passed' or not {'rush', 'hero-land', 'stagger', 'defeat'}.issubset(record['ground_contacts']):
            raise RuntimeError('Native contact events not exercised')
        proof['player'] = record
        (folder/'native').mkdir(exist_ok=True)
        for event, label in [('rush', '怪兽重踏'), ('hero-land', '英雄倒地'), ('stagger', '连击后撤'), ('defeat', '胜利落地')]:
            name = 'ground-'+event+'.png'
            shutil.copy2(args.player/'native'/name, folder/'native'/name)
            native += '<figure><figcaption>'+label+'</figcaption><img loading="lazy" src="native/'+name+'"></figure>'
    if args.guided:
        if not args.player:
            raise RuntimeError('Guided validation requires a current player build')
        guided = json.loads((args.guided/'guided-validation.json').read_text())
        build = json.loads((args.guided/'build.json').read_text())
        for stage in ('before', 'after'):
            for key in ('assembly_sha256', 'resources_sha256'):
                if build[stage][key] != record[key]:
                    raise RuntimeError('Guided evidence is from another build')
        if guided['result'] != 'passed' or not guided['replay_battle_started']:
            raise RuntimeError('Guided loop did not pass')
        proof['guided'] = guided
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder/'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder/'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(folder/'comparison.mp4')], check=True)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>重踏、倒地与碎石尘浪</title><style>body{margin:0;background:#111822;color:#e5edf6;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b3c2d2}video,img{width:100%;display:block;border-radius:7px}figure{margin:0}figcaption{padding:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.labels{display:flex;justify-content:space-around;background:#243443;padding:8px}button,select{font:inherit;background:#263e52;color:white;padding:8px 16px;border:1px solid #57738d;border-radius:6px;margin:8px}a{color:#72d3e6}</style>
<main><h1>重踏、倒地与碎石尘浪</h1><p>从实际落脚和倒地位置弹出有厚度的岩石碎块，短暂翻滚、回弹后消退；低空尘浪向外扩散，保留上身动作。碎石散落声与接触事件同步。</p>
<h2>相同输入 · 修改前后</h2><p>左侧为 1018b8e，右侧为本轮修改。视频依次展示三段连续动作：怪兽重踏与英雄倒地（3 秒）、连击后撤（4 秒）、胜利落地（3 秒）。两侧各 300 帧，600 个战斗状态一致。离线 30 FPS 不代表游戏运行帧率。</p>
<div class="labels"><b>修改前</b><b>修改后</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速看落地</button>
<h2>五英雄倒地接触</h2><select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select><img id="roster" src="roster/Tiga.png">
<h2>碎石声音层</h2><p>本地合成的短促碎裂与回落声，游戏里会降低音量并与原有落地低音混合。此处可单独试听。</p><audio controls src="ground-crunch.wav"></audio>
<h2>当前 macOS 打包版</h2><p>合成输入完整实战回放，未开启真人摄像头。</p><div class="pair">'''+(native or '<p>打包版证据尚未附加。</p>')+'''</div>
<p><a href="provenance.json">源码与构建记录</a> · <a href="effect-validation.txt">穿地、遮挡、时钟与清理验证</a> · <a href="roster-validation.txt">五英雄验证</a></p><p>本轮增强地面接触的重量感；模型细节、攻击动作的丰富度和完整演出仍需继续提升。</p>
<script>const movie=document.querySelector('#movie');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});document.querySelector('#hero').onchange=e=>document.querySelector('#roster').src='roster/'+e.target.value+'.png';</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
