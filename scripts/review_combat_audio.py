"""Compare final Unity mixer recordings, not just generated source clips."""
import hashlib
import json
import subprocess
from pathlib import Path

import numpy as np
from scipy.io import wavfile

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/combat-audio'
BASELINE='4b1328fca804a4576335ea8cd5d22ecc0133a50b'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tone(pcm,rate,start,end):
    x=pcm[round(start*rate):round(end*rate)].mean(axis=1)
    window=np.hanning(len(x));size=2**int(np.ceil(np.log2(len(x)*16)))
    spectrum=np.abs(np.fft.rfft(x*window,n=size));freq=np.fft.rfftfreq(size,1/rate)
    keep=(freq>550)&(freq<650);index=np.flatnonzero(keep)[np.argmax(spectrum[keep])]
    return dict(hz=round(float(freq[index]),3),amplitude=round(float(spectrum[index]*2/window.sum()),6))


def main():
    expected=subprocess.check_output(['git','show',BASELINE+':unity/Assets/Scripts/Runtime/GameAudio.cs'],cwd=ROOT)
    if (FOLDER/'before/GameAudio.cs').read_bytes()!=expected:
        raise RuntimeError('Baseline audio source does not match its recorded revision')
    sources={}
    for line in (FOLDER/'after/sources.txt').read_text().splitlines():
        name,sha=line.rsplit(' ',1);path=ROOT/'unity/Assets/Scripts'/name
        if digest(path)!=sha:raise RuntimeError('Stale recording: '+name)
        sources[name]=sha
    if 'mute rejects' not in (FOLDER/'after/checks.txt').read_text():
        raise RuntimeError('Audio lifecycle checks missing')
    results={}
    for version in ('before','after'):
        folder=FOLDER/version;rate,pcm=wavfile.read(folder/'mix.wav')
        if not np.isfinite(pcm).all() or pcm.ndim!=2 or pcm.shape[1]!=2:
            raise RuntimeError('Invalid stereo capture')
        peak=float(np.abs(pcm).max())
        if peak>=.98 or peak<.1 or not 14.8<len(pcm)/rate<15.2:
            raise RuntimeError('Silence, clipping or incomplete capture')
        first=tone(pcm,rate,.30,.50);overlap=tone(pcm,rate,.90,1.20)
        no_voice=tone(pcm,rate,6.15,6.40);with_voice=tone(pcm,rate,6.75,7.25)
        results[version]=dict(rate=rate,seconds=len(pcm)/rate,peak=peak,
            reference=first,overlap=overlap,
            speech_gain_db=round(float(20*np.log10(with_voice['amplitude']/no_voice['amplitude'])),2),
            capture_sha256=digest(folder/'mix.wav'))
        for name,start,end in [('hits',2.1,5.7),('speech',6.0,9.3),('finisher',9.9,13.5)]:
            section=pcm[round(start*rate):round(end*rate)]
            wavfile.write(folder/(name+'.wav'),rate,np.rint(np.clip(section,-1,1)*32767).astype(np.int16))
    if abs(results['before']['overlap']['hz']-600)<10:
        raise RuntimeError('Baseline did not reproduce the shared-pitch defect')
    if abs(results['after']['overlap']['hz']-600)>.3:
        raise RuntimeError('New swing retuned the existing probe')
    if not -9<results['after']['speech_gain_db']<-5.5:
        raise RuntimeError('Speech did not lower the ongoing effects to the intended range')
    if abs(results['before']['speech_gain_db'])>1:
        raise RuntimeError('Baseline already contained speech ducking; comparison is not valid')
    proof=dict(baseline_commit=BASELINE,recordings=results,current_sources=sources,
        capture_method='OnAudioFilterRead on the Unity AudioListener during Play Mode; no microphone',
        scope='Controlled sound sequence; separate native full-round validation is required')
    native=FOLDER/'player-validation.json'
    if native.exists():
        record=json.loads(native.read_text())
        data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
        for name,path in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
            if record[name]!=digest(data/path):raise RuntimeError('Native review is from an older build')
        if record['result']!='passed' or record.get('contact_sounds')!={'fist':26,'heavy':6,'beam':2}:
            raise RuntimeError('Native contact timing did not pass')
        proof['native_player']=record
    guided=FOLDER/'guided/guided-validation.json'
    if guided.exists():
        record=json.loads(guided.read_text());build=json.loads((guided.parent/'build.json').read_text())
        data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
        for name,path in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
            if any(build[side][name]!=digest(data/path) for side in ('before','after')):
                raise RuntimeError('Guided review is from an older build')
        if record['result']!='passed' or not record['replay_battle_started'] or record['gesture_wobble']['unwanted_attacks']:
            raise RuntimeError('Guided gestures or photo replay failed')
        proof['guided_player']=record
    (FOLDER/'validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>战斗声音对照</title>
<style>body{margin:0;background:#101923;color:#eef5ff;font:17px/1.8 system-ui}main{max-width:1100px;margin:auto;padding:32px}p{color:#b9ccdc}section{background:#172634;border:1px solid #345267;border-radius:14px;padding:20px;margin:20px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:28px}audio{width:100%}h1{font-size:30px}h2{font-size:22px}a{color:#6bd4f5}@media(max-width:650px){.pair{grid-template-columns:1fr}}</style>
<main><h1>普通命中、重击、必杀：声音前后对照</h1>
<p>以下是相同控制序列经过 Unity 最终混音后的录音，没有录制麦克风。请用同一设备音量比较；普通命中加入接触脆响和短促低音，重击带更低的冲击与碎响尾音，光线碰撞使用更长的轰鸣。保留已选定的中文引导与迪迦原声战吼。</p>
<section><h2>三次普攻音效 → 一次重击音效</h2><div class="pair"><div>修改前<audio controls src="before/hits.wav"></audio></div><div>当前<audio controls src="after/hits.wav"></audio></div></div></section>
<section><h2>语音引导的清晰度</h2><p>这一组有意播放持续的测试音，便于听清语音开始时，其他声音是否立即退后。实际战斗中的碰撞、挥拳、碎石也使用同样的音量控制。</p><div class="pair"><div>修改前<audio controls src="before/speech.wav"></audio></div><div>当前<audio controls src="after/speech.wav"></audio></div></div></section>
<section><h2>蓄力 → 原声战吼 → 光线命中</h2><div class="pair"><div>修改前<audio controls src="before/finisher.wav"></audio></div><div>当前<audio controls src="after/finisher.wav"></audio></div></div></section>
<p>修复前，新挥拳让仍在播放的 600 Hz 测试音变成约 577 Hz；现在保持 600 Hz。音效播放通道有固定数量上限，普通声音不会挤掉重要必杀尾音；暂停和静音会清理正在播放的声音。</p>
<p>这是声音模块的对照，画面与连续角色动画仍需继续提升。单独录音不代表孩子在电视上的主观听感已验收。</p><p><a href="validation.json">频谱、混音与当前构建检查</a> · <a href="after/checks.txt">音效叠加、暂停与静音检查</a> · <a href="player-validation.json">实际完整战斗</a> · <a href="guided/guided-validation.json">自动合照与续局</a></p></main></html>''')
    print(json.dumps(results,ensure_ascii=False))


if __name__=='__main__':main()
