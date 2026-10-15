"""Check opening captures and native battle/replay evidence before presentation."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/monster-entrance'
BASELINE = 'fc2f78fcf18b19668a392b23163c8fcbc6789bd8'
HEROES = ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(folder, before=False):
    result = {}
    for line in (folder / 'render-source.txt').read_text().splitlines():
        if line.startswith('UTC:'):
            continue
        name, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/' + name
        data = ((folder / 'review-source.cs').read_bytes() if name == 'Editor/EntranceReview.cs' else
                subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT) if before else
                (ROOT / relative).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Stale capture: ' + relative)
        result[relative] = expected
    return result


def main():
    before = FOLDER / 'before/Tiga'
    manifests = {'before': sources(before, True), 'after': {}}
    assert len(list((before / 'frames').glob('*.png'))) == 210
    reports = {}
    for hero in HEROES:
        capture = FOLDER / 'after' / hero
        manifests['after'][hero] = sources(capture)
        reports[hero] = (capture / 'validation.txt').read_text()
        assert 'starts=1 phase=Battle health=50 punches=0' in reports[hero]
        assert 'steps=2 roars=1' in reports[hero] and 'monsterFrames=114' in reports[hero]
    assert len(list((FOLDER / 'after/Tiga/frames').glob('*.png'))) == 210
    motion = (FOLDER / 'motion-validation.txt').read_text()
    assert len(motion.splitlines()) == 3 and motion.count('steps=2 roars=1') == 3
    audio = (FOLDER / 'audio-validation.txt').read_text()
    assert audio.startswith('passed ') and 'seconds=0.880' in audio
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    assert native['result'] == guided['result'] == 'passed'
    assert native['entrance_steps'] == ['left', 'right'] and native['entrance_roars'] == 1
    assert all(native[k] for k in ('slam', 'ray', 'linked'))
    assert guided['replay_battle_started'] and guided['gesture_wobble']['unwanted_attacks'] == 0
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        assert all(h == digest(app / file) for h in (native[key], build['before'][key], build['after'][key]))
    replay_log = (ROOT / 'logs/monster-entrance-guided.log').read_text()
    live = replay_log.split('[PresentationWarmup] complete; player round untouched')[-1]
    assert live.count('[MonsterEntrance] step=left ') == live.count('[MonsterEntrance] step=right ') == 2
    assert live.count('[MonsterEntrance] roar sound=True') == 2
    for name in ('transform-front', 'monster-entrance-step', 'monster-entrance-plant', 'monster-entrance-roar', 'monster-entrance-return', 'battle-entry'):
        shutil.copyfile(Path(native['evidence_directory']) / 'native' / (name + '.png'), FOLDER / (name + '.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(before / 'frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/Tiga/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-frames:v', '210',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(baseline=BASELINE, sources=manifests, framing=reports, motion=motion, audio=audio, native=native,
                 guided=guided, build=build, replay_entrances=2,
                 media=dict(seconds=7, fps=30, kind='offline Unity capture, not measured runtime FPS'),
                 change='Opening duration increases from 2.2 to 4.4 seconds; combat instructions still provide speech plus 3 seconds.')
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>英雄变身与哥尔赞登场</title><style>body{margin:0;background:#111923;color:#edf3f8;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bfcedb;max-width:1150px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}audio{width:min(100%,700px)}a{color:#86d9ed}</style>
<main><h1>先让对手登场，再开始交锋</h1><p>英雄变身后，镜头转向哥尔赞。它迈出两步，脚底扬起碎石与尘土，抬头低吼，再切回双方对峙。整个开场从 2.2 秒延长到 4.4 秒，战斗引导在演出结束后开始；听完后的三秒准备时间保留。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline src="comparison.mp4"></video><p>同一起始输入的 7 秒对照。右侧新增怪兽登场，所以两侧开始战斗的时刻不同。视频为离线 30 FPS、无声，不代表运行帧率。</p>
<h2>实际游戏中的开场</h2><div class="pair"><img src="transform-front.png" alt="英雄变身"><img src="monster-entrance-step.png" alt="哥尔赞迈步"></div>
<div class="pair"><img src="monster-entrance-plant.png" alt="脚底接地扬尘"><img src="monster-entrance-roar.png" alt="抬头低吼"></div>
<div class="pair"><img src="monster-entrance-return.png" alt="回到双方对峙"><img src="battle-entry.png" alt="正式进入战斗"></div>
<h2>登场低吼试听</h2><audio controls src="arrival-roar.wav"></audio><p>由游戏生成的原创低吼音效，长度 0.88 秒；并非影视原声。实际安装包已核对它只播放一次，并与两次脚步和接地效果对应。音效沿用静音、暂停与语音优先混音。</p>
<p>五位英雄和哥尔赞均完整入镜。平地支撑脚、零时间重复采样及六处暂停恢复检查通过；实际程序完成整局攻防和合成体感合照／重拍／举手续局，两轮开场均正常。合成输入不能代替孩子的真实摄像头体验。</p>
<p>模型精度与连续动画的丰富度仍需改善，整体街机目标尚未完成。<a href="provenance.json">源码、构图、动作与安装包验证</a> · <a href="guided/guided-validation.json">无键鼠流程证据</a></p></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
