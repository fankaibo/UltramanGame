"""Validate and encode contact-light evidence from the actual Unity renderer."""
import csv
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'artifacts/impact-spill'
BASELINE = '6ca7ad31721efdcbeea19c4640c33bc19e4681bd'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifests, measurements = {}, {}
    for version in ('before', 'after'):
        source = {}
        for line in (FOLDER/version/'render-source.txt').read_text().splitlines():
            if not line.startswith(('Scripts/', 'Resources/')):
                continue
            name, expected = line.rsplit(' ', 1)
            path = 'unity/Assets/'+name
            data = (subprocess.check_output(['git', 'show', BASELINE+':'+path], cwd=ROOT)
                    if version == 'before' else (ROOT/path).read_bytes())
            assert hashlib.sha256(data).hexdigest() == expected, path
            source[path] = expected
        assert len(source) == 20
        manifests[version] = source
        measured_source = (FOLDER/version/'lighting/source.sha256').read_text().strip()
        assert source['unity/Assets/Scripts/Runtime/CombatVfx.cs'] == measured_source
        measurements[version] = list(csv.DictReader((FOLDER/version/'lighting/measurements.csv').open()))
        assert len(measurements[version]) == 10
        assert len(list((FOLDER/version/'frames').glob('frame-*.png'))) == 480
    assert {p for p in manifests['before'] if manifests['before'][p] != manifests['after'][p]} == {'unity/Assets/Scripts/Runtime/CombatVfx.cs'}
    for old, new in zip(measurements['before'], measurements['after']):
        assert (old['hero'], old['hand']) == (new['hero'], new['hand'])
        assert int(new['lit_pixels']) >= 30 and float(new['outside_chest_fraction']) <= .05
        assert float(new['outside_chest_fraction']) < float(old['outside_chest_fraction'])
    events = (FOLDER/'before/events.csv').read_bytes()
    assert events == (FOLDER/'after/events.csv').read_bytes()
    assert (FOLDER/'lifecycle.txt').read_text().count('pause=clear reset=clear') == 3
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        assert all(v == digest(app/path) for v in (native[key], build['before'][key], build['after'][key]))
    assert native['result'] == guided['result'] == 'passed'
    assert all(native[k] for k in ('linked', 'slam', 'ray'))
    assert guided['gesture_wobble']['unwanted_attacks'] == 0
    assert guided['gesture_shape_noise']['unexpected_reacquisitions'] == 0
    assert guided['replay_battle_started']
    for name in ('punch-impact-left.png', 'punch-impact-right.png', 'beam-contact.png', 'guard-impact.png'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name, FOLDER/('native-'+name))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER/'before/frames/frame-%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER/'after/frames/frame-%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '480', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER/'comparison.mp4')], check=True)
    (FOLDER/'provenance.json').write_text(json.dumps(dict(
        baseline=BASELINE, sources=manifests, isolated_lighting=measurements,
        baseline_render='Existing 6ca7ad3 visuals reused after validating all 20 manifest files against the committed source.',
        events_sha256=hashlib.sha256(events).hexdigest(), native=native, guided=guided, build=build,
        media=dict(seconds=16, fps=30, kind='offline rendering, not runtime FPS')),
        ensure_ascii=False, indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>普攻接触光照对比</title><style>body{margin:0;background:#111923;color:#edf3f8;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bfcedb;max-width:1150px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}button,select{font:inherit;padding:7px 16px;margin:4px;background:#294256;color:inherit;border:1px solid #54738a}a{color:#86d9ed}</style>
<main><h1>让亮光集中在拳头接触处</h1><p>普攻原有的大范围光源让头部、腹部也变成金色。新版保留命中火花、胸口局部亮光和受击动作，缩小反射光的范围，缩短余光，并把光源放在接触面前方。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video>
<p>相同 16 秒攻防与战斗事件。视频为 30 FPS 离线导出，不代表实际游戏帧率。当前帧率专项仍后置。</p>
<h2>同一英雄、同一命中时刻</h2><select id="hero"><option>Tiga</option><option>Mebius</option><option>Zero</option><option>Geed</option><option>Grigio</option></select><select id="hand"><option value="left">左拳</option><option value="right">右拳</option></select><button id="before">修改前</button><button id="after">当前</button><img id="scene" alt="完整场景同位置切换">
<p>五位英雄、左右手共十组真实蒙皮模型照明检查，单独测量命中点光源。现场完整画面如上；像素测量关闭其他灯，以避免 Unity 自动改选光源干扰结果。局部光可见，胸口区域以外的贡献低于 5%，暂停和重开不会残留。</p>
<h2>当前实际程序</h2><div class="pair"><img src="native-punch-impact-left.png" alt="实际游戏左拳"><img src="native-punch-impact-right.png" alt="实际游戏右拳"></div><div class="pair"><img src="native-beam-contact.png" alt="必杀接触"><img src="native-guard-impact.png" alt="护盾接触"></div>
<p>实际程序完成连续攻防与合成体感全流程，含自动合照、重拍和举手续局。角色细节与动作丰富度仍有差距，此对照不代表已经达到参考街机品质。<a href="provenance.json">源码与本次构建证据</a> · <a href="guided/guided-validation.json">完整体感流程</a></p>
<script>const el=id=>document.getElementById(id);let version='after';function update(){el('scene').src=version+'/lighting/'+el('hero').value+'-'+el('hand').value+'-scene.png'}el('hero').onchange=el('hand').onchange=update;for(const v of ['before','after'])el(v).onclick=()=>{version=v;update()};update();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
