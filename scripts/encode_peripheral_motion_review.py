"""Bind a same-input scene comparison to the GPU checks and the installed player."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'artifacts/peripheral-motion'
BASELINE = '4b317fbe29ea12fa9ac5fe0d64da341fbc7feb1b'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result = {}
    for line in (FOLDER/version/'render-source.txt').read_text().splitlines():
        if not line.startswith(('Scripts/', 'Resources/')):
            continue
        name, expected = line.rsplit(' ', 1)
        path = 'unity/Assets/'+name
        data = (subprocess.check_output(['git', 'show', BASELINE+':'+path], cwd=ROOT)
                if version == 'before' else (ROOT/path).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Rendering source changed: '+path)
        result[path] = expected
    if len(result) < 20:
        raise RuntimeError('Rendering source manifest incomplete')
    return result


def main():
    before, after = sources('before'), sources('after')
    expected_changes = {'unity/Assets/'+name for name in (
        'Scripts/Runtime/ArcadeStageFx.cs', 'Scripts/Runtime/CinematicCamera.cs',
        'Scripts/Runtime/GameWorld.cs', 'Resources/CinematicComposite.shader')}
    if {name for name in before if before[name] != after[name]} != expected_changes:
        raise RuntimeError('Comparison includes unrelated rendering changes')
    events = (FOLDER/'before/events.csv').read_bytes()
    if events != (FOLDER/'after/events.csv').read_bytes():
        raise RuntimeError('Motion overlay changed combat events or timing')
    if any(len(list((FOLDER/version/'frames').glob('frame-*.png'))) != 480 for version in ('before', 'after')):
        raise RuntimeError('Incomplete 16 second comparison')
    gpu = (FOLDER/'gpu/validation.txt').read_text()
    if gpu.count('central=0.00000') != 48 or gpu.count('reset=passed') != 12:
        raise RuntimeError('Missing a mode, aspect, lens or cleanup check')
    flash = (ROOT/'artifacts/contact-light/validation.txt').read_text()
    if 'worldPause=passed reset=passed' not in flash:
        raise RuntimeError('Existing localized flash checks did not complete')
    exchange = (FOLDER/'after/validation.txt').read_text()
    if 'leftPunches=2 rightPunches=2 blocks=1 hurt=1 alternatingClaws=2' not in exchange:
        raise RuntimeError('Exchange did not complete')
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided-release/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided-release/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(app/file) for value in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Proof does not match the installed app')
    if (native['result'] != 'passed' or not all(native[k] for k in ('linked', 'slam', 'ray'))
            or guided['result'] != 'passed' or guided['gesture_wobble']['unwanted_attacks']
            or guided['gesture_shape_noise']['unexpected_reacquisitions']
            or guided['gesture_startup_noise']['guard_overlap_frames'] < 4
            or not guided['replay_battle_started']):
        raise RuntimeError('Combat or guided flow regression')
    for name in ('punch-impact-left.png', 'monster-rush-left.png', 'guard-impact.png'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name, FOLDER/('native-'+name))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER/'before/frames/frame-%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER/'after/frames/frame-%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '480', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER/'comparison.mp4')], check=True)
    report = dict(baseline_commit=BASELINE, before=before, after=after,
                  events_sha256=hashlib.sha256(events).hexdigest(), gpu=gpu, flash=flash,
                  exchange=exchange, native=native, guided=guided, build=build,
                  gesture_source_sha256=digest(ROOT/'unity/Assets/Scripts/Core/Pose.cs'),
                  media=dict(seconds=16, fps=30, frames=480, kind='offline render, not runtime frame rate'))
    (FOLDER/'provenance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>战斗边缘动效对照</title><style>body{margin:0;background:#101721;color:#eef4fa;font:16px/1.8 system-ui}main{max-width:1600px;margin:auto;padding:28px}p{color:#bfcedc;max-width:1150px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}button,select{font:inherit;padding:7px 16px;margin:4px;background:#254459;color:inherit;border:1px solid #567e99}a{color:#8cdded}</style>
<main><h1>让打击动效留在镜头边缘</h1><p>旧速度线按世界坐标摆放，镜头变化后会落到山体、烟柱和废墟中间。当前改为短促的边缘拖影与柔和光丝，随出拳、怪兽冲刺和受击出现，向画面中心渐隐。角色、命中火花、场景和攻击时点相同。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video>
<p>16 秒攻防：左右各两拳、两次怪兽攻击，一次格挡、一次倒地。视频为 30 FPS 离线导出，不代表实际游戏帧率。</p>
<h2>同位置切换</h2><select id="frame"><option value="0082">左拳命中</option><option value="0106">右拳命中</option><option value="0033">怪兽冲刺</option><option value="0426">受击</option><option value="0210">静止恢复</option></select><button id="old">修改前</button><button id="new">当前</button><img id="large" alt="同一战斗时刻的画面对照">
<h2>实际安装包</h2><img src="native-punch-impact-left.png" alt="实际游戏左拳命中"><div class="pair"><img src="native-monster-rush-left.png" alt="怪兽攻击"><img src="native-guard-impact.png" alt="护盾格挡"></div>
<p>4 种画幅、3 种镜头及 4 种动作共 48 组 GPU 检查确认：中央 78% 宽度与上下边缘不受新动效改变；暂停、必杀、静止和重开时清除。原有命中闪光与高光处理回归通过。实际程序完成整局战斗和含噪声的合成体感、合照续局验证，未使用真人输入。</p>
<p>这是战斗画面的一项改进，整体角色细节和动作丰富度仍未达到参考街机品质。<a href="provenance.json">源码、时间轴与构建证据</a> · <a href="guided-release/guided-validation.json">完整体感流程</a></p>
<script>const el=id=>document.getElementById(id);let version='after';function shot(){el('large').src=version+'/frames/frame-'+el('frame').value+'.png'}el('old').onclick=()=>{version='before';shot()};el('new').onclick=()=>{version='after';shot()};el('frame').onchange=shot;shot();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
