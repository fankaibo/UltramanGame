"""Verify and publish continuous kick, roster and actual-player evidence."""
import argparse
import csv
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/hero-kick'
BASELINE = '70c3d0f00507484a4552367b70ce8dc66ed90a44'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, action='append', required=True)
    args = parser.parse_args()
    sources = {}
    samples = {}
    for version in ('before', 'after'):
        folder = FOLDER / version
        sources[version] = {}
        for line in (folder / 'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            data = ((ROOT / 'unity/Assets' / name).read_bytes() if version == 'after' else
                    (folder / 'ComboStrikeReview.cs').read_bytes() if name == 'Editor/ComboStrikeReview.cs' else
                    subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/' + name], cwd=ROOT))
            assert hashlib.sha256(data).hexdigest() == expected, 'Stale capture: ' + version + '/' + name
            sources[version][name] = expected
        assert len(list((folder / 'frames').glob('*.png'))) == 210
        assert '10 alternating punches, health=40, energy=10, hurt=0' in (folder / 'validation.txt').read_text()
        with (folder / 'sequence.csv').open() as stream:
            samples[version] = list(csv.DictReader(stream))
        assert len(samples[version]) == 420
        assert float(samples[version][-1]['health']) == 40 and float(samples[version][-1]['energy']) == 10
    assert [(r['frame'], r['time']) for r in samples['before']] == [(r['frame'], r['time']) for r in samples['after']]
    checks = (FOLDER / 'kick-validation.txt').read_text()
    assert checks.count('hits=1 health=45 energy=5') == 10 and checks.count('priority=passed') == 30
    hurt = (FOLDER / 'kick-hurt.txt').read_text()
    assert hurt.count('hurtTransitions=1 entryFootStep=0.0000') == 10
    assert hurt.count('recovered=True hitsTaken=1') == 10
    assert sha(ROOT / 'unity/Assets/Editor/HeroKickReview.cs') == (FOLDER / 'kick-checks-source.txt').read_text().strip()
    for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'):
        for side in ('left', 'right'):
            assert len(list((FOLDER / f'roster/{hero}-{side}/frames').glob('*.png'))) == 33
    regression = {name: (FOLDER / 'validation' / name).read_text() for name in ('validation.txt', 'interruptions.txt')}
    assert regression['validation.txt'].count('contacts=10 accents=2') == 7
    assert regression['interruptions.txt'].count('newRound=passed') == 10
    players = [json.loads((path / 'validation.json').read_text()) for path in args.player]
    assert {p['hero'] for p in players} == {'Tiga', 'Grigio'}
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    assert all(p['result'] == 'passed' for p in players)
    assert guided['result'] == 'passed' and guided['replay_battle_started'] and guided['play_again'] and guided['retake']
    assert guided['gesture_wobble']['unwanted_attacks'] == 0
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        current = sha(app / file)
        assert all(record[key] == current for record in players + [build['before'], build['after']]), 'Proof belongs to another build'
    native = '';(FOLDER / 'native').mkdir(exist_ok=True)
    for record in players:
        directory = Path(record['evidence_directory'])
        log = (directory / 'player.log').read_text().split('[PresentationWarmup] complete', 1)[1]
        assert log.count('[HeroKick] begin ') == log.count('[HeroKick] contact ') == (1 if record['finisher'] else 2)
        for moment in ('kick-chamber', 'kick-contact', 'kick-retract', 'kick-setdown'):
            file = record['hero'] + '-' + moment + '.png'
            shutil.copy2(directory / 'native' / (moment + '.png'), FOLDER / 'native' / file)
            native += f'<img src="native/{file}" loading="lazy" alt="{record["hero"]} {moment}">'
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-frames:v', '210',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(baseline=BASELINE, sources=sources, checks=checks, hurt=hurt, regression=regression, players=players, guided=guided, build=build,
                 video=dict(frames_per_side=210, seconds=7, fps=30, note='Offline Unity render, not runtime FPS. Kick presentation deliberately slows the action clock.'))
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>连击踢击 · 抬膝与落脚</title><style>body{margin:0;background:#111b27;color:#edf3fa;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bccddd}video,img{width:100%;display:block;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}.four{display:grid;grid-template-columns:1fr 1fr;gap:10px}button,select{font:inherit;background:#254150;color:white;padding:7px 14px;border:1px solid #527184;border-radius:6px;margin:8px}input{width:100%}a{color:#8cdaef}</style>
<main><h1>连击踢击：抬膝、命中、收腿、落脚</h1><p>参考录屏的连续拳脚变化，每第 5、25、45…次普攻改用踢击演出。孩子仍然挥拳，一次输入扣 1 点、充能 1 次；角色双手护胸，由对应侧的腿完成攻击，另一只脚支撑。第 10、20…次的上勾拳保留。</p>
<div class="pair"><b>原版 70c3d0f：拳击变化</b><b>当前：拳脚交替</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速检查</button><button id="step">前进一帧</button><input id="scrub" aria-label="逐帧检查" type="range" min="0" max="209" value="0">
<p>相同十次交替输入，两侧最终均为血量 40、能量 10。踢击段动作时钟乘以 0.72，让抬膝和收腿更清楚，因此两侧动作时序不完全一致；识别仍每帧读取。7 秒无声对照，离线 30 FPS 不代表实际帧率。</p>
<h2>五英雄 · 左右腿</h2><select id="hero" aria-label="选择角色"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select><div class="pair"><img id="left" alt="左腿接触"><img id="right" alt="右腿接触"></div>
<p>命中特效和短拖尾跟随实际脚部。踢中后的收腿阶段，防御或大招立即接管规则，抬起的腿用 0.16 秒收回。五英雄左右腿的接触、支撑、重复采样与 30 种中断组合均有数值记录。</p>
<h2>踢腿时受击</h2><p>从实际抬腿姿势进入倒地，避免先跳回站姿。以下为同一次受击开始后的连续采样；五英雄左右侧均验证完整倒地与恢复。</p><div class="four"><img src="hurt-entry/0000.png" alt="受击开始"><img src="hurt-entry/0006.png" alt="从抬腿转入失衡"><img src="hurt-entry/0012.png" alt="腿部回收"><img src="hurt-entry/0018.png" alt="接入倒地"></div>
<h2>实际安装包 · 迪迦与格力乔</h2><div class="four">'''+native+'''</div><p>实际程序完成整局与胜利收尾；合成体感回放通过自动拍照、重拍、断流恢复和举手续局，防御与大招扰动未产生误攻击。没有使用真人摄像头；合成输入验证不能替代孩子试玩。</p>
<p><a href="kick-validation.txt">五英雄踢击与中断检查</a> · <a href="kick-hurt.txt">受击衔接检查</a> · <a href="provenance.json">源码和构建记录</a></p><p>这轮增加了近身战的动作种类。整体材质、镜头组合和 AI 合照融合仍需继续改善。</p>
<script>const movie=document.querySelector('#movie'),scrub=document.querySelector('#scrub');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});scrub.oninput=()=>{movie.pause();movie.currentTime=Number(scrub.value)/30};movie.ontimeupdate=()=>scrub.value=Math.min(209,Math.round(movie.currentTime*30));document.querySelector('#step').onclick=()=>{movie.pause();movie.currentTime=Math.min(209/30,movie.currentTime+1/30)};const hero=document.querySelector('#hero');function show(){document.querySelector('#left').src='roster/'+hero.value+'-left/contact.png';document.querySelector('#right').src='roster/'+hero.value+'-right/contact.png'}hero.onchange=show;show();</script></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
