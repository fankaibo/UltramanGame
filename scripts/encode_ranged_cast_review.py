"""Validate source/build provenance and export remote-skill before/after footage."""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/ranged-cast-20261009'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    reports, sources = [], {}
    for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'):
        for hz in (15, 30, 60):
            folder = FOLDER / 'after' / f'{hero}-{hz}'
            report = (folder / 'validation.txt').read_text()
            for expected in ('hits=3 launches=3 impacts=3', 'launchVolume=True impactVolume=True', 'pool=stable pause=pass'):
                if expected not in report:
                    raise RuntimeError('Incomplete model validation: ' + report)
            reports.append(report + ' ' + (folder / 'pose.txt').read_text())
            for row in (folder / 'sources.txt').read_text().splitlines():
                name, expected = row.rsplit(' ', 1)
                if sha(ROOT / 'unity/Assets' / name) != expected:
                    raise RuntimeError('Stale rendered source: ' + name)
                sources[name] = expected
    lifecycle = (FOLDER / 'transitions.txt').read_text().splitlines()
    if len(lifecycle) != 15 or any('intentAndConfirmed=true' not in row or 'rangeToMeleeGuard=pass' not in row for row in lifecycle):
        raise RuntimeError('Lifecycle review incomplete')
    columns = ('frame', 'action', 'age', 'shotAge', 'punches', 'health', 'energy', 'sequence')
    for hero in ('Tiga', 'Zero'):
        baseline_sources = dict(row.rsplit(' ', 1) for row in
                                (FOLDER / 'before' / (hero+'-60') / 'sources.txt').read_text().splitlines())
        rig_path = 'Scripts/Runtime/RiggedActor.cs'
        baseline_rig = subprocess.check_output(['git', 'show', '2d25eb0:unity/Assets/' + rig_path], cwd=ROOT)
        if hashlib.sha256(baseline_rig).hexdigest() != baseline_sources[rig_path]:
            raise RuntimeError('Baseline cast source differs: ' + hero)
        traces = [list(csv.DictReader((FOLDER / version / (hero+'-60') / 'trace.csv').open())) for version in ('before', 'after')]
        if any(len(rows) != 300 for rows in traces):
            raise RuntimeError('Missing five-second trace')
        if any(any(a[key] != b[key] for key in columns) for a, b in zip(*traces)):
            raise RuntimeError('Gameplay differs: ' + hero)
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = {hero: json.loads((FOLDER / ('guided-'+hero.lower()) / 'guided-validation.json').read_text()) for hero in ('Tiga', 'Zero')}
    builds = {hero: json.loads((FOLDER / ('guided-'+hero.lower()) / 'build.json').read_text()) for hero in guided}
    if native['result'] != 'passed':
        raise RuntimeError('Melee Release regression failed')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        current = sha(data / file)
        records = [native]+[entry for build in builds.values() for entry in (build['before'], build['after'])]
        if any(entry[key] != current for entry in records):
            raise RuntimeError('Release hash mismatch: ' + key)
    for hero, result in guided.items():
        if result['result'] != 'passed' or result['battle_heroes'][0] != hero:
            raise RuntimeError('Wrong guided hero: ' + hero)
        if result['tempo_skills']['impacts'] != result['tempo_skills']['launches']:
            raise RuntimeError('Remote impacts duplicated or lost: ' + hero)
    for hero in ('Tiga', 'Zero'):
        command = ['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error']
        for version in ('before', 'after'):
            frames = FOLDER / version / (hero+'-60') / 'frames'
            if len(list(frames.glob('[0-9][0-9][0-9][0-9].png'))) != 150:
                raise RuntimeError('Missing frames: ' + str(frames))
            command += ['-framerate', '30', '-i', str(frames / '%04d.png')]
        command += ['-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-c:v', 'libx264',
                    '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / (hero+'-comparison.mp4'))]
        subprocess.run(command, check=True)
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-i', str(FOLDER / 'Tiga-comparison.mp4'),
                    '-i', str(FOLDER / 'Zero-comparison.mp4'), '-filter_complex', '[0:v][1:v]concat=n=2:v=1:a=0[v]',
                    '-map', '[v]', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(FOLDER / 'ranged-comparison.mp4')], check=True)
    report = dict(baseline='2d25eb0', implementation='650b286', sources=sources, model_checks=reports, lifecycle=lifecycle, gameplay_identical_frames=600,
                  native=native, guided=guided, build=builds,
                  media=dict(frames=300, export_fps=30, kind='offline Unity rendering, not a runtime FPS measurement'))
    (FOLDER / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>远程出招与收手衔接</title><style>body{max-width:1500px;margin:24px auto;padding:20px;background:#111923;color:#edf4ff;font:16px/1.7 system-ui}p{color:#bfcbdb}video{width:100%}a{color:#6fd8ff}</style>
<h1>远程出招与收手衔接</h1><p>左旧右新：迪迦发射光弹时保持重心，以转肩、挥臂带动发射；赛罗抬手至头侧再引导头镖。两位英雄均按实际动作速度收招，并从当前手的位置进入防御。</p>
<h2>迪迦：慢、中、快三次出招</h2><video src="Tiga-comparison.mp4" controls autoplay muted loop></video>
<h2>赛罗：头侧准备、挥臂引导与收手</h2><video src="Zero-comparison.mp4" controls autoplay muted loop></video>
<p>两段各 300 帧状态轨迹的动作时钟、血量、能量和命中完全一致；五位骨骼英雄的快慢技能以及左右手／防御接管／暂停／新局／近战切换专项通过。迪迦和赛罗分别经过发行包合成体感整局、自动合照和续局。</p>
<p>这是实际 Unity 代码的离线连续渲染，30 FPS 导出不代表实机性能；精细武器资产、真实儿童和电视体验仍待完善。<a href="validation.json">查看源码与发行包证据</a></p></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
