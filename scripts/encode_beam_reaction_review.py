"""Build a synchronized reaction-camera comparison tied to the tested app."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/beam-reaction-camera'
BASELINE = 'a905408189be3140f310101c697ff162ca1bdab5'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result = {}
    for line in (FOLDER / version / 'render-source.txt').read_text().splitlines():
        if line.startswith(('UTC:', 'Unity:')):
            continue
        path, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/' + path
        data = (subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT)
                if version == 'before' else (ROOT / relative).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Stale render source: ' + relative)
        result[relative] = expected
    if not result:
        raise RuntimeError('Missing render provenance')
    return result


def main():
    proof = dict(baseline_commit=BASELINE, before=sources('before'), after=sources('after'))
    sequence = (FOLDER / 'before/sequence.csv').read_bytes()
    if sequence != (FOLDER / 'after/sequence.csv').read_bytes():
        raise RuntimeError('Camera change altered combat timing or damage')
    for version in ('before', 'after'):
        if len(list((FOLDER / version / 'frames').glob('*.png'))) != 126:
            raise RuntimeError('Incomplete rendered sequence')
        if 'contacts=1 releases=1 health=26' not in (FOLDER / version / 'validation.txt').read_text():
            raise RuntimeError('Incomplete beam contact/recovery')
    inspection = (FOLDER / 'after/flow-validation.txt').read_text()
    if (inspection.count('framing/coverage/zero-time/return=passed') != 7
            or inspection.count('pause-and-reset=passed') != 3):
        raise RuntimeError('Missing roster, framing or interruption check')
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(app / file) for value in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Player evidence does not match the current app')
    if (native['result'] != 'passed' or not native['slam'] or not native['ray']
            or native['beam_reaction_cuts'] != dict(begins=2, ends=2) or guided['result'] != 'passed'):
        raise RuntimeError('Native combat or guided flow failed')
    if (guided['gesture_wobble']['unwanted_attacks'] or not guided['replay_battle_started']
            or guided['automatic_photos'] != 2):
        raise RuntimeError('Pose, photo or replay regression')
    for name in ('beam-reaction-entry', 'beam-reaction-peak', 'beam-fade'):
        shutil.copyfile(Path(native['evidence_directory']) / 'native' / (name + '.png'), FOLDER / ('native-' + name + '.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '126', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof.update(sequence_sha256=hashlib.sha256(sequence).hexdigest(), inspection=inspection,
                 native=native, guided=guided, build=build,
                 media=dict(frames=126, fps=30, seconds=4.2, kind='offline render, not measured game frame rate'))
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>必杀命中 · 镜头对照</title><style>body{margin:0;background:#101721;color:#ecf4fb;font:16px/1.7 system-ui}main{max-width:1500px;padding:28px;margin:auto}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bccbdc}video,img{width:100%;border-radius:8px}select{font:inherit;padding:8px;background:#29465b;color:inherit;border:1px solid #60829b;border-radius:6px}a{color:#8cdded}.labels{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}</style>
<main><h1>必杀：让命中的瞬间更清楚</h1><p>英雄蓄力、光线发射之后，切到约 0.72 秒的哥尔赞受击近景，看到胸口命中、后仰、烟尘和环境受光；光线收束时切回双方站位。前景保留英雄的手和光线方向，近景期间收起连击数字。战斗时序、伤害、能量和动作识别保持一致。</p>
<div class="labels"><span>修改前：持续使用普通对战镜头</span><span>当前：命中近景 → 回到双方</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video><p>4.2 秒无声离线渲染对照，两侧使用相同输入与战斗时序。30 FPS 是导出视频帧率，不代表游戏实测帧率。</p>
<h2>实际打包版画面</h2><select id="stage"><option value="entry">命中近景</option><option value="peak">受击持续</option><option value="fade">光线收束与回位</option></select><img id="native" alt="本次实际应用画面">
<p>五位英雄、15／30／60 Hz 逻辑步长、背景覆盖、零时间重复渲染和暂停／重开检查通过。最终打包版完成两次大招近景及回位、整局战斗、自动合照、重拍、短暂断流恢复与举手续局。防御和大招姿势抖动测试期间未误触普攻。测试使用合成输入，没有打开真人摄像头或调用合照 AI。</p>
<p>这次增加了命中镜头层次；角色细节、连续动画和真人合照融合仍需继续改进，不能据此认为已经达到参考街机效果。</p><p><a href="after/flow-validation.txt">镜头与战斗检查</a> · <a href="guided/guided-validation.json">引导流程</a> · <a href="provenance.json">源码与构建证据</a></p>
<script>const stage=document.querySelector('#stage');function show(){document.querySelector('#native').src='native-beam-'+(stage.value==='fade'?'fade':'reaction-'+stage.value)+'.png'}stage.onchange=show;show();</script></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
