"""Compare the same hit before/after, binding rendering and player evidence."""
import csv
import hashlib
import json
import math
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/grounded-rise'
BASELINE='b563b10a82a20998c94458eb6e599065d54f14b2'
HEROES=('Tiga','Mebius','Zero','Geed','Grigio')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rear_gap(rows):
    values=[]
    for row in rows:
        if row['action']!='Hurt' or not 1<=float(row['age'])<=1.60:continue
        hip=(float(row['hipX'])+float(row['hipZ']))/math.sqrt(2)
        feet=[(float(row[side+'X'])+float(row[side+'Z']))/math.sqrt(2) for side in ('left','right')]
        values.append(max(0,min(feet)-hip))
    return max(values)


def main():
    sources={};comparisons={}
    for version in ('before','after','rates/15','rates/30'):
        folder=FOLDER/version
        if not (folder/'validation.txt').exists():raise RuntimeError('Incomplete rendering: '+version)
        sources[version]={}
        for line in (folder/'render-source.txt').read_text().splitlines():
            if line.startswith(('Rendered UTC:','Unity:')):continue
            name,expected=line.rsplit(' ',1);path='unity/Assets/'+name
            # Baseline fixture was extended before runtime changes. Its hash
            # remains recorded, while runtime files must match the base commit.
            if version=='before' and name=='Editor/KnockdownReview.cs':
                sources[version][path]=expected;continue
            data=(subprocess.check_output(['git','show',BASELINE+':'+path],cwd=ROOT)
                  if version=='before' else (ROOT/path).read_bytes())
            if hashlib.sha256(data).hexdigest()!=expected:raise RuntimeError('Stale source: '+path)
            sources[version][path]=expected
    for name in HEROES:
        before=list(csv.DictReader((FOLDER/'before'/(name+'-sequence.csv')).open()))
        after=list(csv.DictReader((FOLDER/'after'/(name+'-sequence.csv')).open()))
        keys=('frame','action','age','hits','landings')
        if len(before)!=190 or [[r[k] for k in keys] for r in before]!=[[r[k] for k in keys] for r in after]:
            raise RuntimeError('Recovery changed combat timing: '+name)
        old,new=rear_gap(before),rear_gap(after)
        if old<.5 or new>.20:raise RuntimeError('Missing support improvement: '+name)
        planted=[r for r in after if r['action']=='Hurt' and .88<float(r['age'])<1.37]
        anchor=planted[0]
        drift=max(math.hypot(float(r['rightX'])-float(anchor['rightX']),float(r['rightZ'])-float(anchor['rightZ'])) for r in planted)
        if drift>.015:raise RuntimeError('Right support slides while the left foot steps: '+name)
        comparisons[name]={'states_equal':True,'samples':190,'hips_behind_rearmost_foot_before':round(old,5),
                           'hips_behind_rearmost_foot_after':round(new,5),'right_support_drift':round(drift,6)}
    log=(ROOT/'logs/grounded-rise-rates.log').read_text()
    for name in HEROES:
        if '[KnockdownBoundary] '+name+' pause=passed resumeGuard=passed counterpunch=passed newRound=passed' not in log:
            raise RuntimeError('Missing interruption evidence: '+name)
        if '[RosterGuardTransitions] '+name not in log:raise RuntimeError('Missing guard handoff: '+name)
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    if native['result']!='passed' or guided['result']!='passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Native fight or guided loop failed')
    data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,path in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(value!=digest(data/path) for value in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Player evidence is not the current build')
    for name in ('hero-landed','hero-rising','hero-recovered'):
        src=Path(native['evidence_directory'])/'native'/(name+'.png')
        if not src.exists():raise RuntimeError('Missing native recovery image: '+name)
        shutil.copyfile(src,FOLDER/(name+'.png'))
    for version in ('before','after'):
        if len(list((FOLDER/version/'frames').glob('*.png')))!=95:raise RuntimeError('Missing animation frames')
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error','-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
        '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),'-filter_complex','[0:v][1:v]hstack=inputs=2[v]',
        '-map','[v]','-frames:v','95','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof=dict(baseline=BASELINE,sources=sources,comparisons=comparisons,native=native,guided=guided,build=build,
               media={'frames':95,'export_fps':30,'kind':'offline Unity render; not a player FPS benchmark'})
    (FOLDER/'validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>倒地后，撑地收脚再站起</title><style>body{margin:0;background:#111922;color:#edf4ff;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{max-width:1100px;color:#b8cad9}video,img{width:100%;display:block}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}button,select{font:inherit;background:#234255;color:white;border:0;border-radius:6px;padding:8px;margin:6px}a{color:#7ddbeb}</style>
<main><h1>倒地后，先把脚收回身体下方</h1><p>旧动作在双脚仍留在前方时就抬起身体，看起来像坐在半空。现在右脚先收回，左手撑住身体，再换左脚收步并站起。落地声音和受击时长保持一致，角色恢复后继续接受防御与出拳。</p>
<div class="pair"><b>修改前</b><b>当前</b></div><video src="comparison.mp4" controls loop playsinline></video>
<p>两侧使用相同战斗输入和时间，共 190 次状态采样；95 帧／约 3.2 秒连续对照。30 FPS 为离线视频导出速度，与游戏帧率优化无关。</p>
<select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select>
<select id="moment"><option value="rising">收脚起身</option><option value="landed">倒地撑住</option><option value="recovered">恢复站姿</option><option value="fall">受击失衡</option></select>
<div class="pair"><img id="before"><img id="after"></div>
<h2>实际 macOS 游戏画面</h2><img src="hero-rising.png" alt="实际游戏中的收脚起身"><div class="pair"><img src="hero-landed.png" alt="受击落地"><img src="hero-recovered.png" alt="起身回到战斗"></div>
<p>五位英雄通过 15／30／60 Hz 更新步长、地面与画幅、重复采样、起身中途暂停、恢复防御和反击、重新开局检查。实际程序完整攻防及自动合照续局通过，体感回放使用合成姿势，未拍摄或上传家庭照片。</p>
<p>这是已有骨骼上的分阶段动作修正，仍需继续提升完整战斗编排与角色细节，整体街机目标尚未完成。</p><a href="validation.json">源码、轨迹及当前安装包验证记录</a>
<script>const hero=document.getElementById('hero'),moment=document.getElementById('moment');function show(){for(const mode of ['before','after'])document.getElementById(mode).src=mode+'/'+hero.value+'-'+moment.value+'.png'}hero.onchange=moment.onchange=show;show()</script></main></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':main()
