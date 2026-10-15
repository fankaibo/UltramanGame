"""Assemble verified, local-only photo framing comparisons. No AI/image edits."""
import hashlib
import json
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '0f668553c21badd8f5ba725d328ba2ea57af5298'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    folder = ROOT/'artifacts/photo-framing'
    sources = {}
    for stage in ('before', 'after'):
        sources[stage] = {}
        for line in (folder/stage/'sources.txt').read_text().splitlines():
            path, expected = line.rsplit(' ', 1)
            if stage == 'before' and not path.startswith('Editor/'):
                data = subprocess.check_output(['git', 'show', BASELINE+':unity/Assets/'+path], cwd=ROOT)
                if hashlib.sha256(data).hexdigest() != expected:
                    raise RuntimeError('Incorrect baseline: '+path)
            if stage == 'after' and digest(ROOT/'unity/Assets'/path) != expected:
                raise RuntimeError('Stale render: '+path)
            sources[stage][path] = expected
    checks = (folder/'after/validation.txt').read_text()
    if checks.count('fixedHero=passed') != 24:
        raise RuntimeError('Incomplete roster, resolution or framing coverage')
    stats = {}
    for framing in ('half', 'group', 'full', 'raised'):
        if digest(folder/f'before/input-{framing}.png') != digest(folder/f'after/input-{framing}.png'):
            raise RuntimeError('Input cutouts changed')
        for width in (1920, 2560):
            name = f'Tiga-{width}-{framing}'
            a = np.asarray(Image.open(folder/f'before/{name}.png')).astype(np.int16)
            b = np.asarray(Image.open(folder/f'after/{name}.png')).astype(np.int16)
            delta = np.abs(a[:, :width//2]-b[:, :width//2])
            # Independent GPU sessions can round shadow edges differently:
            # the 2K full-body pair has 50 affected pixels, maximum 12/255.
            # Bound both the size and magnitude; a moved/resized hero fails.
            changed = int(np.any(delta > 2, axis=2).sum())
            if delta.max() > 32 or delta.mean() > .001 or changed > 128:
                raise RuntimeError('Framing altered fixed hero or background')
            stats[name] = {'hero_max_delta': int(delta.max()), 'hero_mean_delta': float(delta.mean()), 'hero_changed_pixels_above_2': changed}
    guided = json.loads((folder/'guided/guided-validation.json').read_text())
    build = json.loads((folder/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, name in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if build['before'][key] != digest(app/name) or build['after'][key] != digest(app/name):
            raise RuntimeError('Actual flow does not match the current build')
    if guided['result'] != 'passed' or guided['automatic_photos'] != 2 or not guided['replay_battle_started']:
        raise RuntimeError('Incomplete native guided flow')
    if guided['gesture_wobble']['unwanted_attacks'] or guided['photo_preview_p99_error'] > 2:
        raise RuntimeError('Input or preview regression')
    proof = dict(baseline_commit=BASELINE, sources=sources, checks=checks, fixed_hero_pixels=stats, build=build, guided=guided)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n')
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>半身与双人合照构图</title><style>body{margin:0;background:#111822;color:#edf3fb;font:16px/1.7 system-ui}main{max-width:1560px;margin:auto;padding:28px}h1{font-size:28px}p{color:#bbcbdc;max-width:1100px}img{width:100%;display:block;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}figcaption{padding:8px}select{font:inherit;padding:8px;margin:8px;background:#284459;color:white;border:1px solid #7494aa;border-radius:6px}a{color:#75d7fa}@media(max-width:760px){.pair{grid-template-columns:1fr}}</style>
<main><h1>半身在前景，全身站在英雄身旁</h1><p>旧版把半身人物的肩膀对齐英雄，导致身体截断处悬在场景中。现在半身构图延伸到照片下边缘，双人以整个轮廓居中；全身仍按脚底对齐。英雄尺寸、位置和完整轮廓固定。</p>
<p>这些是 Unity 实际相机输出。圆形与色块是同一组测试人像，专门验证构图，没有修改或上传家庭照片，也没有调用 AI。真实头发硬边、光照匹配和生成式融合仍需改进。</p>
<label>取景<select id="framing"><option value="half">单人半身</option><option value="group">双人半身</option><option value="full">完整全身</option><option value="raised">半身举手</option></select></label>
<label>分辨率<select id="resolution"><option value="1920">1920 × 1080</option><option value="2560">2560 × 1440</option></select></label>
<div class="pair"><figure><figcaption>修改前</figcaption><img id="before" alt="修改前构图"></figure><figure><figcaption>修改后</figcaption><img id="after" alt="修改后构图"></figure></div>
<h2>五位英雄均固定</h2><label>英雄<select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select></label><img id="roster" alt="所选英雄与测试人像">
<h2>打包版 · 自动拍照后的预览</h2><p>通过本机姿态协议完成战斗、合照、重拍、断流恢复和双手举高再开局；没有键鼠事件。TEST 人物也是合成数据，不能代替真实摄像头体验。</p><img src="guided/native/photo-Review.png" alt="实际 macOS 试玩包照片预览">
<p><a href="after/validation.txt">24 组构图检查</a> · <a href="guided/guided-validation.json">自动流程</a> · <a href="provenance.json">源码与构建证据</a></p>
<script>const framing=document.querySelector('#framing'),resolution=document.querySelector('#resolution'),hero=document.querySelector('#hero');function update(){for(const stage of ['before','after'])document.getElementById(stage).src=stage+'/Tiga-'+resolution.value+'-'+framing.value+'.png';document.getElementById('roster').src='after/'+hero.value+'-1920-'+framing.value+'.png'}framing.onchange=resolution.onchange=hero.onchange=update;update();</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
