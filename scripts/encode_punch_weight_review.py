"""Compare actual Unity hip/foot motion and bind review media to the tested app."""
import csv
import hashlib
import json
import math
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/punch-weight'
BASELINE = 'b297c3a911531445ff19ecface522d5cb285f0af'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result = {}
    for line in (FOLDER/version/'sources.txt').read_text().splitlines():
        path, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/'+path
        data = (subprocess.check_output(['git', 'show', BASELINE+':'+relative], cwd=ROOT)
                if version == 'before' else (ROOT/relative).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Source mismatch: '+relative)
        result[relative] = expected
    if len(result) != 5:
        raise RuntimeError('Incomplete rendering manifest')
    return result


def rows(version, name):
    with (FOLDER/version/f'Tiga-60-{name}.csv').open() as stream:
        return list(csv.DictReader(stream))


def point(row, name, axes='XYZ'):
    return [float(row[name+axis]) for axis in axes]


def main():
    proof = dict(baseline_commit=BASELINE, before=sources('before'), after=sources('after'))
    sequence = (FOLDER/'before/Tiga-60-states.csv').read_bytes()
    if sequence != (FOLDER/'after/Tiga-60-states.csv').read_bytes():
        raise RuntimeError('Presentation changed battle timing or damage')
    states = rows('before', 'states')
    bodies = {version:rows(version, 'body') for version in ('before', 'after')}
    motion = {version:rows(version, 'motion') for version in ('before', 'after')}
    if any(len(items) != 240 for items in [states, *bodies.values(), *motion.values()]):
        raise RuntimeError('Incomplete six-strike capture')
    offsets = {name:max(math.dist(point(a, name), point(b, name))
                       for a,b in zip(bodies['before'], bodies['after']))
               for name in ('hip','leftFoot','rightFoot')}
    turns = []
    for a,b in zip(bodies['before'], bodies['after']):
        qa,qb = point(a, 'hipQ', 'XYZW'),point(b, 'hipQ', 'XYZW')
        dot = abs(sum(x*y for x,y in zip(qa,qb)))/math.sqrt(sum(x*x for x in qa)*sum(x*x for x in qb))
        turns.append(math.degrees(2*math.acos(min(1,dot))))
    if offsets['leftFoot'] > .002 or offsets['rightFoot'] > .002 or max(turns) < 5:
        raise RuntimeError('Hip motion missing or existing foot placements changed')
    contacts = []
    for i,state in enumerate(states):
        if i and state['health'] != states[i-1]['health']:
            hand = 'left' if state['action'] == 'LeftPunch' else 'right'
            contacts.append(dict(frame=i, hand=hand, wrist_difference=math.dist(
                point(motion['before'][i],hand), point(motion['after'][i],hand))))
    if len(contacts) != 6 or max(c['wrist_difference'] for c in contacts) > .15:
        raise RuntimeError('Striking hand moved too far from its contact lane')
    inspection = []
    ordinary = []
    for hero in ('Tiga','Mebius','Zero','Geed','Grigio'):
        for rate in (15,30,60):
            report = (FOLDER/'after'/f'{hero}-{rate}-validation.txt').read_text()
            if 'punches=6 health=44 linked=5 peak=1.0000' not in report:
                raise RuntimeError('Missing roster handoff result: '+report)
            inspection.append(report)
        before = (FOLDER/'contact/before'/hero/'validation.txt').read_text()
        after = (FOLDER/'contact/after'/hero/'validation.txt').read_text()
        peak = lambda value:float(re.search(r'maxHandStep=([0-9.]+)', value)[1])
        if peak(after) >= peak(before):
            raise RuntimeError('Ordinary fist continuity did not improve: '+hero)
        ordinary.append(dict(hero=hero,before=before,after=after,
                             peak_step_reduction_percent=round(100*(1-peak(after)/peak(before)),1)))
    for version in ('before','after'):
        if len(list((FOLDER/version/'frames').glob('*.png'))) != 120:
            raise RuntimeError('Incomplete movie frames')
    native = json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER/'guided/build.json').read_text())
    app = ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(value != digest(app/file) for value in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Evidence does not match the current app')
    if (native['result'] != 'passed' or not all(native[k] for k in ('linked','slam','ray'))
            or guided['result'] != 'passed' or guided['gesture_wobble']['unwanted_attacks']
            or guided['gesture_shape_noise']['unexpected_reacquisitions']
            or not guided['replay_battle_started'] or guided['automatic_photos'] != 2):
        raise RuntimeError('Combat or gesture/photo loop regression')
    for name in ('punch-impact-left.png','punch-impact-right.png','punch-link-prepare.png'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name,FOLDER/('native-'+name))
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error',
                    '-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
                    '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),
                    '-filter_complex','[0:v][1:v]hstack=inputs=2[v]','-map','[v]',
                    '-frames:v','120','-c:v','libx264','-crf','19','-pix_fmt','yuv420p',
                    '-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    support = FOLDER/'support/validation'
    proof.update(sequence_sha256=hashlib.sha256(sequence).hexdigest(), contacts=contacts,
                 max_position_difference=offsets,max_hip_turn_degrees=max(turns),inspection=inspection,ordinary=ordinary,
                 support=(support/'validation.txt').read_text(),interruptions=(support/'interruptions.txt').read_text(),
                 native=native,guided=guided,build=build,
                 media=dict(frames=120,fps=30,seconds=4,kind='offline render, not measured game frame rate'))
    (FOLDER/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>连续出拳：转髋与承重</title><style>body{margin:0;background:#101721;color:#eef4fa;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bfcedc}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}select,button{font:inherit;padding:7px 16px;margin:4px;background:#254459;color:inherit;border:1px solid #567e99}a{color:#8cdded}</style>
<main><h1>让身体带着拳头发力</h1><p>出拳时髋部先转，随后屈膝承重，上身和手臂跟进。连续交替时，已收到的下一拳带动另一侧预备。拳头从上一帧实际位置出发，中段速度更均匀，减少前冲肩膀叠加出来的突然加速。保持原有双脚落点、攻击时钟和伤害；勾拳已有较大的上身旋转，因此新增转髋幅度更小。</p>
<div class="pair"><span>修改前</span><span>当前：转髋与膝部承重</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video>
<p>两侧为同一组连续六拳，240 个战斗状态完全一致。4 秒视频按 30 FPS 离线导出，不是游戏运行帧率。角色模型、场景和镜头相同。</p>
<h2>同位置看左右拳</h2><select id="pose"><option value="0013">左拳</option><option value="0027">右拳</option></select><button id="old">修改前</button><button id="new">当前</button><img id="large" alt="选择同一时刻对比转髋和膝部动作">
<h2>五位英雄连续衔接</h2><select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select><div class="pair"><img id="prepare" alt="收拳预备"><img id="handoff" alt="下一拳衔接"></div>
<h2>实际打包版</h2><div class="pair"><img src="native-punch-impact-left.png" alt="实际游戏左拳"><img src="native-punch-impact-right.png" alt="实际游戏右拳"></div>
<p>五位角色在 15／30／60 Hz 下完成连续拳检查；额外验证了命中、副手收拢、支撑脚、重复采样、切入护盾、暂停及新局清理。当前程序通过整局战斗与自动合照续局测试，使用合成输入。这些结果不等同于孩子的真人体感确认，也不表示整体画面已达到参考街机品质。</p>
<p><a href="provenance.json">对照数据与当前构建证据</a> · <a href="guided/guided-validation.json">体感与合照流程</a></p>
<script>const el=id=>document.getElementById(id);let version='after';function shot(){el('large').src=version+'/frames/'+el('pose').value+'.png'}el('old').onclick=()=>{version='before';shot()};el('new').onclick=()=>{version='after';shot()};el('pose').onchange=shot;function roster(){for(const stage of ['prepare','handoff'])el(stage).src='after/'+el('hero').value+'-60-'+stage+'.png'}el('hero').onchange=roster;shot();roster();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__ == '__main__':
    main()
