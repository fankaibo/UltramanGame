"""Verify and encode the actual claw wake and interrupted-slam captures."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'artifacts/claw-wake'
BASELINE = 'd20826199d0bfc8ea767f934bb07c1f5b8cecd73'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources = {}
    for version in ('before', 'after'):
        manifest = {}
        for line in (FOLDER/version/'render-source.txt').read_text().splitlines():
            if not line.startswith(('Scripts/', 'Resources/')):
                continue
            name, expected = line.rsplit(' ', 1)
            path = 'unity/Assets/'+name
            data = (subprocess.check_output(['git', 'show', BASELINE+':'+path], cwd=ROOT)
                    if version == 'before' else (ROOT/path).read_bytes())
            assert hashlib.sha256(data).hexdigest() == expected, path
            manifest[path] = expected
        assert len(manifest) == 20
        assert len(list((FOLDER/version/'frames').glob('frame-*.png'))) == 480
        sources[version] = manifest
        entry = FOLDER/('slam-entry-'+version)
        assert (entry/'actor.sha256').read_text().strip() == manifest['unity/Assets/Scripts/Runtime/RiggedActor.cs']
    assert {p for p in sources['before'] if sources['before'][p] != sources['after'][p]} == {
        'unity/Assets/Scripts/Runtime/RiggedActor.cs', 'unity/Assets/Scripts/Runtime/StrikeTrails.cs',
        'unity/Assets/Scripts/Runtime/MonsterAttackEffects.cs', 'unity/Assets/Resources/ClawSweep.shader'}
    events = (FOLDER/'before/events.csv').read_bytes()
    assert events == (FOLDER/'after/events.csv').read_bytes()
    log = ROOT/'logs/claw-wake-checks-final.log'
    assert '[ClawWakeChecks] contact pause reset slam ray=passed' in log.read_text()
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        assert all(v == digest(app/path) for v in (native[key], build['before'][key], build['after'][key]))
    assert native['hero'] == 'Tiga' and native['result'] == guided['result'] == 'passed'
    assert all(native[k] for k in ('linked', 'slam', 'ray'))
    assert guided['gesture_wobble']['unwanted_attacks'] == 0
    assert guided['gesture_shape_noise']['unexpected_reacquisitions'] == 0
    assert guided['replay_battle_started']
    for name in ('monster-rush-left.png', 'monster-rush-right.png', 'guard-impact.png', 'slam-swing.png'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name, FOLDER/('native-'+name))
    entry_frames = len(list((FOLDER/'slam-entry-before').glob('*.png')))
    assert entry_frames >= 18 and entry_frames == len(list((FOLDER/'slam-entry-after').glob('*.png')))
    for before, after, rate, count, output in [
        ('before/frames/frame-%04d.png', 'after/frames/frame-%04d.png', 30, 480, 'comparison.mp4'),
        ('slam-entry-before/%04d.png', 'slam-entry-after/%04d.png', 15, entry_frames, 'slam-entry.mp4')]:
        subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                        '-framerate', str(rate), '-i', str(FOLDER/before),
                        '-framerate', str(rate), '-i', str(FOLDER/after),
                        '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                        '-frames:v', str(count), '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                        '-movflags', '+faststart', str(FOLDER/output)], check=True)
    proof = dict(baseline=BASELINE, sources=sources, events_sha256=hashlib.sha256(events).hexdigest(),
                 checks_sha256=digest(log), exchange=(FOLDER/'after/validation.txt').read_text(),
                 native=native, guided=guided, build=build,
                 slam_entry_frames=entry_frames, slam_entry_playback='60 Hz sampling at 15 FPS, quarter speed',
                 media=dict(seconds=16, fps=30, kind='offline rendering, not runtime FPS'))
    (FOLDER/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>怪兽挥爪与砸地衔接</title><style>body{margin:0;background:#111923;color:#edf3f8;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bfcedb;max-width:1150px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}button,select{font:inherit;padding:7px 16px;margin:4px;background:#294256;color:inherit;border:1px solid #54738a}a{color:#86d9ed}</style>
<main><h1>让挥爪读起来像连续动作</h1><p>修改前，手部拖尾和三条亮爪痕在同一时段叠加，像几根发光线。新版保留前冲前段的淡色运动残影，接触时交接给较宽的弧面，两侧余波更短、更淡。成功格挡仍转为蓝色，命中时间和伤害相同。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video>
<p>相同 16 秒攻防，包含四次普攻、两次怪兽攻击、一次格挡与一次受击。离线导出为 30 FPS，不代表运行帧率。</p>
<select id="frame"><option value="0030">挥爪前段</option><option value="0033">右爪靠近护盾</option><option value="0035">格挡接触</option><option value="0424">左爪命中</option><option value="0210">静止恢复</option></select><button id="old">修改前</button><button id="new">当前</button><img id="large" alt="同一时刻切换对照">
<h2>蓄力末段被打中后的砸地</h2><p>旧动作在进入砸地第一帧切掉受击层，双手出现跳变。新版从上一帧的可见双手平滑接回，0.14 秒内完成衔接，仍在原时刻砸地。下方以四分之一速度查看首段；最大手部速度从 27.642 降到 14.714，暂停、重复采样与重开通过。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline preload="metadata" src="slam-entry.mp4"></video>
<h2>实际安装包</h2><div class="pair"><img src="native-monster-rush-left.png" alt="左爪"><img src="native-monster-rush-right.png" alt="右爪"></div><div class="pair"><img src="native-guard-impact.png" alt="格挡"><img src="native-slam-swing.png" alt="砸地"></div>
<p>当前程序完成整局攻防与合成体感全流程，含自动合照、重拍、断流恢复和举手续局。合成输入不能代替真人试玩；角色模型细节和动画丰富度仍未达到参考街机目标。<a href="provenance.json">源码、战斗事件与安装包证据</a> · <a href="guided/guided-validation.json">体感流程</a></p>
<script>const el=id=>document.getElementById(id);let version='after';function update(){el('large').src=version+'/frames/frame-'+el('frame').value+'.png'}el('frame').onchange=update;el('old').onclick=()=>{version='before';update()};el('new').onclick=()=>{version='after';update()};update();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
