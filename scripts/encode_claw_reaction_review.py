"""Bind the claw reaction comparison to identical fights and the tested build."""
import csv
import hashlib
import json
import math
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/claw-reaction'
BASELINE='76df31bbbd6c4fb7cdf032b046a756a6eeadb34f'
MODES=('left','right','uppercut','beam')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result={}
    for line in (FOLDER/version/'sources.txt').read_text().splitlines():
        path,expected=line.rsplit(' ',1);relative='unity/Assets/'+path
        data=(subprocess.check_output(['git','show',BASELINE+':'+relative],cwd=ROOT)
              if version=='before' else (ROOT/relative).read_bytes())
        if hashlib.sha256(data).hexdigest()!=expected:
            raise RuntimeError('Stale render source: '+relative)
        result[relative]=expected
    if len(result)!=(2 if version=='before' else 3):
        raise RuntimeError('Incomplete source manifest')
    return result


def main():
    proof=dict(baseline_commit=BASELINE,before=sources('before'),after=sources('after'),motion={})
    for version in ('before','after'):
        if len(list((FOLDER/version/'frames').glob('*.png')))!=480:
            raise RuntimeError('Incomplete comparison frames')
    for mode in MODES:
        sequence=(FOLDER/f'before/{mode}-60-sequence.csv').read_bytes()
        if sequence!=(FOLDER/f'after/{mode}-60-sequence.csv').read_bytes():
            raise RuntimeError('Changed fight: '+mode)
        a=list(csv.DictReader((FOLDER/f'before/{mode}-60-motion.csv').open()))
        b=list(csv.DictReader((FOLDER/f'after/{mode}-60-motion.csv').open()))
        if len(a)!=240 or len(b)!=240:
            raise RuntimeError('Incomplete motion samples')
        errors={key:max(math.dist([float(x[key+c]) for c in 'XYZ'],[float(y[key+c]) for c in 'XYZ'])
                        for x,y in zip(a,b)) for key in ('left','right','root','footL','footR','chest')}
        if any(errors[key]>.0001 for key in ('root','footL','footR','chest')):
            raise RuntimeError('Arm reaction displaced the body, support or beam contact')
        if max(errors['left'],errors['right'])<.3:
            raise RuntimeError('The requested arm reaction was not rendered')
        proof['motion'][mode]=dict(sequence_sha256=hashlib.sha256(sequence).hexdigest(),max_position_difference=errors)
    validations={str(rate):(FOLDER/f'after/{rate}-validation.txt').read_text() for rate in (15,30,60)}
    if any(value.count('contacts=1')!=4 for value in validations.values()):
        raise RuntimeError('Missing reaction cases')
    stability=(FOLDER/'stability/stability.txt').read_text()
    if stability.count('passed')!=12:
        raise RuntimeError('Missing continuous attack, pause or restart validation')
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    app=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(h!=digest(app/file) for h in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Evidence does not match current app')
    if (native['result']!='passed' or not native['linked'] or not native['slam'] or not native['ray']
            or guided['result']!='passed' or guided['gesture_wobble']['unwanted_attacks']
            or not guided['replay_battle_started'] or guided['automatic_photos']!=2):
        raise RuntimeError('Combat, pose or photo regression')
    for mode in MODES:
        name='monster-arms-'+mode+'.png'
        shutil.copyfile(Path(native['evidence_directory'])/'native'/name,FOLDER/('native-'+mode+'.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error',
                    '-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
                    '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),
                    '-filter_complex','[0:v][1:v]hstack=inputs=2[v]','-map','[v]',
                    '-frames:v','480','-c:v','libx264','-crf','19','-pix_fmt','yuv420p',
                    '-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof.update(validations=validations,stability=stability,native=native,guided=guided,build=build,
                 media=dict(frames=480,fps=30,seconds=16,kind='offline render, not measured game frame rate'))
    (FOLDER/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>哥尔赞 · 双爪受力对照</title><style>body{margin:0;background:#101721;color:#edf4fa;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bfcedc}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}figure{margin:12px 0}select,button{font:inherit;padding:8px;background:#254459;color:inherit;border:1px solid #567e99}a{color:#8cdded}</style>
<main><h1>受击时，让双爪也有反应</h1><p>普通拳击中一侧时，该侧爪子向外弹开，另一只稍后收向胸前；上勾拳使双臂随腾空向外抬起，落地再收回；光线持续命中时，两只手以不同幅度抬起承力。身体、脚部支撑、胸口命中点与战斗时序保持原样。</p>
<div class="pair"><span>修改前：手臂基本跟随躯干摆动</span><span>当前：双臂错开受力、回收</span></div><video id="video" controls loop playsinline preload="metadata" src="comparison.mp4"></video><p><button data-time="0">左拳</button> <button data-time="4">右拳</button> <button data-time="8">上勾拳</button> <button data-time="12">光线</button></p><p>16 秒无声同步对照，每组 4 秒。两侧使用同一输入；960 个战斗状态一致，身体、双脚和胸口位置逐帧一致。30 FPS 是视频导出帧率，不是游戏实测帧率。</p>
<h2>实际打包版与关键帧</h2><select id="mode"><option value="left">左拳命中</option><option value="right">右拳命中</option><option value="uppercut">上勾拳</option><option value="beam">必杀光线</option></select><div class="pair"><figure><img id="before" alt="修改前"><figcaption>修改前</figcaption></figure><figure><img id="after" alt="修改后"><figcaption>当前</figcaption></figure></div><img id="native" alt="本次实际游戏画面">
<p>15／30／60 Hz 逻辑步长下检查手腕方向、骨长和零时间重复采样；额外检查连续受击、原有爪击、暂停恢复与新局。实际程序完成整局攻防、两次必杀、倒地和胜利，另一轮完成自动合照、重拍、中断恢复与举手续局；防御／大招抖动期间误触普攻为 0。输入和照片均为合成 TEST 数据，没有开启真人摄像头或调用合照 AI。</p>
<p>这一轮补足受力动作的层次，源模型的手掌形状、贴图精度和完整街机演出仍需继续提升。</p><p><a href="provenance.json">源码、构建与逐帧证据</a> · <a href="stability/stability.txt">连续动作检查</a> · <a href="guided/guided-validation.json">无键鼠流程</a></p>
<script>const mode=document.querySelector('#mode'),video=document.querySelector('#video');function show(){for(const v of ['before','after'])document.querySelector('#'+v).src=v+'/'+mode.value+'-60-impact.png';document.querySelector('#native').src='native-'+mode.value+'.png'}mode.onchange=show;show();document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{video.currentTime=Number(b.dataset.time);video.play()});</script></main></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':
    main()
