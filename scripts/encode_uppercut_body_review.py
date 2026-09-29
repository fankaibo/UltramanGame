"""Create the matched-camera full-body uppercut review from Unity renders."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/uppercut-body'
BASELINE='a9dc8db96f33a4d7aeed48b3c1373af649169b37'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    proof={'baseline':BASELINE,'sources':{},'validation':{}}
    for version in ('before','after'):
        capture=FOLDER/version
        rows=(capture/'launch-validation.txt').read_text().splitlines()
        if len(rows)!=40 or any(not row.endswith('passed') for row in rows):
            raise RuntimeError('Missing launch/interrupt/follow-up checks: '+version)
        proof['validation'][version]=rows
        proof['sources'][version]={}
        for line in (capture/'validation-sources.txt').read_text().splitlines():
            name,expected=line.rsplit(' ',1);path=ROOT/'unity/Assets'/name
            # Only the output-directory option was added to the capture helper
            # before baseline rendering. Runtime sources use the actual commit.
            data=(subprocess.check_output(['git','show',BASELINE+':unity/Assets/'+name],cwd=ROOT)
                  if version=='before' and not name.startswith('Editor/') else path.read_bytes())
            if hashlib.sha256(data).hexdigest()!=expected:
                raise RuntimeError('Stale render: '+version+'/'+name)
            proof['sources'][version][name]=expected
    ffmpeg=['/opt/homebrew/bin/ffmpeg','-y','-v','error']
    for side in ('left','right'):
        for version in ('before','after'):
            if len(list((FOLDER/version/'shots'/side/'frames').glob('*.png')))!=120:
                raise RuntimeError('Missing four-second sequence: '+version+'/'+side)
        subprocess.run(ffmpeg+['-framerate','30','-i',str(FOLDER/'before/shots'/side/'frames/%04d.png'),
            '-framerate','30','-i',str(FOLDER/'after/shots'/side/'frames/%04d.png'),
            '-filter_complex','[0:v][1:v]hstack=inputs=2[v]','-map','[v]','-frames:v','120',
            '-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/(side+'.mp4'))],check=True)
    stability=(FOLDER/'stability/stability.txt').read_text()
    if stability.count('passed')!=12:raise RuntimeError('Missing wrist/tail continuous sampling checks')
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,name in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        expected=digest(data/name)
        if any(value!=expected for value in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Native evidence belongs to another build')
    if native['result']!='passed' or native['launch_landings']<1 or not all(native[k] for k in ('slam','ray','linked')):
        raise RuntimeError('Incomplete native fight')
    if guided['result']!='passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Guided flow or gesture regression')
    for name in ('uppercut-airborne','uppercut-land','uppercut-recover'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/(name+'.png'),FOLDER/(name+'.png'))
    proof.update(native=native,guided=guided,build=build,stability=stability,
                 media={'kind':'Unity offline render, not measured game frame rate','fps':30,'seconds_per_side':4})
    (FOLDER/'validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>上挑受击 · 全身失衡</title><style>body{margin:0;background:#101821;color:#edf3fa;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{max-width:1100px;color:#b5c7d8}video,img{width:100%;display:block}button,select{font:inherit;padding:8px 16px;margin:8px 8px 8px 0;background:#254155;color:inherit;border:1px solid #52728a;border-radius:5px}.labels{display:flex;justify-content:space-around;padding:8px;background:#203140}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}a{color:#7edbed}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>让上挑重击带动整个身体</h1><p>格尔赞围绕髋部后仰、侧转，两腿按受击方向错开收起；尾巴稍后卷起，落地时恢复支撑。双爪随躯干受力，手腕继续受角度约束。来自同一真实 Unity 场景的连续画面，左侧为修改前，右侧为当前版本。</p>
<div class="labels"><b>修改前 · 站姿抬升</b><b>当前 · 后仰侧转、收腿、尾巴跟随</b></div><video id="movie" src="left.mp4" controls loop playsinline preload="metadata"></video>
<select id="side"><option value="left">左手上挑</option><option value="right">右手上挑</option></select><button onclick="movie.playbackRate=.5">半速</button><button onclick="movie.playbackRate=1">正常速度</button><p>每段 4 秒，包含命中、腾空、落地及恢复。导出视频为 30 FPS，不代表游戏实测帧率。两侧使用相同战斗输入与摄像机逻辑。</p>
<div class="pair"><img id="before" alt="修改前腾空"><img id="after" alt="当前腾空"></div>
<h2>实际打包版</h2><img src="uppercut-airborne.png" alt="实际游戏腾空画面"><div class="pair"><img src="uppercut-land.png" alt="落地"><img src="uppercut-recover.png" alt="恢复"></div>
<p>五位英雄左右上挑、空中追击、暂停、新局、必杀接管及临近怪兽攻击共 40 组检查通过。检查了实际蒙皮穿地、顶部 HUD 空间、落地滑动和追击接触；另检查手腕与尾巴在连续受击、暂停及新局中的重复采样。</p>
<p>当前 macOS 安装包完成整局攻防、合照、重拍和举手续局。输入为合成姿势，照片为 TEST 图像；未调用真人摄像头或合照 AI。整体场景、源模型和更多连续动作仍需继续提升，不能据此宣称达到最终街机效果。</p><a href="validation.json">源码、构建及验证数据</a>
<script>const movie=document.getElementById('movie'),side=document.getElementById('side');function show(){movie.src=side.value+'.mp4';const flag=side.value==='left'?'True':'False';for(const v of ['before','after'])document.getElementById(v).src=v+'/validation/Tiga-30-'+flag+'-normal-air.png'}side.onchange=show;show()</script></main></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':
    main()
