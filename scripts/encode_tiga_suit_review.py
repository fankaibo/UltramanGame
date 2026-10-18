"""Publish the source-checked costume comparison and current native proof."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, required=True)
    parser.add_argument('--guided', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    out = root/'artifacts/tiga-suit'
    baseline = '8c9e449371c1b2d4f16af0393065b710f9d5ae92'
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30, 'duration': 4.2}
    for version in ('before', 'after'):
        sources = json.loads((out/version/'runtime-source.json').read_text())
        for record in (out/version/'sources.txt', out/'arena'/version/'render-source.txt'):
            for line in record.read_text().splitlines():
                if line.startswith(('UTC:', 'Unity:')):
                    continue
                name, expected = line.rsplit(' ', 1)
                sources['unity/Assets/'+name] = expected
        for path, expected in sources.items():
            if version == 'after':
                data = (root/path).read_bytes()
            elif path == 'unity/Assets/Editor/HeroMaterialReview.cs':
                data = (out/'before/HeroMaterialReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':'+path], cwd=root)
            if hashlib.sha256(data).hexdigest() != expected:
                raise RuntimeError('Stale source: '+version+'/'+path)
        proof['sources'].append({'version': version, 'sha256': sources})
        if len(list((out/'arena'/version/'frames').glob('*.png'))) != 126:
            raise RuntimeError('Incomplete continuous sequence')
        if len(list((out/version).glob('*-*-front.png'))) != 15:
            raise RuntimeError('Incomplete hero comparison')
    if (out/'arena/before/sequence.csv').read_bytes() != (out/'arena/after/sequence.csv').read_bytes():
        raise RuntimeError('Before/after action clocks or beam origin changed')
    inspection = (out/'inspection/validation.txt').read_text()
    if 'emission-preserved=passed' not in inspection or inspection.count('original material configuration preserved') != 4:
        raise RuntimeError('Incomplete material checks')
    bake = json.loads((out/'bake/TigaBodyOcclusion.json').read_text())
    if bake['output_sha256'] != digest(root/'unity/Assets/Resources/Characters/Tiga/TigaBodyOcclusion.png'):
        raise RuntimeError('Imported occlusion differs from baked data')
    player = json.loads((args.player/'validation.json').read_text())
    guided = json.loads((args.guided/'guided-validation.json').read_text())
    build = json.loads((args.guided/'build.json').read_text())
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        actual = digest(app/name)
        if player[key] != actual or build['before'][key] != actual or build['after'][key] != actual:
            raise RuntimeError('Native proof belongs to a different build')
    if player['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Full battle/photo/replay incomplete')
    if guided['photo_preview_p99_error'] > 2 or guided['gesture_wobble']['unwanted_attacks'] != 0:
        raise RuntimeError('Photo color or protected pose regression')
    proof.update(bake=bake, inspection=inspection, player=player, guided=guided, build=build)
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(out/'arena/before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(out/'arena/after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(out/'comparison.mp4')], check=True)
    native = ''
    (out/'native').mkdir(exist_ok=True)
    for name, label in [('battle', '普通出拳'), ('guard-impact', '护盾接触'), ('beam-closeup-peak', '必杀近景'), ('victory-hero', '胜利庆祝')]:
        shutil.copy2(args.player/'native'/(name+'.png'), out/'native'/(name+'.png'))
        native += f'<figure><figcaption>{label}</figcaption><img loading="lazy" src="native/{name}.png"></figure>'
    (out/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (out/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>迪迦战衣 · 材质对照</title><style>body{background:#111822;color:#e5edf6;font:16px/1.7 system-ui;margin:0}main{max-width:1480px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{color:#b3c2d2;max-width:1100px}.pair,.native{display:grid;grid-template-columns:1fr 1fr;gap:12px}img,video{width:100%;display:block;border-radius:8px}figure{margin:0}figcaption{padding:8px}button,select{font:inherit;color:#e5edf6;background:#263e52;padding:8px 14px;border:1px solid #57738d;border-radius:6px;margin:6px}a{color:#72d3e6}label{display:inline-block}</style>
<main><h1>迪迦战衣的颜色、表面与身体凹处</h1><p>原图案位置和模型不变：红紫战衣减少纯色塑料感，银色保持独立高光；遮蔽图从原模型烘焙，让身体凹处产生局部环境阴影。细微织纹随 UV 贴在战衣上，远处淡出。</p>
<h2>相同动作与镜头 · 修改前后</h2><p>左：8c9e449；右：本轮版本。每侧 126 帧、4.2 秒，动作、伤害和光线起点记录一致。离线 30 FPS、无声，不代表实际游戏帧率。</p><div class="pair"><b>原来</b><b>现在</b></div><video id="clip" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速检查</button>
<h2>角色正背面</h2><p>此组使用固定材质检查镜头，下缘有裁切。迪迦启用新战衣参数，另外四位英雄保留原材质配置，用于检查共用着色器是否受到影响。</p>
<label>角色 <select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select></label><label>姿势 <select id="pose"><option value="0">准备</option><option value="3">防御</option><option value="4">必杀</option></select></label><label>角度 <select id="angle"><option value="front">正面</option><option value="back">背面</option></select></label><div class="pair"><img id="before" alt="原材质"><img id="after" alt="新材质"></div>
<h2>当前打包版 · 实际游戏</h2><p>来自本次 macOS 玩家的完整战斗截图。输入为开发回放，没有拍摄真人；另行完成体感协议抖动、自动合照、重拍、断流恢复和举手再开局检查。</p><div class="native">'''+native+'''</div><p><a href="inspection/validation.txt">材质与发光检查</a> · <a href="guard/validation.txt">五英雄护盾</a> · <a href="provenance.json">源码、烘焙及构建证据</a></p><p>本轮改善迪迦身体表面，未增加模型细节或新的动作；整体街机品质仍未完成。</p>
<script>const hero=document.querySelector('#hero'),pose=document.querySelector('#pose'),angle=document.querySelector('#angle');function update(){for(const v of ['before','after'])document.getElementById(v).src=v+'/'+hero.value+'-'+pose.value+'-'+angle.value+'.png'}for(const e of [hero,pose,angle])e.onchange=update;update();const clip=document.querySelector('#clip');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{clip.playbackRate=Number(b.dataset.speed);clip.play()});</script></main></html>''')
    print(out/'index.html')


if __name__ == '__main__':
    main()
