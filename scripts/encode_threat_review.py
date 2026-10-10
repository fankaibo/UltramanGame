"""Verify and encode the warning approach / return camera comparison."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path)
    parser.add_argument('--folder', type=Path)
    parser.add_argument('--baseline', default='28915835ff18fe0b7c554560b9e31348225ab2b2')
    parser.add_argument('--subject', choices=('camera', 'windup'), default='camera')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = args.folder.resolve() if args.folder else root / 'artifacts/threat-camera'
    baseline = args.baseline
    proof = {'baseline': baseline, 'sources': [], 'offline_fps': 30, 'player': None}
    for side, heroes in [('before', ['Tiga']), ('after', ['Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'])]:
        for hero in heroes:
            path = folder / side / hero
            if not (path / 'validation.txt').is_file():
                raise RuntimeError('Missing passed camera review: ' + str(path))
            for line in (path / 'sources.txt').read_text().splitlines():
                source, digest = line.rsplit(' ', 1)
                if side == 'after':
                    data = (root / 'unity/Assets' / source).read_bytes()
                elif source == 'Editor/ThreatCameraReview.cs':
                    data = (folder / 'before/ThreatCameraReview.cs').read_bytes()
                else:
                    data = subprocess.check_output(['git', 'show', baseline + ':unity/Assets/' + source], cwd=root)
                if hashlib.sha256(data).hexdigest() != digest:
                    raise RuntimeError('Stale source: ' + side + '/' + source)
                proof['sources'].append({'side': side, 'hero': hero, 'path': source, 'sha256': digest})
            if hero == 'Tiga' and len(list((path / 'frames').glob('*.png'))) != 240:
                raise RuntimeError('Missing continuous frames')
    if (folder / 'before/Tiga/sequence.csv').read_bytes() != (folder / 'after/Tiga/sequence.csv').read_bytes():
        raise RuntimeError('Before/after battle timings differ')
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder / 'before/Tiga/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder / 'after/Tiga/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(folder / 'comparison.mp4')], check=True)
    native = ''
    if args.player:
        record = json.loads((args.player / 'validation.json').read_text())
        app = root / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
        for key, path in [('assembly_sha256', app / 'Managed/Assembly-CSharp.dll'), ('resources_sha256', app / 'resources.assets')]:
            if hashlib.sha256(path.read_bytes()).hexdigest() != record[key]:
                raise RuntimeError('Player evidence is from another build')
        if record['result'] != 'passed':
            raise RuntimeError('Player did not pass')
        proof['player'] = record
        (folder / 'native').mkdir(exist_ok=True)
        for name in ('monster-threat', 'monster-threat-return', 'guard-impact'):
            shutil.copy2(args.player / 'native' / (name + '.png'), folder / 'native' / (name + '.png'))
            native += f'<img loading="lazy" src="native/{name}.png" alt="{name}">'
    (folder / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>怪兽蓄力镜头对照</title><style>body{margin:0;background:#0d121c;color:#e4edf8;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:26px}h1{font-size:28px}h2{font-size:21px}p{color:#acbdd0}video,img{display:block;width:100%;border-radius:8px}.labels{display:flex;justify-content:space-around;background:#182737;padding:10px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}button,select{font:inherit;padding:7px 14px;background:#1b3044;color:white;border:1px solid #405c78;border-radius:6px;margin:6px}a{color:#75d6ed}</style>
<main><h1>怪兽蓄力：前推、停留、返回</h1><p>较低机位靠近双方上身，保留头部和护盾；蓄力第 3.4 秒回到完整对战构图，最短预警仍留出 2 秒再出手。识别、语音反应时间、攻击和格挡时点保持一致。</p>
<h2>8 秒连续对照</h2><p>左侧为 2891583，右侧为新版。两侧各 240 帧，480 个战斗状态样本相同。离线 30 FPS 不代表游戏运行帧率。</p><div class="labels"><b>修改前</b><b>修改后</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速看衔接</button>
<h2>五位英雄 · 蓄力近景与返回</h2><select id="hero"><option>Tiga</option><option>Mebius</option><option>Zero</option><option>Geed</option><option>Grigio</option></select><div class="labels"><b>蓄力近景</b><b>即将返回完成</b></div><div class="pair"><img id="near"><img id="wide"></div>
<h2>本轮 1080P 打包版</h2><p>合成输入回放，保留实际游戏界面；未使用真人摄像头。</p>'''+native+'''
<p><a href="provenance.json">源码与构建来源</a> · <a href="flow-validation.txt">中断与时序验证</a></p><p>本轮改善镜头节奏，角色素材和完整动画丰富度仍需继续提升。</p>
<script>const movie=document.querySelector('#movie');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});const hero=document.querySelector('#hero');function show(){document.querySelector('#near').src='after/'+hero.value+'/stage-95.png';document.querySelector('#wide').src='after/'+hero.value+'/stage-200.png'}hero.onchange=show;show();</script></main></html>'''
    page = page.replace('左侧为 2891583', '左侧为 ' + baseline[:7])
    if args.subject == 'windup':
        page = page.replace('怪兽蓄力镜头对照', '怪兽收爪与连续蓄力对照').replace('怪兽蓄力：前推、停留、返回', '怪兽蓄力：收爪、转肩与蓄势')
        page = page.replace('较低机位靠近双方上身，保留头部和护盾；蓄力第 3.4 秒回到完整对战构图，最短预警仍留出 2 秒再出手。识别、语音反应时间、攻击和格挡时点保持一致。',
                            '预警动画从长时间定格改为连续收爪和转肩蓄力，等待时保持轻微呼吸，最后回到前冲准备姿势。两侧使用相同镜头、输入、预警和格挡时点。')
        page = page.replace('本轮改善镜头节奏', '本轮改善怪兽的连续动作')
    (folder / 'index.html').write_text(page)
    print(folder / 'index.html')


if __name__ == '__main__':
    main()
