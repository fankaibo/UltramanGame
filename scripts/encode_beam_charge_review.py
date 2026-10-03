"""Encode verified, synchronized beam-charge renders and current player proof."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'artifacts/beam-charge'
BASELINE = 'ed97945f10a59baf7e6a25ab1ceb50d5c9956fbf'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result = {}
    for line in (FOLDER/version/'render-source.txt').read_text().splitlines():
        if line.startswith(('UTC:', 'Unity:')):
            continue
        path, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/'+path
        data = subprocess.check_output(['git', 'show', BASELINE+':'+relative], cwd=ROOT) if version == 'before' else (ROOT/relative).read_bytes()
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Stale render source: '+relative)
        result[relative] = expected
    if not result:
        raise RuntimeError('Missing render provenance')
    return result


def main():
    proof = dict(baseline_commit=BASELINE, before=sources('before'), after=sources('after'))
    before = (FOLDER/'before/sequence.csv').read_bytes()
    if before != (FOLDER/'after/sequence.csv').read_bytes():
        raise RuntimeError('Presentation changed the combat sequence')
    for version in ('before', 'after'):
        if len(list((FOLDER/version/'frames').glob('*.png'))) != 126:
            raise RuntimeError('Incomplete render sequence')
        if 'contacts=1 releases=1 health=26' not in (FOLDER/version/'validation.txt').read_text():
            raise RuntimeError('Beam contact/recovery regression')
    inspection = (FOLDER/'inspection/validation.txt').read_text()
    if inspection.count('expire/pause/reset=passed') != 5 or 'zero-timePixels=0 foregroundLeakPixels=0 clear=passed' not in inspection:
        raise RuntimeError('Incomplete roster or GPU inspection')
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(app/file) for value in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Player evidence does not match the current build')
    if native['result'] != 'passed' or not native['slam'] or guided['result'] != 'passed':
        raise RuntimeError('Native flow failed')
    if guided['gesture_wobble']['unwanted_attacks'] or not guided['replay_battle_started'] or guided['automatic_photos'] != 2:
        raise RuntimeError('Held pose/photo/replay regression')
    live = (Path(native['evidence_directory'])/'player.log').read_text().split('[PresentationWarmup] complete', 1)[1]
    audio = dict(starts=live.count('[BeamChargeAudio] started'), stops=live.count('[BeamChargeAudio] stopped'))
    if audio != dict(starts=2, stops=2):
        raise RuntimeError('Charge sound was missing or persisted after firing')
    proof.update(sequence_sha256=hashlib.sha256(before).hexdigest(), inspection=inspection,
                 native=native, guided=guided, build=build, audio=audio,
                 media=dict(frames=126, fps=30, seconds=4.2, kind='offline render, not measured game frame rate'))
    shutil.copyfile(Path(native['evidence_directory'])/'native/beam-closeup-peak.png', FOLDER/'native-charge.png')
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER/'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER/'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '126', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER/'comparison.mp4')], check=True)
    (FOLDER/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>必杀聚能 · 同步对照</title><style>body{margin:0;background:#101721;color:#ecf4fb;font:16px/1.7 system-ui}main{max-width:1500px;padding:28px;margin:auto}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bccbdc}video,img{width:100%;border-radius:8px}select{font:inherit;padding:8px;background:#29465b;color:inherit;border:1px solid #60829b;border-radius:6px}a{color:#8cdded}.labels{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}</style>
<main><h1>必杀：聚拢、收束、发射</h1><p>原来的三条固定圆环，改为向双臂汇集的细光流、带体积的蓝色能量和逐渐增强的亮核。亮核位于胸前，随后汇向前臂，保持脸和手势清楚；近景结束后仍持续到光线发射。战斗计时与伤害时点保持一致。</p>
<div class="labels"><span>修改前：头部附近圆环</span><span>当前：胸前聚能与前臂收束</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video><p>4.2 秒离线渲染，左右使用相同输入、镜头、战斗时序。视频为 30 FPS 无声对照，不代表游戏实测帧率。实际游戏新增随聚能强度升起、发射后消失的低音效，迪迦原战吼保留。</p>
<h2>五位英雄的三个蓄力阶段</h2><select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select> <select id="stage"><option value="0">开始 · 0.32 秒</option><option value="1" selected>聚拢 · 0.72 秒</option><option value="2">收束 · 1.12 秒</option></select><img id="roster" alt="英雄蓄力阶段">
<h2>本次打包版</h2><p>通过整局战斗与无键鼠体感协议回放：防御、大招、倒地、重踏、胜利退场、两次自动合照、重拍及举手续局。测试姿态和人像均为合成输入，未打开真人摄像头，未调用合照 AI。</p><img src="native-charge.png" alt="实际应用中的大招聚能">
<p>本轮是必杀表现层升级。孩子实际识别、真人合照融合与更接近参考机台的整体角色动画仍需继续验证和改进。</p><p><a href="inspection/validation.txt">五英雄与 GPU 检查</a> · <a href="guided/guided-validation.json">引导流程</a> · <a href="provenance.json">源码、构建和声音证据</a></p>
<script>const hero=document.querySelector('#hero'),stage=document.querySelector('#stage');function show(){document.querySelector('#roster').src='inspection/'+hero.value+'-'+stage.value+'.png'}hero.onchange=stage.onchange=show;show();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
