"""Source-checked before/after evidence for the fifth-punch presentation."""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player', type=Path, action='append', default=[])
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/combo-strike'
    baseline = '621c4f153f9aed255f53d1dda6b2a630f76a6e17'
    proof = {'baseline': baseline, 'offline_video_fps': 30, 'source_checks': [], 'players': []}
    for side in ('before', 'after'):
        for line in (folder/side/'sources.txt').read_text().splitlines():
            path, digest = line.rsplit(' ', 1)
            if not re.fullmatch('[0-9a-f]{64}', digest):
                raise RuntimeError('Invalid source evidence')
            if side == 'after':
                data = (root/'unity/Assets'/path).read_bytes()
            elif path == 'Editor/ComboStrikeReview.cs':
                data = (folder/'before/ComboStrikeReview.cs').read_bytes()
            else:
                data = subprocess.check_output(['git', 'show', baseline+':unity/Assets/'+path], cwd=root)
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError('Stale source: '+side+'/'+path)
            proof['source_checks'].append({'side': side, 'path': path, 'sha256': digest})
        if len(list((folder/side/'frames').glob('*.png'))) != 210:
            raise RuntimeError('Missing animation frames')
    if (folder/'before/sequence.csv').read_bytes() != (folder/'after/sequence.csv').read_bytes():
        raise RuntimeError('Battle inputs or timing differ')
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(folder/'before/frames/%04d.png'),
                    '-framerate', '30', '-i', str(folder/'after/frames/%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]', '-map', '[v]',
                    '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(folder/'comparison.mp4')], check=True)
    native = ''
    for directory in args.player:
        record = json.loads((directory/'validation.json').read_text())
        app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
        for key, path in [('assembly_sha256', app/'Managed/Assembly-CSharp.dll'), ('resources_sha256', app/'resources.assets')]:
            if hashlib.sha256(path.read_bytes()).hexdigest() != record[key]:
                raise RuntimeError('Player evidence is from another build')
        if record['result'] != 'passed':
            raise RuntimeError('Player review did not pass')
        proof['players'].append(record)
        (folder/'native').mkdir(exist_ok=True)
        for side in ('left', 'right'):
            name = record['hero']+'-combo-'+side+'.png'
            shutil.copy2(directory/'native'/('combo-'+side+'.png'), folder/'native'/name)
            native += '<img loading="lazy" src="native/'+name+'" alt="'+record['hero']+' '+side+'">'
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>连击转肩与怪兽受力对照</title><style>body{margin:0;background:#0d121c;color:#e4edf8;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:26px}h1{font-size:28px}h2{font-size:21px;margin-top:34px}p{color:#aabbd1}video,img{display:block;width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.labels{display:flex;justify-content:space-around;padding:10px;background:#182737}select,button{font:inherit;padding:7px 14px;background:#1b3044;color:white;border:1px solid #405c78;border-radius:6px;margin:6px}a{color:#75d6ed}</style>
<main><h1>连击转肩与怪兽受力</h1><p>每第 5 次普攻加入外侧挥入、转肩和收手，怪兽随左右拳侧转后仰。五位英雄共用原来的识别与伤害规则：十拳仍扣 10 点、充能 10 次。</p>
<h2>十次交替出拳 · 同输入连续对照</h2><p>左侧为 621c4f1，右侧为新版。两侧各 210 帧、7 秒，420 个战斗时钟与血量样本一致。第 5、10 拳展示不同方向。离线 30 FPS 不是游戏运行帧率。</p><div class="labels"><b>修改前</b><b>修改后</b></div><video id="movie" src="comparison.mp4" controls loop playsinline></video><button data-speed="1">正常速度</button><button data-speed="0.5">半速看衔接</button>
<h2>五英雄左右连击接触</h2><label>角色<select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select></label><div class="labels"><b>左手 · 第 5 拳</b><b>右手 · 第 10 拳</b></div><div class="pair"><img id="left"><img id="right"></div>
<h2>本轮打包版 · 1080P 截图</h2><p>下图来自当前 macOS 游戏进程的合成输入回放，保留真实界面。没有使用真人摄像头。</p><div class="pair">'''+(native or '<p>玩家截图尚未附加。</p>')+'''</div>
<p><a href="provenance.json">来源与构建记录</a> · <a href="validation/validation.txt">胸部接触与支撑脚检查</a> · <a href="validation/interruptions.txt">防御、暂停与新局检查</a></p><p>角色素材与完整演出仍需继续改进，这轮不代表已经达到参考街机的最终品质。</p>
<script>const movie=document.querySelector('#movie');document.querySelectorAll('[data-speed]').forEach(b=>b.onclick=()=>{movie.playbackRate=Number(b.dataset.speed);movie.play()});const hero=document.querySelector('#hero');function show(){document.querySelector('#left').src='validation/'+hero.value+'-60-contact-5.png';document.querySelector('#right').src='validation/'+hero.value+'-60-contact-10.png'}hero.onchange=show;show();</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
