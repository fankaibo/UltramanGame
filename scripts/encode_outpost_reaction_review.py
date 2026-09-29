"""Verify source-bound scene reactions and export the synchronized comparison."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/outpost-reaction'
BASELINE='131f7969dd1b14286bad299e0e19a1cb94ac2e96'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    sources={}
    for version in ('before','after','inspection'):
        folder=FOLDER/version
        if version!='inspection':
            if len(list((folder/'frames').glob('*.png')))!=180 or (folder/'validation.txt').read_text()!='passed frames=180 samples=360 health=50 blocks=3 hurt=0':
                raise RuntimeError('Incomplete slam rendering: '+version)
        sources[version]={}
        for line in (folder/'sources.txt').read_text().splitlines():
            name,expected=line.rsplit(' ',1);path='unity/Assets/'+name
            data=(subprocess.check_output(['git','show',BASELINE+':'+path],cwd=ROOT)
                  if version=='before' and name!='Editor/OutpostReactionReview.cs' else (ROOT/path).read_bytes())
            if hashlib.sha256(data).hexdigest()!=expected:raise RuntimeError('Stale source: '+path)
            sources[version][path]=expected
    if (FOLDER/'before/sequence.csv').read_bytes()!=(FOLDER/'after/sequence.csv').read_bytes():
        raise RuntimeError('Building reaction changed the fight sequence')
    inspection=(FOLDER/'inspection/validation.txt').read_text()
    if any(token not in inspection for token in ('15Hz','30Hz','60Hz','restingFaces=2','GameWorld pause=passed newBattle=passed','frozenDifference=0 resetDifference=0')):
        raise RuntimeError('Missing motion, reset or GPU evidence')
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    if (native['result']!='passed' or not all(native[k] for k in ('slam','ray','linked')) or
            native['outpost_shocks']!=len(native['ground_contacts']) or native['outpost_detachments_scheduled']<4 or
            guided['result']!='passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']):
        raise RuntimeError('Native fight, reactions or replay failed')
    data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,path in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(value!=digest(data/path) for value in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Native evidence differs from the current app')
    for name in ('outpost-dust','outpost-settled','slam-wave','victory-hero'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/(name+'.png'),FOLDER/(name+'.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error','-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
        '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),'-filter_complex','[0:v][1:v]hstack=inputs=2[v]',
        '-map','[v]','-frames:v','180','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof=dict(baseline=BASELINE,sources=sources,inspection=inspection,native=native,guided=guided,build=build,
               media=dict(frames=180,seconds=6,export_fps=30,kind='offline Unity render, not actual player FPS'))
    (FOLDER/'validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>巨人重击 · 废墟受力</title><style>body{margin:0;background:#101820;color:#edf3f7;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{max-width:1100px;color:#b9cad8}img,video{width:100%;display:block}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}.labels{display:flex;justify-content:space-around;padding:8px;background:#243744}figure{margin:0}a{color:#82dbe9}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>重击传到两侧废墟</h1><p>怪兽砸地、击飞落地和倒地会沿地面传出震动。两侧松动的楼板或窗楣先晃动，再脱落、翻转、落地扬尘，最后翻平留在地面。本局后续重击会继续剥落尚未掉落的构件；暂停冻结，新一局恢复。</p>
<div class="labels"><b>修改前 · 建筑静止</b><b>当前 · 碎片掉落与尘团</b></div><video src="comparison.mp4" controls loop playsinline></video>
<p>同一组第三次怪兽砸地输入与镜头，360 条战斗状态完全相同，180 帧／6 秒并排对照。30 FPS 是离线导出速度，不代表当前游戏运行帧率。改动集中在画面两侧建筑，不改变中央角色的动作。</p>
<h2>近景检查：同一版本的一次重击</h2><div class="pair"><figure><img src="inspection/intact.png" alt="重击前的废墟"><figcaption>重击前</figcaption></figure><figure><img src="inspection/falling.png" alt="楼板脱落与扬尘"><figcaption>楼板脱落与扬尘</figcaption></figure><figure><img src="inspection/settled.png" alt="碎块留在地面"><figcaption>落地后翻平，破损保留</figcaption></figure><figure><img src="inspection/reset.png" alt="新局恢复"><figcaption>新一局恢复；与初始画面像素一致</figcaption></figure></div>
<h2>实际 macOS 游戏</h2><img src="outpost-dust.png" alt="实际游戏中的废墟尘团"><div class="pair"><img src="outpost-settled.png" alt="同局保留的碎块"><img src="slam-wave.png" alt="怪兽砸地"></div>
<p>18 个可脱落构件共用一组动态网格，16 个尘团循环复用。15／30／60 Hz 时间步验证了地形接触、法线、稳定落点、暂停、有限数量与复位；实际游戏还检查了重击事件与废墟反应一一对应。完整攻防、自动合照、重拍与举手续局通过，测试使用合成输入和 TEST 人像。</p>
<p>这仍是预设构件的受力演出，不是整栋建筑的结构物理模拟。近景模型、动作丰富度和真实合照融合仍需提升，整体街机目标尚未完成。</p><a href="validation.json">源码、渲染与安装包验证记录</a></main></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':
    main()
