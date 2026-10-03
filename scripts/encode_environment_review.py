"""Make a local, source-checked comparison of the arena lighting revision."""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path)
    parser.add_argument('--ffmpeg', default='/opt/homebrew/bin/ffmpeg')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / 'artifacts/hero-environment'
    old = root / 'artifacts/beam-volume/after'
    new = output / 'arena/after'
    baseline = '01cefe230da8385a008f2172d273b6c98e3248f3'
    provenance = {'baseline_commit': baseline, 'source_checks': [], 'offline_fps': 30}
    for record, version in [(output/'before/sources.txt', 'before'), (old/'render-source.txt', 'before'),
                            (output/'after/sources.txt', 'after'), (new/'render-source.txt', 'after')]:
        count = 0
        for line in record.read_text().splitlines():
            match = re.fullmatch(r'(.+) ([0-9a-f]{64})', line)
            if not match:
                continue
            path, expected = match.groups()
            path = 'unity/Assets/' + path
            data = subprocess.check_output(['git', 'show', baseline + ':' + path], cwd=root) if version == 'before' else (root/path).read_bytes()
            if hashlib.sha256(data).hexdigest() != expected:
                raise RuntimeError('Stale comparison source: ' + path)
            count += 1
        if not count:
            raise RuntimeError('No source evidence: ' + str(record))
        provenance['source_checks'].append({'record': str(record.relative_to(root)), 'version': version, 'files': count})
    if (old/'sequence.csv').read_bytes() != (new/'sequence.csv').read_bytes():
        raise RuntimeError('Before/after battle time or pose differs')
    for folder in (old, new):
        if len(list((folder/'frames').glob('*.png'))) != 126:
            raise RuntimeError('Incomplete 4.2-second render')
    subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(old/'frames/%04d.png'),
                    '-framerate', '30', '-i', str(new/'frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(output/'comparison.mp4')], check=True)
    native = ''
    if args.player:
        player = args.player.resolve()
        proof = json.loads((player/'validation.json').read_text())
        app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
        for key, path in [('assembly_sha256', app/'Managed/Assembly-CSharp.dll'), ('resources_sha256', app/'resources.assets')]:
            if hashlib.sha256(path.read_bytes()).hexdigest() != proof[key]:
                raise RuntimeError('Native screenshot is from a different build: ' + key)
        if proof['result'] != 'passed':
            raise RuntimeError('Player review failed')
        (output/'native').mkdir(exist_ok=True)
        for name in ('battle-entry', 'guard-impact', 'beam-closeup-peak', 'beam-sustain', 'victory-hero'):
            shutil.copy2(player/'native'/f'{name}.png', output/'native'/f'{name}.png')
            native += f'<img loading="lazy" src="native/{name}.png" alt="实际游戏：{name}">'
        provenance['native_player'] = proof
    (output/'provenance.json').write_text(json.dumps(provenance, ensure_ascii=False, indent=2))
    html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>角色材质与火山场景光照对照</title><style>
*{box-sizing:border-box}body{margin:0;background:#0b1018;color:#e8edf5;font:16px/1.65 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:32px 24px}h1{font-size:30px;margin:8px 0}h2{font-size:21px;margin:34px 0 12px}p{color:#aebdd0;max-width:1050px}small{color:#aebdd0}.labels,.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.labels{margin:12px 0 6px;font-weight:600}.labels span:last-child{color:#74ddc7}img,video{width:100%;display:block;border-radius:10px;background:#080b10}select,button{font:inherit;padding:8px 14px;border:1px solid #41546d;color:#e8edf5;background:#172434;border-radius:7px;margin:4px 8px 4px 0}a{color:#78d3f2}.native{display:grid;grid-template-columns:1fr 1fr;gap:14px}@media(max-width:700px){main{padding:18px 12px}.native{grid-template-columns:1fr}}
</style><main><small>ULTRAMAN GAME · 画面对照 · 2026-09-27</small><h1>角色材质与火山场景光照</h1>
<p>补上火山环境反射，分开处理银色装甲和彩色战衣，调整冷暖补光，并让强光逐渐过渡。这里是本轮可见差异；角色贴图和动作素材仍限制最终街机品质。</p>
<h2>同一段大招 · 修改前 / 修改后</h2><p>左侧为 01cefe2，右侧为新版。同一随机种子、同一动作时钟，4.2 秒离线渲染。视频 30 FPS 只用于看连续画面，不代表游戏帧率。</p>
<div class="labels"><span>修改前</span><span>修改后</span></div><video id="clip" src="comparison.mp4" controls loop playsinline preload="metadata"></video><button id="slow">0.5 倍慢放</button><button id="normal">正常速度</button>
<h2>五英雄材质镜头</h2><p>两侧使用相同镜头和姿势。此处隐藏背景以观察表面；画面下缘有裁切，不用于验证整个人物构图。</p>
<label>角色 <select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select></label>
<label>姿势 <select id="pose"><option value="0">准备</option><option value="3">防御</option><option value="4">必杀</option></select></label><label>角度 <select id="angle"><option value="front">正面</option><option value="back">背面</option></select></label>
<div class="labels"><span>修改前</span><span>修改后</span></div><div class="pair"><img id="before" alt="修改前的角色材质"><img id="after" alt="修改后的角色材质"></div>
<h2>当前打包版游戏截图</h2><p>以下来自本轮真实 macOS 玩家进程的 1080P 截图，使用合成输入验证；没有拍摄真人。</p><div class="native">NATIVE_IMAGES</div>
<p><a href="provenance.json">来源、代码摘要与玩家验证记录</a> · <a href="inspection/validation.txt">反射与资源检查</a> · <a href="inspection/highlights.txt">高光检查</a> · <a href="guard/validation.txt">五英雄护盾检查</a></p>
<script>const hero=document.querySelector('#hero'),pose=document.querySelector('#pose'),angle=document.querySelector('#angle');function update(){for(const version of ['before','after'])document.getElementById(version).src=version+'/'+hero.value+'-'+pose.value+'-'+angle.value+'.png'}for(const e of [hero,pose,angle])e.addEventListener('change',update);update();const clip=document.querySelector('#clip');document.querySelector('#slow').onclick=()=>{clip.playbackRate=.5;clip.play()};document.querySelector('#normal').onclick=()=>{clip.playbackRate=1;clip.play()};</script></main></html>'''
    (output/'index.html').write_text(html.replace('NATIVE_IMAGES', native or '<p>本轮玩家截图尚未附加。</p>'))
    print(output/'index.html')


if __name__ == '__main__':
    main()
