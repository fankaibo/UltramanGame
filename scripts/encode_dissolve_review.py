"""Publish a source-checked continuous departure comparison and native proof."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, required=True)
    parser.add_argument('--guided', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/monster-dissolve'
    baseline = '331af579da301f370c495a7cf2749825d0db2991'
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30, 'frames_per_side': 195}
    for version in ('before', 'after'):
        capture = folder/version
        if len(list((capture/'frames').glob('*.png'))) != 195 or 'victory=1 landings=1' not in (capture/'validation.txt').read_text():
            raise RuntimeError('Incomplete continuous sequence '+version)
        for line in (capture/'render-source.txt').read_text().splitlines():
            if line.startswith(('UTC:', 'Unity:')):
                continue
            file, digest = line.rsplit(' ', 1)
            if version == 'after':
                data = (root/'unity/Assets'/file).read_bytes()
            elif file == 'Editor/VictoryReview.cs':
                data = (capture/'VictoryReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':unity/Assets/'+file], cwd=root)
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale capture source '+version+'/'+file)
            proof['sources'].append({'version': version, 'path': file, 'sha256': digest})
    if (folder/'before/motion.csv').read_bytes() != (folder/'after/motion.csv').read_bytes():
        raise RuntimeError('Victory pose or camera changed between captures')
    checks = (folder/'checks.txt').read_text()
    if checks.count('fully-cleared=passed') != 2 or checks.count('photo-clear=passed') != 3 or 'finite=passed' not in checks:
        raise RuntimeError('GPU/flow/audio checks incomplete')
    roster = (folder/'after/roster-validation.txt').read_text()
    if any(name+':' not in roster for name in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio')):
        raise RuntimeError('Five-hero staging proof incomplete')
    player = json.loads((args.player/'validation.json').read_text())
    guided = json.loads((args.guided/'guided-validation.json').read_text())
    build = json.loads((args.guided/'build.json').read_text())
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        digest = hashlib.sha256((app/path).read_bytes()).hexdigest()
        if player[key] != digest or build['before'][key] != digest or build['after'][key] != digest:
            raise RuntimeError('Native proof belongs to another build')
    log = (args.player/'player.log').read_text()
    if player['result'] != 'passed' or log.count('[MonsterDissolve] begin samples=384') != 1 or log.count('[MonsterDissolve] shimmer playing=True') != 1:
        raise RuntimeError('Native departure or sound was missing/duplicated')
    if guided['result'] != 'passed' or not guided['replay_battle_started'] or guided['photo_preview_p99_error'] > 2:
        raise RuntimeError('Hands-free photo/replay or preview color did not pass')
    proof.update(checks=checks, roster=roster, player=player, guided=guided, build=build)
    native = '';(folder/'native').mkdir(exist_ok=True)
    for name, label in [('victory-dissolve', '沿真实模型消散'), ('victory-motes', '光粒向上散开'), ('victory-hero', '接回英雄庆祝')]:
        shutil.copy2(args.player/'native'/(name+'.png'), folder/'native'/(name+'.png'))
        native += f'<figure><figcaption>{label}</figcaption><img loading="lazy" src="native/{name}.png"></figure>'
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder/'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder/'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(folder/'comparison.mp4')], check=True)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>格尔赞发光消散 · 胜利收尾</title><style>body{margin:0;background:#111822;color:#e5edf6;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b3c2d2}video,img{width:100%;display:block;border-radius:7px}figure{margin:0}figcaption{padding:8px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}.labels{display:flex;justify-content:space-around;background:#243443;padding:8px}button{font:inherit;background:#263e52;color:white;padding:8px 16px;border:1px solid #57738d;border-radius:6px;margin:8px}a{color:#72d3e6}input{width:100%}</style>
<main><h1>格尔赞发光消散</h1><p>保留失衡落地与英雄转身，退场改为从身体表面逐步消散，光粒从实际皮肤位置升起。眼睛和接触阴影同步清除，随后进入自动合照。</p>
<h2>同一胜利动作 · 修改前后</h2><p>左侧 331af57：透明淡出。右侧本轮：发光边缘与光粒消散。每侧 195 帧，6.5 秒，骨骼动作、脚底高度和镜头相同。离线 30 FPS 不代表实际游戏帧率；此对照视频无声。</p><div class="labels"><b>原来：透明淡出</b><b>现在：沿身体消散</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速检查</button><button id="step">前进一帧</button><input id="scrub" aria-label="逐帧定位" type="range" min="0" max="194" step="1" value="0">
<h2>退场声音</h2><p>本地合成的轻声上扬音效，游戏里以较低音量配合原有胜利语音。这里可单独试听。</p><audio controls src="departure-shimmer.wav"></audio>
<h2>当前 macOS 打包版</h2><p>这些画面来自完整实战回放；输入由开发测试提供，没有开启真人摄像头。另一次体感协议回放验证了防御／大招抖动、自动合照、重拍及举手再开局。</p><div class="grid">'''+native+'''</div><p><a href="checks.txt">GPU、重开、光粒清理与音频检查</a> · <a href="after/roster-validation.txt">五英雄构图</a> · <a href="provenance.json">源码与构建记录</a></p><p>本轮补上胜利收尾的消散演出。整场动作丰富度、镜头节奏和角色细节仍需继续向参考街机提升。</p>
<script>const movie=document.querySelector('#movie'),scrub=document.querySelector('#scrub');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});scrub.oninput=()=>{movie.pause();movie.currentTime=Number(scrub.value)/30};movie.ontimeupdate=()=>scrub.value=Math.min(194,Math.round(movie.currentTime*30));document.querySelector('#step').onclick=()=>{movie.pause();movie.currentTime=Math.min(194/30,movie.currentTime+1/30)};</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
