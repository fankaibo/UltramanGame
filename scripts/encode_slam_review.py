"""Check the sources and battle outcome before publishing the slam comparison."""
import argparse
import csv
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
    folder = root/'artifacts/monster-slam'
    baseline = 'c031254c2dafb0e162df48c6c184f5e23300a8a2'
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30, 'frames_per_side': 121}
    outcomes = []
    for version in ('before', 'after'):
        capture = folder/version
        if len(list(capture.glob('frame-*.png'))) != 121 or 'blocks=3' not in (capture/'validation.txt').read_text():
            raise RuntimeError('Incomplete continuous sequence '+version)
        for line in (capture/'sources.txt').read_text().splitlines():
            file, digest = line.rsplit(' ', 1)
            if version == 'after':
                data = (root/'unity'/file).read_bytes()
            elif file == 'Assets/Editor/MonsterSlamReview.cs':
                data = (capture/'MonsterSlamReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':unity/'+file], cwd=root)
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale capture source '+version+'/'+file)
            proof['sources'].append({'version': version, 'path': file, 'sha256': digest})
        with (capture/'motion.csv').open() as stream:
            outcomes.append([{key: row[key] for key in ('frame', 'phase', 'attack', 'age', 'blocks', 'hurt', 'health')}
                             for row in csv.DictReader(stream)])
    if outcomes[0] != outcomes[1]:
        raise RuntimeError('Battle inputs or results changed between captures')
    checks = (folder/'checks.txt').read_text()
    if checks.count(' passed\n') != 21 or checks.count('pool=passed') != 3:
        raise RuntimeError('Incomplete flow or effect checks')
    proof['checks'] = checks
    player = json.loads((args.player/'validation.json').read_text())
    guided = json.loads((args.guided/'guided-validation.json').read_text())
    build = json.loads((args.guided/'build.json').read_text())
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        digest = hashlib.sha256((app/path).read_bytes()).hexdigest()
        if player[key] != digest or build['before'][key] != digest or build['after'][key] != digest:
            raise RuntimeError('Native or guided proof is from another build')
    if player['result'] != 'passed' or not player['slam'] or player['ground_contacts'].count('slam') != 1:
        raise RuntimeError('Player did not complete the ground-slam round')
    if guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Hands-free photo and replay did not complete')
    proof.update(player=player, guided=guided, build=build)
    (folder/'native').mkdir(exist_ok=True)
    native = ''
    for name, label in [('slam-prepare', '抬起双爪'), ('slam-swing', '下压重心'), ('slam-ground', '砸地接触'),
                        ('slam-wave', '碎石冲向护盾'), ('slam-rise', '起身回到战斗')]:
        shutil.copy2(args.player/'native'/(name+'.png'), folder/'native'/(name+'.png'))
        native += f'<figure><figcaption>{label}</figcaption><img loading="lazy" src="native/{name}.png"></figure>'
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder/'before/frame-%03d.png'),
                    '-framerate', '30', '-i', str(folder/'after/frame-%03d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(folder/'comparison.mp4')], check=True)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>格尔赞双爪砸地 · 连续攻击对照</title><style>body{margin:0;background:#111822;color:#e5edf6;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b3c2d2}video,img{width:100%;display:block;border-radius:7px}figure{margin:0}figcaption{padding:8px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}.labels{display:flex;justify-content:space-around;background:#243443;padding:8px}button{font:inherit;background:#263e52;color:white;padding:8px 16px;border:1px solid #57738d;border-radius:6px;margin:8px}a{color:#72d3e6}input{width:100%}</style>
<main><h1>格尔赞双爪砸地</h1><p>前两次左右爪前冲，第三次抬起双爪、下蹲砸地、震起向前扩散的碎石和低空尘浪，再起身继续战斗。沿用原来的防御姿势、语音准备时间和轻度反击规则。</p>
<h2>同一次第三回合反击 · 修改前后</h2><p>左侧 c031254，右侧本轮版本。连续 121 帧，约 4 秒，战斗状态、血量和格挡结果一致。离线 30 FPS 用于动画对照，不代表游戏实时帧率。此对照视频无声。</p>
<div class="labels"><b>原来：再次单爪前冲</b><b>现在：双爪砸地</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速检查</button><button id="step">前进一帧</button><input id="scrub" aria-label="逐帧定位" type="range" min="0" max="120" step="1" value="0">
<h2>当前 macOS 打包版</h2><p>以下五帧来自同一轮实际应用运行。使用开发测试输入，未开启真人摄像头；包括未防御倒地、两次格挡、32 次普攻、两次大招、暂停恢复和胜利。另一次体感协议测试验证自动合照、重拍和举手再开局。</p><div class="grid">'''+native+'''</div>
<p><a href="checks.txt">姿态、打断和碎石检查</a> · <a href="provenance.json">源码与构建记录</a></p><p>本轮增加攻击变化和地面冲击演出。角色动作和镜头丰富度仍未达到参考街机视频的整体水平。</p>
<script>const movie=document.querySelector('#movie'),scrub=document.querySelector('#scrub');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});scrub.oninput=()=>{movie.pause();movie.currentTime=Number(scrub.value)/30};movie.ontimeupdate=()=>scrub.value=Math.min(120,Math.round(movie.currentTime*30));document.querySelector('#step').onclick=()=>{movie.pause();movie.currentTime=Math.min(4,movie.currentTime+1/30)};</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
