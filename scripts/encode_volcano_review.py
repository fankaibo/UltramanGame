"""Check capture provenance and encode the deterministic volcano comparison."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root / 'artifacts/volcano-volume'
    baseline = '53f690c4cb24029ed0ae8315c7e5a644efa67db3'
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30}
    for side in ('before', 'after'):
        capture = folder / side
        if not (capture / 'validation.txt').is_file() or len(list((capture / 'frames').glob('*.png'))) != 240:
            raise RuntimeError('Incomplete continuous render: ' + side)
        for line in (capture / 'sources.txt').read_text().splitlines():
            source, digest = line.rsplit(' ', 1)
            if side == 'after':
                data = (root / 'unity/Assets' / source).read_bytes()
            elif source == 'Editor/VolcanoStageReview.cs':
                data = (capture / 'VolcanoStageReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline + ':unity/Assets/' + source], cwd=root)
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale source: ' + side + '/' + source)
            proof['sources'].append({'side': side, 'path': source, 'sha256': digest})
    if (folder / 'before/sequence.csv').read_bytes() != (folder / 'after/sequence.csv').read_bytes():
        raise RuntimeError('Battle clocks differ across captures')
    record = json.loads((args.player / 'validation.json').read_text())
    app = root / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if hashlib.sha256((app / file).read_bytes()).hexdigest() != record[key]:
            raise RuntimeError('Native proof does not match the current build')
    if record['result'] != 'passed':
        raise RuntimeError('Native round did not pass')
    proof['player'] = record
    volume_checks = (folder / 'volume-validation.txt').read_text()
    if 'foregroundLeakPixels=0' not in volume_checks or 'zeroTime=passed' not in volume_checks:
        raise RuntimeError('Missing passed depth / frozen-time checks')
    proof['volume_checks'] = volume_checks.strip()
    guided = json.loads((folder / 'guided/guided-validation.json').read_text())
    guided_build = json.loads((folder / 'guided/build.json').read_text())
    for stage in ('before', 'after'):
        for key in ('assembly_sha256', 'resources_sha256'):
            if guided_build[stage][key] != record[key]:
                raise RuntimeError('Guided proof is from another build')
    if guided['result'] != 'passed' or not guided['replay_battle_started']:
        raise RuntimeError('Guided photo/replay did not pass')
    proof['guided'] = guided
    (folder / 'native').mkdir(exist_ok=True)
    for name in ('monster-threat-return', 'guard-impact', 'beam-sustain'):
        shutil.copy2(args.player / 'native' / (name + '.png'), folder / 'native' / (name + '.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(folder / 'comparison.mp4')], check=True)
    (folder / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>火山场景 · 体积烟柱与熔岩岩壳</title><style>body{margin:0;background:#101620;color:#e9edf5;font:16px/1.7 system-ui}main{max-width:1600px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:21px}p{color:#acbdd0}video,img{display:block;width:100%;border-radius:7px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.labels{display:flex;justify-content:space-around;background:#203040;padding:8px}button{font:inherit;padding:8px 16px;margin:8px;background:#253e52;color:white;border:1px solid #4b697e;border-radius:5px}a{color:#79d4ee}</style>
<main><h1>火山场景：烟柱、喷口与熔岩岩壳</h1><p>烟柱有世界空间的内部明暗和遮挡；熔岩从实体喷口流入带冷却岩壳的沟槽。远处富士山、前方战斗站位和引导节奏保持原样。</p>
<h2>同一段 8 秒攻防</h2><p>左侧为 53f690c，右侧为本轮修改。两侧各 240 帧，480 条战斗状态相同；离线 30 FPS 不代表游戏运行帧率。</p><div class="labels"><b>修改前</b><b>修改后</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button onclick="movie.playbackRate=1;movie.play()">正常速度</button><button onclick="movie.playbackRate=.5;movie.play()">半速</button>
<h2>喷口近景 · 专用检查机位</h2><p>检查烟柱与地面的关系，实际战斗使用下方打包版机位。</p><div class="labels"><b>修改前</b><b>修改后</b></div><div class="pair"><img src="before/vent-25.png"><img src="after/vent-25.png"></div>
<h2>当前 macOS 打包版 · 1080P</h2><p>合成输入完整回放，未开启真人摄像头。</p><img src="native/monster-threat-return.png"><img loading="lazy" src="native/guard-impact.png"><img loading="lazy" src="native/beam-sustain.png">
<p><a href="provenance.json">源码与构建来源</a> · <a href="volume-validation.txt">遮挡与时钟检查</a> · <a href="guided/guided-validation.json">合照与再开局验证</a></p><p>本轮提升场景层次；角色贴图与完整动作库的丰富度仍需继续提升。</p></main></html>''')
    print(folder / 'index.html')


if __name__ == '__main__':
    main()
