"""Publish the source-checked Golza material comparison and current-player proof."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/kaiju-skin'
    proof = {'comparison': 'same current runtime, original material override versus runtime HD material',
             'offline_fps': 30, 'duration_seconds': 4, 'sources': []}
    for version in ('original', 'after'):
        capture = folder/version
        if len(list((capture/'frames').glob('*.png'))) != 120 or 'passed' not in (capture/'validation.txt').read_text():
            raise RuntimeError('Incomplete material comparison: '+version)
        for line in (capture/'sources.txt').read_text().splitlines():
            path, digest = line.rsplit(' ', 1)
            if hashlib.sha256((root/'unity/Assets'/path).read_bytes()).hexdigest() != digest:
                raise RuntimeError('Stale comparison source: '+path)
            proof['sources'].append({'version': version, 'path': path, 'sha256': digest})
        proof[version] = (capture/'validation.txt').read_text()
    if (folder/'original/poses.txt').read_bytes() != (folder/'after/poses.txt').read_bytes():
        raise RuntimeError('The two materials changed bone poses')
    impacts = [f'{hero}-{action}' for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio')
               for action in ('left', 'right', 'beam')]+['Tiga-left-interrupted', 'Tiga-beam-interrupted']
    proof['surface_impact'] = {}
    for name in impacts:
        report = (folder/'surface-impact'/(name+'.txt')).read_text()
        if 'decay=passed' not in report or 'zeroTime=passed' not in report:
            raise RuntimeError('Missing impact validation: '+name)
        proof['surface_impact'][name] = report
    # Compare to the actual pre-edit capture, not just the current override.
    baseline = '01cb811eb8d7cdb704710570e0b1a46206623241'
    for path in ('Golza.fbx', 'GolzaBody.png', 'GolzaEyes.png'):
        rel = 'unity/Assets/Resources/Characters/Golza/'+path
        if (root/rel).read_bytes() != subprocess.check_output(['git', 'show', baseline+':'+rel], cwd=root):
            raise RuntimeError('Original model or texture changed: '+path)
    record = json.loads((args.player/'validation.json').read_text())
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if hashlib.sha256((app/path).read_bytes()).hexdigest() != record[key]:
            raise RuntimeError('Native evidence is from another build')
    if record['result'] != 'passed' or record['camera_used']:
        raise RuntimeError('Full synthetic player round did not pass')
    proof['player'] = record
    proof['preserved_source_commit'] = baseline
    native = ''
    (folder/'native').mkdir(exist_ok=True)
    for name, label in [('monster-threat', '怪兽蓄力近景'), ('combo-camera-peak', '连击镜头'),
                        ('monster-rush-left', '挥爪攻击'), ('beam-contact', '必杀命中')]:
        shutil.copy2(args.player/'native'/(name+'.png'), folder/'native'/(name+'.png'))
        native += f'<figure><figcaption>{label}</figcaption><img loading="lazy" src="native/{name}.png"></figure>'
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder/'original/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder/'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(folder/'comparison.mp4')], check=True)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>格尔赞 · 皮肤与甲片材质</title><style>body{margin:0;background:#10161d;color:#edf3f8;font:16px/1.7 system-ui}main{max-width:1400px;margin:auto;padding:26px}h1{font-size:28px}h2{font-size:21px}p{color:#b5c3d0}img,video{width:100%;display:block;border-radius:6px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}figure{margin:0}figcaption{padding:7px}select,button{font:inherit;background:#233748;color:#eaf3ff;border:1px solid #50708a;border-radius:5px;padding:7px 14px;margin:10px 8px 10px 0}a{color:#76d4e1}.labels{display:flex;justify-content:space-around;padding:8px;background:#243344}@media(max-width:650px){.pair{grid-template-columns:1fr}}</style>
<main><h1>格尔赞 · 皮肤与甲片材质</h1><p>增强甲片、胸腹、背部和尾部的细节，降低塑料感反光。使用同一套几何、骨架与灯光对照；眼睛保留原贴图。图像由内置 image_gen 基于原贴图修复，新增纹理细节并非官方高清素材。</p>
<h2>近景对照</h2><select id="view" aria-label="查看角度"><option value="head">头部与胸甲</option><option value="claw">爪部</option><option value="pose-0-0">全身正面</option><option value="pose-0--55">侧面</option><option value="pose-0-180">背面</option><option value="pose-2-55">攻击姿态</option><option value="pose-5-0">受击姿态</option></select>
<div class="pair"><figure><figcaption>原贴图与原反光参数</figcaption><img id="old" src="original/head.png"></figure><figure><figcaption>新贴图与调整后的反光参数</figcaption><img id="new" src="after/head.png"></figure></div>
<h2>连续旋转</h2><p>两侧各 120 帧、4 秒；离线 30 FPS，不代表游戏运行帧率。</p><div class="labels"><b>原材质</b><b>新材质</b></div><video controls loop playsinline src="comparison.mp4"></video>
<h2>实际 macOS 游戏</h2><p>同一构建通过完整合成输入战斗，包含左右拳、格挡、受击、必杀、暂停恢复与胜利；未开启真人摄像头。本轮没有重新验证自动合照。</p><div class="pair">'''+native+'''</div>
<p>源模型的部分 UV 接缝和多边形轮廓仍然存在。本轮提升材质细节，尚未达到参考街机整体品质。</p>
<p><a href="provenance.json">源码与构建验证</a> · <a href="../../docs/格尔赞高清材质提示词.md">完整生成提示词</a></p>
<script>document.querySelector('#view').onchange=e=>{document.querySelector('#old').src='original/'+e.target.value+'.png';document.querySelector('#new').src='after/'+e.target.value+'.png'}</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
