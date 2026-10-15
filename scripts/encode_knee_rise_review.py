"""Verify the reproduced knee defect, then encode continuous before/after evidence."""
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/knee-rise-20261008'
HEROES = ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    rig = ROOT / 'unity/Assets/Scripts/Runtime/RiggedActor.cs'
    baseline = subprocess.check_output(['git', 'show', '0bf95f6:unity/Assets/Scripts/Runtime/RiggedActor.cs'], cwd=ROOT)
    if hashlib.sha256(baseline).hexdigest() != (FOLDER / 'before/Tiga-60/rig-sha256.txt').read_text():
        raise RuntimeError('Baseline rig mismatch')
    reports = []
    for hero in HEROES:
        for hz in (15, 30, 60):
            folder = FOLDER / f'after/{hero}-{hz}'
            if (folder / 'rig-sha256.txt').read_text() != digest(rig):
                raise RuntimeError('Stale pose rendering: ' + str(folder))
            report = (folder / 'validation.txt').read_text()
            if 'hits=1 landings=1 recovered=True' not in report or 'reversedKnees=0 ' not in report:
                raise RuntimeError('Invalid knees/recovery: ' + report)
            reports.append(report)
    baseline_report = (FOLDER / 'before/Tiga-60/validation.txt').read_text()
    if int(re.search(r'reversedKnees=(\d+)', baseline_report)[1]) < 20:
        raise RuntimeError('Original defect was not reproduced')
    rows = {}
    for version in ('before', 'after'):
        with (FOLDER / version / 'Tiga-60/trace.csv').open() as stream:
            rows[version] = list(csv.DictReader(stream))
        if len(rows[version]) != 300 or len(list((FOLDER / version / 'Tiga-60/frames').glob('*.png'))) != 150:
            raise RuntimeError('Incomplete five-second footage: ' + version)
    keys = ('frame', 'action', 'age', 'hits', 'landings')
    if [[row[k] for k in keys] for row in rows['before']] != [[row[k] for k in keys] for row in rows['after']]:
        raise RuntimeError('Hit or recovery timing changed')
    boundary_log = (ROOT / 'logs/knee-boundaries.log').read_text()
    for hero in HEROES:
        if f'[KnockdownBoundary] {hero} pause=passed resumeGuard=passed counterpunch=passed newRound=passed' not in boundary_log:
            raise RuntimeError('Missing interruption evidence: ' + hero)
    native = json.loads((FOLDER / 'native/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    if native['result'] != 'passed' or guided['result'] != 'passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Integrated Release failed')
    if not (Path(native['evidence_directory']) / 'native/hero-rise-support.png').exists():
        raise RuntimeError('Missing native support pose')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(data / path) for value in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Evidence belongs to another Release')
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30',
                    '-i', str(FOLDER / 'before/Tiga-60/frames/%04d.png'), '-framerate', '30',
                    '-i', str(FOLDER / 'after/Tiga-60/frames/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '150', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(baseline='0bf95f6', implementation='4e1614a', rig_sha256=digest(rig), baseline_report=baseline_report,
                 reports=reports, unchanged_state_samples=300, native=native, guided=guided, build=build,
                 media=dict(frames=150, fps=30, offline=True, realtime_fps_claim=False))
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<title>起身膝盖修复</title><style>body{background:#101722;color:#edf4ff;font:16px/1.7 system-ui;max-width:1500px;margin:30px auto;padding:20px}video,img{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}a{color:#88daf0}</style>
<h1>石头击倒后的起身：双膝随脚尖转向</h1><p>用户截图精确对应旧录像 rock-hurt/0104.png。旧逻辑把右膝导向身后、左膝导向侧方；现改为收脚时逐步转向脚尖，再撑起身体。伤害、起身时长和后续输入规则不变。</p>
<div class="pair"><b>修复前</b><b>修复后</b></div><video src="comparison.mp4" controls loop playsinline></video>
<div class="pair"><img src="before/Tiga-60/rise.png"><img src="after/Tiga-60/rise.png"></div>
<p>五位现有骨骼英雄 × 15/30/60 Hz 完整起身与中断检查通过。300 帧战斗状态前后一致。视频是离线 Unity 序列，30 FPS 导出，不是稳定帧率测量。新发行包另做整局攻防与合成体感合照续局回放；真人儿童和电视现场尚未验收。</p><a href="validation.json">源码、测试与发行包证据</a></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
