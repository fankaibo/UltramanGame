"""Bind the fall-body comparison to its sources, timing and Release evidence."""
import csv
import hashlib
import json
import statistics
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/fall-body-20261008'
BASELINE = 'bdefd6e198298d72a6e7e7eeac8644b33136612a'
HEROES = ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources = {}
    for version in ('before', 'after', 'rates/15', 'rates/30'):
        folder = FOLDER / version
        report = (folder / 'validation.txt').read_text()
        for hero in HEROES:
            if hero + ': rate=' not in report:
                raise RuntimeError('Missing skeletal hero: ' + hero)
        for hero in ('Zeta', 'DeckerStrong'):
            if hero + ': skipped: missing skeletal model' not in report:
                raise RuntimeError('Missing explicit asset exclusion: ' + hero)
        sources[version] = {}
        for line in (folder / 'render-source.txt').read_text().splitlines():
            if line.startswith(('Rendered UTC:', 'Unity:')):
                continue
            name, expected = line.rsplit(' ', 1)
            path = 'unity/Assets/' + name
            # The measurement fixture was extended before the baseline render.
            if not (version == 'before' and name == 'Editor/KnockdownReview.cs'):
                data = (subprocess.check_output(['git', 'show', BASELINE + ':' + path], cwd=ROOT)
                        if version == 'before' else (ROOT / path).read_bytes())
                if hashlib.sha256(data).hexdigest() != expected:
                    raise RuntimeError('Stale rendering source: ' + path)
            sources[version][path] = expected

    comparisons = {}
    for hero in HEROES:
        def rows(version):
            with (FOLDER / version / (hero + '-sequence.csv')).open() as handle:
                return list(csv.DictReader(handle))
        before, after = rows('before'), rows('after')
        keys = ('frame', 'action', 'age', 'hits', 'landings')
        if len(before) != 190 or [[r[k] for k in keys] for r in before] != [[r[k] for k in keys] for r in after]:
            raise RuntimeError('Combat timing changed: ' + hero)
        def landing_height(sequence):
            return statistics.mean(float(r['headY']) for r in sequence
                                   if r['action'] == 'Hurt' and .38 <= float(r['age']) <= .55)
        old, new = landing_height(before), landing_height(after)
        if old - new < .15:
            raise RuntimeError('Missing visible body recline: ' + hero)
        comparisons[hero] = dict(states_equal=True, samples=190,
                                landing_head_height_before=old, landing_head_height_after=new)
    log = (ROOT / 'logs/fall-body-rates-20261008.log').read_text()
    for hero in HEROES:
        if f'[KnockdownBoundary] {hero} pause=passed resumeGuard=passed counterpunch=passed newRound=passed' not in log:
            raise RuntimeError('Missing interruption evidence: ' + hero)
        if f'[RosterGuardTransitions] {hero} maxHandStep=' not in log:
            raise RuntimeError('Missing guard handoff: ' + hero)

    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Release fight/photo/replay failed')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(data / path) for value in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Release evidence is from another build')
    for version in ('before', 'after'):
        if len(list((FOLDER / version / 'frames').glob('*.png'))) != 95:
            raise RuntimeError('Incomplete continuous footage: ' + version)
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30',
                    '-i', str(FOLDER / 'before/frames/%04d.png'), '-framerate', '30',
                    '-i', str(FOLDER / 'after/frames/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '95', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(baseline=BASELINE, sources=sources, comparisons=comparisons, native=native, guided=guided, build=build,
                 media=dict(frames=95, export_fps=30, kind='offline Unity rendering, not a real-time FPS benchmark'))
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>受击落地与收脚起身对照</title>
<style>body{background:#101722;color:#edf4ff;font:16px/1.7 system-ui;margin:30px auto;max-width:1500px;padding:20px}p{max-width:1100px;color:#b7c6d7}video{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}img{width:100%}select{padding:9px;margin:12px}a{color:#88daf0}</style>
<h1>上身失衡落地，再撑地收脚起身</h1><p>本轮加深受击后的躯干倾角，收回悬空的右臂；先抬起身体，再让膝盖转向起身方向。补齐右臂、肩颈动画层恢复，暂停和新一局不会残留倒地偏移。</p>
<div class="pair"><b>修改前 · bdefd6e</b><b>当前</b></div><video src="comparison.mp4" controls loop playsinline></video>
<p>两侧采用同一输入与战斗时间，共 190 次状态采样、95 帧连续渲染。视频导出为 30 FPS，不表示游戏达到稳定 60 FPS。</p>
<select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select>
<select id="moment"><option value="landed">落地</option><option value="fall">失衡</option><option value="rising">收脚起身</option><option value="recovered">恢复</option></select>
<div class="pair"><img id="before"><img id="after"></div>
<p>五位现有骨骼英雄通过 15/30/60 Hz 步长、地面、构图、暂停、重开与防御衔接检查。泽塔、德凯缺少骨骼模型，明确跳过。发行包攻防和合照续局通过；体感协议回放使用合成姿势及照片，未拍摄或上传真人照片。</p>
<p>参考街机级角色精度、完整连续演出和真人电视现场仍待验收。</p><a href="validation.json">源码哈希、轨迹和发行包证据</a>
<script>const hero=document.getElementById('hero'),moment=document.getElementById('moment');function show(){for(const mode of ['before','after'])document.getElementById(mode).src=mode+'/'+hero.value+'-'+moment.value+'.png'}hero.onchange=moment.onchange=show;show()</script></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
