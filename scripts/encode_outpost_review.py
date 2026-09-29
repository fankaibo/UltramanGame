"""Encode the outpost comparison only when renders and player evidence agree."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/volcano-outpost'
BASELINE = '9ed62ed9919a96f7a97168fd670f27ba01f52149'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    proof = dict(baseline=BASELINE, sources={}, offline_fps=30)
    for side in ('before', 'after'):
        capture = FOLDER / side
        if len(list((capture / 'frames').glob('*.png'))) != 240:
            raise RuntimeError('Incomplete stage capture')
        if 'repeat=passed health=50 blocks=1 hurt=0' not in (capture / 'validation.txt').read_text():
            raise RuntimeError('Stage capture failed')
        sources = {}
        for line in (capture / 'sources.txt').read_text().splitlines():
            source, expected = line.rsplit(' ', 1)
            relative = 'unity/Assets/' + source
            data = ((ROOT / relative).read_bytes() if side == 'after' else
                    subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT))
            if hashlib.sha256(data).hexdigest() != expected:
                raise RuntimeError('Changed capture source: ' + relative)
            sources[relative] = expected
        proof['sources'][side] = sources
    if (FOLDER / 'before/sequence.csv').read_bytes() != (FOLDER / 'after/sequence.csv').read_bytes():
        raise RuntimeError('Comparison changed battle timing')
    assets = ROOT / 'unity/Assets/Resources/Art/Outpost'
    source_info = json.loads((assets / 'sources.json').read_text())
    for file in source_info['files']:
        if sha(assets / file['file']) != file['sha256']:
            raise RuntimeError('Downloaded asset differs from recorded source')
    flow = (FOLDER / 'finisher/after/flow-validation.txt').read_text()
    for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'):
        if hero + ' 60fps flight=' not in flow:
            raise RuntimeError('Missing hero finisher flow')
    if flow.count('pause-and-reset=passed') != 3:
        raise RuntimeError('Finisher interruption failed')
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(h != sha(app / file) for h in (native[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Evidence does not match current app')
    if (native['result'] != 'passed' or not native['slam'] or not native['ray'] or not native['linked']
            or guided['result'] != 'passed' or guided['gesture_wobble']['unwanted_attacks']
            or not guided['replay_battle_started'] or guided['automatic_photos'] != 2):
        raise RuntimeError('Combat or guided-flow regression')
    (FOLDER / 'native').mkdir(exist_ok=True)
    for name in ('battle', 'guard-impact', 'beam-reaction-peak', 'victory-hero'):
        shutil.copyfile(Path(native['evidence_directory']) / 'native' / (name + '.png'), FOLDER / 'native' / (name + '.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error',
                    '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-frames:v', '240', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p',
                    '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof.update(native=native, guided=guided, build=build, hero_flow=flow, assets=source_info,
                 comparison=dict(seconds=8, frames=240, game_state_samples=480))
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>火山基地 · 巨人尺度</title><style>body{margin:0;background:#111821;color:#edf3fa;font:16px/1.75 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:21px}p{max-width:1100px;color:#bdcbd9}video,img{width:100%;border-radius:7px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}button{font:inherit;padding:8px 18px;margin:8px;background:#26485b;color:white;border:1px solid #698296}a{color:#8cd9ef}</style>
<main><h1>让道路与残墙衬出巨大英雄</h1><p>火山地面加入废弃观测基地：露出内部的混凝土残墙、钢筋、坍塌车棚、破损储罐、断裂道路及两辆小型车辆。富士山、熔岩与烟柱保留；双方脚步和攻击位置不变。</p>
<div class="pair"><b>修改前 · 岩石场地</b><b>当前 · 火山基地废墟</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><p>同一段 8 秒攻防、相同镜头和输入；480 条战斗状态完全一致。30 FPS 是离线视频导出帧率，不是游戏实测帧率。</p>
<h2>同位置切换</h2><button onclick="still.src='before/battle-420.png'">修改前</button><button onclick="still.src='after/battle-420.png'">当前</button><img id="still" src="after/battle-420.png" alt="火山基地战斗构图">
<h2>最终打包版 · 实际画面</h2><img src="native/battle.png" alt="实际战斗镜头"><div class="pair"><img src="native/guard-impact.png" alt="实际防御"><img src="native/beam-reaction-peak.png" alt="实际大招近景"></div><img src="native/victory-hero.png" alt="实际胜利画面">
<p>五位英雄的必杀发射、命中、回位和三种中断检查通过。最终程序完成整局攻防与自动合照、重拍、断流恢复和举手续局；防御及大招抖动期间误触普攻为 0。输入和人像均为合成 TEST 数据，没有使用真人摄像头或调用合照 AI。</p>
<p>混凝土扫描纹理来自 <a href="https://polyhaven.com/a/rebar_reinforced_concrete">Poly Haven / Amal Kumar</a>，许可 CC0。废墟几何在本项目生成，合并成五组静态网格。建筑破损仍是有限的固定形态；角色动作丰富度、近景模型和真实合照融合仍需提升。</p><p><a href="provenance.json">素材、源码及构建证据</a> · <a href="finisher/after/flow-validation.txt">五英雄必杀流程</a> · <a href="guided/guided-validation.json">无键鼠回放</a></p></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
