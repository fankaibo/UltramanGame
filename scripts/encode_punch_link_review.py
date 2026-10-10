"""Compare accepted-fist preparation without changing the underlying fight."""
import csv
import hashlib
import json
import math
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/punch-link'
BASELINE = '788f2d6f821eb0edb7ddcb255586c0aec34b8ad1'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result = {}
    for line in (FOLDER / version / 'sources.txt').read_text().splitlines():
        path, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/' + path
        data = (subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT)
                if version == 'before' else (ROOT / relative).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Stale source: ' + relative)
        result[relative] = expected
    if len(result) != (4 if version == 'before' else 5):
        raise RuntimeError('Incomplete source manifest')
    return result


def main():
    proof = dict(baseline_commit=BASELINE, before=sources('before'), after=sources('after'))
    sequence = (FOLDER / 'before/Tiga-60-states.csv').read_bytes()
    if sequence != (FOLDER / 'after/Tiga-60-states.csv').read_bytes():
        raise RuntimeError('Preparation changed the fight')
    states = list(csv.DictReader(sequence.decode().splitlines()))
    motion = {v:list(csv.DictReader((FOLDER / v / 'Tiga-60-motion.csv').open())) for v in ('before','after')}
    contacts = []
    for i, state in enumerate(states):
        if i and state['health'] != states[i-1]['health']:
            hand = 'left' if state['action'] == 'LeftPunch' else 'right'
            distance = math.dist(*[[float(motion[v][i][hand+axis]) for axis in 'XYZ'] for v in ('before','after')])
            if distance > .001:
                raise RuntimeError('Preparation changed the striking wrist at contact')
            contacts.append(dict(frame=i, hand=hand, wrist_difference=distance))
    if len(contacts) != 6 or len(states) != 240:
        raise RuntimeError('Incomplete six-punch sequence')
    for version in ('before','after'):
        if len(list((FOLDER/version/'frames').glob('*.png'))) != 120:
            raise RuntimeError('Incomplete comparison frames')
    inspection = []
    for hero in ('Tiga','Mebius','Zero','Geed','Grigio'):
        for rate in (15,30,60):
            report = (FOLDER/'after'/f'{hero}-{rate}-validation.txt').read_text()
            if 'punches=6 health=44 linked=5 peak=1.0000' not in report:
                raise RuntimeError('Missing roster handoff check: '+report)
            inspection.append(report)
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(h != digest(app/file) for h in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Evidence does not match the current app')
    if (native['result'] != 'passed' or not native['linked'] or not native['slam'] or not native['ray']
            or guided['result'] != 'passed' or guided['gesture_wobble']['unwanted_attacks']
            or not guided['replay_battle_started'] or guided['automatic_photos'] != 2):
        raise RuntimeError('Combat, pose or photo regression')
    for stage in ('prepare','handoff'):
        name = 'punch-link-'+stage+'.png'
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name,FOLDER/('native-'+stage+'.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error',
                    '-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
                    '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),
                    '-filter_complex','[0:v][1:v]hstack=inputs=2[v]','-map','[v]',
                    '-frames:v','120','-c:v','libx264','-crf','19','-pix_fmt','yuv420p',
                    '-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof.update(sequence_sha256=hashlib.sha256(sequence).hexdigest(),contacts=contacts,
                 inspection=inspection,native=native,guided=guided,build=build,
                 media=dict(frames=120,fps=30,seconds=4,kind='offline render, not measured game frame rate'))
    (FOLDER/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>交替拳 · 动作衔接对照</title><style>body{margin:0;background:#101721;color:#edf4fa;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bfcedc}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}select{font:inherit;padding:8px;background:#254459;color:inherit;border:1px solid #567e99}a{color:#8cdded}</style>
<main><h1>下一只拳，提前准备</h1><p>快速交替出拳时，角色在上一拳收回的过程中，将已经收到指令的另一只拳收向肋侧，稍微转肩，再连续打出。单次挥拳仍只出一拳；防御、大招、受击与暂停会结束这段预备动作。</p>
<div class="pair"><span>修改前：回到护手姿势再出拳</span><span>当前：另一只拳提前预备、接续</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video><p>4 秒无声同步对照，两侧输入与 240 个战斗状态完全一致，6 次命中时的出拳手位置一致。导出视频为 30 FPS，不是游戏实测帧率。</p>
<h2>五位英雄</h2><select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select><div class="pair"><figure><img id="prepare" alt="下一拳预备"><figcaption>收拳转肩</figcaption></figure><figure><img id="handoff" alt="衔接下一拳"><figcaption>接入下一击</figcaption></figure></div>
<h2>实际打包版</h2><div class="pair"><img src="native-prepare.png" alt="实际游戏的收拳预备"><img src="native-handoff.png" alt="实际游戏的下一拳衔接"></div>
<p>五位英雄在 15／30／60 Hz 逻辑步长下完成 6 拳、5 次衔接。实际程序跑过整局战斗、两次必杀、受击、防御、暂停与胜利；另一次体感协议回放检查防御／大招抖动、自动合照、重拍、中断恢复与举手续局。使用模拟输入，未打开真人摄像头，也未调用合照 AI。</p>
<p>本轮只改善快速左右交替拳的衔接，原有第五拳勾拳和第十拳上勾拳仍沿用已有动画。整体连续动作与角色细节仍需提升，尚未达到参考街机的完整效果。</p><p><a href="provenance.json">输入、源码、构建与流程证据</a> · <a href="guided/guided-validation.json">无键鼠流程记录</a></p>
<script>const hero=document.querySelector('#hero');function show(){for(const stage of ['prepare','handoff'])document.querySelector('#'+stage).src='after/'+hero.value+'-60-'+stage+'.png'}hero.onchange=show;show();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
