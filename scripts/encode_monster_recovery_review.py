"""Verify and encode the monster's reset gesture against the previous build."""
import csv
import hashlib
import json
import math
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/monster-recovery'
BASELINE = 'c22edd5b95df094afbe5766c9fd424ae24d785fc'


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
            raise RuntimeError('Stale render source: ' + relative)
        result[relative] = expected
    if len(result) != (2 if version == 'before' else 3):
        raise RuntimeError('Incomplete source manifest')
    return result


def distance(a, b, key):
    return math.dist([float(a[key + c]) for c in 'XYZ'], [float(b[key + c]) for c in 'XYZ'])


def angle(a, b, key):
    x = [float(a[key + c]) for c in 'XYZW']
    y = [float(b[key + c]) for c in 'XYZW']
    dot = abs(sum(i * j for i, j in zip(x, y)))
    norm = math.sqrt(sum(i * i for i in x) * sum(j * j for j in y))
    return math.degrees(2 * math.acos(min(1, dot / norm)))


def rows(path):
    with path.open() as stream:
        return list(csv.DictReader(stream))


def main():
    proof = dict(baseline_commit=BASELINE, before=sources('before'), after=sources('after'), motion={})
    for version in ('before', 'after'):
        if len(list((FOLDER / version / 'frames').glob('*.png'))) != 480:
            raise RuntimeError('Incomplete comparison frames')
    for attack in range(1, 5):
        sequence_path = FOLDER / f'before/attack-{attack}-60-sequence.csv'
        sequence = sequence_path.read_bytes()
        if sequence != (FOLDER / f'after/attack-{attack}-60-sequence.csv').read_bytes():
            raise RuntimeError('Changed battle timeline')
        states = rows(sequence_path)
        a = rows(FOLDER / f'before/attack-{attack}-60-motion.csv')
        b = rows(FOLDER / f'after/attack-{attack}-60-motion.csv')
        if any(len(v) != 240 for v in (states, a, b)):
            raise RuntimeError('Incomplete motion samples')
        errors = {key: max(distance(x, y, key) for x, y in zip(a, b))
                  for key in ('left', 'right', 'footL', 'footR')}
        angles = {key: max(angle(x, y, key) for x, y in zip(a, b)) for key in ('head', 'jaw')}
        attack_drift = max(distance(x, y, key) for s, x, y in zip(states, a, b)
                           if s['enemy'] == 'Attack' for key in ('left', 'right'))
        if max(errors['footL'], errors['footR'], attack_drift) > .0001:
            raise RuntimeError('Reset gesture changed support or attack trajectory')
        if max(errors['left'], errors['right']) < .15 or angles['head'] < 5 or angles['jaw'] < 8:
            raise RuntimeError('Reset gesture not exercised')
        proof['motion'][str(attack)] = dict(sequence_sha256=hashlib.sha256(sequence).hexdigest(),
                                          max_position_difference=errors, max_angle_difference=angles,
                                          attack_hand_difference=attack_drift)
    validations = {str(rate): (FOLDER / f'after/{rate}-validation.txt').read_text() for rate in (15, 30, 60)}
    if any(value.count('health=50') != 4 for value in validations.values()):
        raise RuntimeError('Missing recovery cases')
    stability = (FOLDER / 'claw-stability/stability.txt').read_text()
    flow = (FOLDER / 'flow-validation.txt').read_text()
    if stability.count('passed') != 12 or flow.count('passed') != 12:
        raise RuntimeError('Missing continuous action or interruption checks')
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(h != digest(app / file) for h in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Evidence does not match current app')
    if (native['result'] != 'passed' or not native['linked'] or not native['slam'] or not native['ray']
            or guided['result'] != 'passed' or guided['gesture_wobble']['unwanted_attacks']
            or not guided['replay_battle_started'] or guided['automatic_photos'] != 2):
        raise RuntimeError('Combat, pose or photo regression')
    for mode in ('drop', 'return'):
        shutil.copyfile(Path(native['evidence_directory']) / 'native' / ('monster-recovery-' + mode + '.png'),
                        FOLDER / ('native-' + mode + '.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '480', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof.update(validations=validations, stability=stability, interruptions=flow,
                 native=native, guided=guided, build=build,
                 media=dict(frames=480, fps=30, seconds=16, kind='offline render, not measured game frame rate'))
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>哥尔赞 · 攻击后收势</title><style>body{margin:0;background:#101721;color:#edf4fa;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bfcedc}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}figure{margin:12px 0}select,button{font:inherit;padding:8px;background:#254459;color:inherit;border:1px solid #567e99}a{color:#8cdded}</style>
<main><h1>攻击结束后，收爪、喘息，再准备</h1><p>哥尔赞在原有两秒恢复阶段内放低双爪，稍微收颌、转头，再回到准备姿势。孩子可以照常反击；被击中、暂停、新局或新语音引导会立即接管。没有增加等待或修改伤害。</p>
<div class="pair"><span>修改前：回到重复待机</span><span>当前：收爪、呼吸与回正</span></div><video id="video" controls loop playsinline preload="metadata" src="comparison.mp4"></video><p><button data-time="0">第一次爪击</button> <button data-time="4">第二次爪击</button> <button data-time="8">砸地</button> <button data-time="12">头部光线</button></p><p>16 秒无声同步对照，每组 4 秒。960 个战斗状态一致，双脚落点与攻击阶段手部轨迹不变。30 FPS 为离线导出帧率，不代表游戏实测帧率。</p>
<h2>同一时刻的收势</h2><select id="mode"><option value="1">第一次爪击</option><option value="2">第二次爪击</option><option value="3">砸地</option><option value="4">头部光线</option></select><div class="pair"><figure><img id="before" alt="修改前"><figcaption>修改前</figcaption></figure><figure><img id="after" alt="修改后"><figcaption>当前</figcaption></figure></div>
<h2>本次实际打包版</h2><div class="pair"><figure><img src="native-drop.png" alt="实际游戏收爪"><figcaption>放低双爪</figcaption></figure><figure><img src="native-return.png" alt="实际游戏回正"><figcaption>回到准备</figcaption></figure></div>
<p>15／30／60 Hz 逻辑步长下检查重复采样、连续攻防、手腕方向及恢复动作被打断。最终程序通过整局攻防、自动合照、重拍、中断恢复和举手续局；防御／大招抖动期间误触普攻为 0。输入及人像均为合成 TEST 数据，本轮未开启真人摄像头或调用合照 AI。</p>
<p>本轮补足攻击后的连续动作，源模型的手掌形状、贴图精度和整体街机演出仍需提升。</p><p><a href="provenance.json">源码、构建与逐帧证据</a> · <a href="flow-validation.txt">打断检查</a> · <a href="guided/guided-validation.json">无键鼠流程</a></p>
<script>const mode=document.querySelector('#mode'),video=document.querySelector('#video');function show(){for(const v of ['before','after'])document.querySelector('#'+v).src=v+'/attack-'+mode.value+'-60-0.65.png'}mode.onchange=show;show();document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{video.currentTime=Number(b.dataset.time);video.play()});</script></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
