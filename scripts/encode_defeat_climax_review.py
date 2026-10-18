"""Publish the source-bound landing comparison and actual built-player proof."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/defeat-climax'
BASELINE = '3b747b41442a7d2ce879379b695f818f1a2c9d02'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--beam-player', type=Path, required=True)
    parser.add_argument('--punch-player', type=Path, required=True)
    args = parser.parse_args()
    sources = {}
    for version in ('before', 'after'):
        folder = FOLDER / version
        assert len(list((folder / 'frames').glob('*.png'))) == 195
        assert 'victory=1 landings=1' in (folder / 'validation.txt').read_text()
        sources[version] = {}
        for line in (folder / 'render-source.txt').read_text().splitlines():
            if line.startswith(('UTC:', 'Unity:')):
                continue
            name, expected = line.rsplit(' ', 1)
            data = ((ROOT / 'unity/Assets' / name).read_bytes() if version == 'after' else
                    (folder / 'VictoryReview.cs').read_bytes() if name == 'Editor/VictoryReview.cs' else
                    subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/' + name], cwd=ROOT))
            assert hashlib.sha256(data).hexdigest() == expected, 'Stale render: ' + version + '/' + name
            sources[version][name] = expected
    assert (FOLDER / 'before/motion.csv').read_bytes() == (FOLDER / 'after/motion.csv').read_bytes()
    assert digest(ROOT / 'unity/Assets/Editor/DefeatImpactReview.cs') == (FOLDER / 'checks-source.txt').read_text().strip()
    checks = (FOLDER / 'checks.txt').read_text()
    assert checks.count('photo-clear=passed') == 3 and 'foregroundMismatch=0' in checks and 'finite=passed' in checks
    roster = (FOLDER / 'after/roster-validation.txt').read_text()
    assert all(name + ':' in roster for name in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'))
    players = [json.loads(path.read_text()) for path in (args.beam_player, args.punch_player)]
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    assert players[0]['finisher'] and not players[1]['finisher']
    assert all(p['result'] == 'passed' for p in players)
    assert guided['result'] == 'passed' and guided['replay_battle_started'] and guided['play_again']
    assert guided['gesture_wobble']['unwanted_attacks'] == 0
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        current = digest(app / file)
        assert all(item[key] == current for item in players + [build['before'], build['after']]), 'Another build was tested'
    for player in players:
        log = (Path(player['evidence_directory']) / 'player.log').read_text().split('[PresentationWarmup] complete', 1)[1]
        assert log.count('[DefeatImpact] begin landing=True') == log.count('[DefeatImpact] sound=True') == 1
    (FOLDER / 'native').mkdir(exist_ok=True)
    figures = ''
    for name, label in [('defeat-flash', '接触闪光'), ('defeat-billows', '烟尘展开'), ('defeat-settling', '烟尘消退'), ('victory-hero', '英雄庆祝')]:
        shutil.copy2(Path(players[0]['evidence_directory']) / 'native' / (name + '.png'), FOLDER / 'native' / (name + '.png'))
        figures += f'<figure><figcaption>{label}</figcaption><img loading="lazy" src="native/{name}.png" alt="{label}"></figure>'
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]', '-frames:v', '195',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(baseline=BASELINE, sources=sources, checks=checks, roster=roster, players=players, guided=guided, build=build,
                 media=dict(frames_per_side=195, seconds=6.5, fps=30, kind='offline Unity render, not runtime FPS'))
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>怪兽倒地 · 冲击与烟尘</title><style>body{margin:0;background:#101a25;color:#edf4fa;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#c2d1df;max-width:1100px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:0}a{color:#85ddec}button{font:inherit;background:#274150;color:white;border:0;border-radius:6px;padding:8px 16px;margin:8px}input{width:100%}</style>
<main><h1>怪兽倒地：闪光、烟尘和落地声</h1><p>参考录屏约 183 秒处的爆发收尾，给当前屈膝倒地补上短促暖光、滚动地尘和上升烟团。效果跟随实际脚下落点，保留头部与英雄动作，随英雄转身逐渐消散，合照前清场。</p>
<div class="pair"><b>修改前：少量碎石与尘点</b><b>当前：落地冲击与体积烟尘</b></div>
<video id="movie" controls loop playsinline src="comparison.mp4"></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速</button><button id="step">前进一帧</button><input id="scrub" aria-label="逐帧查看" type="range" min="0" max="194" value="0">
<p>左右均为实际 Unity 渲染，动作和机位保持相同。6.5 秒无声对照，离线 30 FPS 不代表运行帧率。</p>
<h2>新增落地气浪声</h2><audio controls src="landing-surge.wav"></audio><p>本地合成的低频与散落气浪，游戏中低音量叠加原有落地声；未替换迪迦原声与引导语音。</p>
<h2>当前 macOS 安装包实拍</h2><div class="pair">'''+figures+'''</div>
<p>原生程序分别通过普通拳与必杀光线收尾。合成体感回放覆盖防御与大招姿态抖动、自动合照、重拍、短暂断流和举手续局；没有使用真人摄像头或上传家庭照片。合成输入验证不能代替孩子试玩。</p>
<p><a href="checks.txt">遮挡、冻结、清理与音频检查</a> · <a href="after/roster-validation.txt">五英雄构图</a> · <a href="provenance.json">源码与构建记录</a></p><p>这是收尾演出的增量改进；整体画面仍需向参考街机继续提升，AI 合照融合也尚未完成。</p>
<script>const movie=document.querySelector('#movie'),scrub=document.querySelector('#scrub');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});scrub.oninput=()=>{movie.pause();movie.currentTime=Number(scrub.value)/30};movie.ontimeupdate=()=>scrub.value=Math.min(194,Math.round(movie.currentTime*30));document.querySelector('#step').onclick=()=>{movie.pause();movie.currentTime=Math.min(194/30,movie.currentTime+1/30)};</script></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
