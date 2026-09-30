"""Bind the boot/contact-shadow comparison to the tested Unity build."""
import csv
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'artifacts/grounding'
BASELINE = 'b1ad2f02b1bda6cfc479ce820cd6d5f75ad20028'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifests, geometry = {}, {}
    for version in ('before', 'after'):
        source = {}
        for line in (FOLDER/version/'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            path = 'unity/Assets/'+name
            data = (subprocess.check_output(['git', 'show', BASELINE+':'+path], cwd=ROOT)
                    if version == 'before' else (ROOT/path).read_bytes())
            assert hashlib.sha256(data).hexdigest() == expected, path
            source[path] = expected
        assert len(source) == (7 if version == 'before' else 9)
        manifests[version] = source
        geometry[version] = list(csv.DictReader((FOLDER/version/'geometry.csv').open()))
        assert len(geometry[version]) == 20
        assert len(list((FOLDER/version/'frames').glob('frame-*.png'))) == 120
    for old, new in zip(geometry['before'], geometry['after']):
        assert (old['hero'], old['pose']) == (new['hero'], new['pose'])
        if new['hero'] != 'Tiga':
            assert all(abs(float(new[k])-.002) < .001 for k in ('left_sole_y', 'right_sole_y'))
    feet = {}
    for hero in ('Mebius', 'Zero', 'Geed', 'Grigio'):
        feet[hero] = (FOLDER/'footwork/after'/hero/'validation.txt').read_text()
        assert 'supportDrift=0.0000' in feet[hero]
    validation_log = ROOT/'logs/grounding-validation-release.log'
    assert '[GroundingRelease] soles shadowHeight rapidPunches guard knockdown victory=passed' in validation_log.read_text()
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        assert all(v == digest(app/path) for v in (native[key], build['before'][key], build['after'][key]))
    assert native['hero'] == 'Zero' and native['result'] == guided['result'] == 'passed'
    assert all(native[k] for k in ('linked', 'slam', 'ray'))
    assert guided['gesture_wobble']['unwanted_attacks'] == 0
    assert guided['gesture_shape_noise']['unexpected_reacquisitions'] == 0
    assert guided['replay_battle_started']
    for name in ('punch-impact-left.png', 'punch-impact-right.png', 'guard-impact.png', 'hero-rising.png'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name, FOLDER/('native-'+name))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER/'before/frames/frame-%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER/'after/frames/frame-%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '120', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER/'comparison.mp4')], check=True)
    (FOLDER/'provenance.json').write_text(json.dumps(dict(
        baseline=BASELINE, sources=manifests, soles=geometry, footwork=feet,
        shadow_height=(FOLDER/'shadow-height.txt').read_text(), validation_log_sha256=digest(validation_log),
        native=native, guided=guided, build=build,
        media=dict(seconds=4, fps=30, kind='offline rendering, not runtime FPS')),
        ensure_ascii=False, indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>角色鞋底与接触阴影</title><style>body{margin:0;background:#111923;color:#edf3f8;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bfcedb;max-width:1150px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}button,select{font:inherit;padding:7px 16px;margin:4px;background:#294256;color:inherit;border:1px solid #54738a}a{color:#86d9ed}</style>
<main><h1>让角色的脚踩住地面</h1><p>梦比优斯、赛罗、捷德和格力乔的源模型站姿翘起脚尖。现在依据实际鞋底校正朝向与高度，出拳时让膝盖和骨盆承担重心变化，支撑脚保持原位。脚下增加更清晰的接触阴影，脚抬高时自然减弱。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video>
<p>同一段赛罗左右拳，4 秒、30 FPS 离线导出；不代表实际游戏帧率。帧率专项继续后置。</p>
<select id="hero"><option>Zero</option><option>Mebius</option><option>Geed</option><option>Grigio</option><option>Tiga</option></select><select id="pose"><option value="idle">站立</option><option value="left">左拳命中</option><option value="right">右拳命中</option><option value="recovered">回到站姿</option></select><select id="detail"><option value="-feet">脚部近景</option><option value="">完整场景</option></select>
<div class="pair"><img id="before" alt="修改前"><img id="after" alt="当前"></div>
<p>四位校正角色在站立、命中与收势时，两脚最低顶点均为 0.002；舞台地面为 -0.012。迪迦保留原有鞋底姿势。此数值只说明脚与地面的关系，不代表所有角色动画已达到街机水平。</p>
<h2>实际程序验证</h2><div class="pair"><img src="native-punch-impact-left.png" alt="实际游戏左拳"><img src="native-punch-impact-right.png" alt="实际游戏右拳"></div><div class="pair"><img src="native-guard-impact.png" alt="实际游戏防御"><img src="native-hero-rising.png" alt="实际游戏倒地起身"></div>
<p>赛罗完整攻防通过；合成体感回放完成防御、大招、自动合照、重拍、断流恢复及举手续局。合成输入不能代替孩子实际试玩。<a href="provenance.json">源码、测量与安装包证据</a> · <a href="guided/guided-validation.json">完整体感流程</a></p>
<script>const el=id=>document.getElementById(id);function update(){for(const v of ['before','after'])el(v).src=v+'/'+el('hero').value+'-'+el('pose').value+el('detail').value+'.png'}for(const k of ['hero','pose','detail'])el(k).onchange=update;update();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
