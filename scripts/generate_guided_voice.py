"""Extend the selected synthetic hero voice using the already downloaded local model."""
import hashlib
import json
import wave
from pathlib import Path

LINES = {
    'photo_intro': '来和迪迦合照吧！看着右边的自己，摆个喜欢的姿势。我会倒数五秒，自动拍照。',
    'photo_missing': '让我看看你！把头和身体移进镜头，站稳一点。',
    'photo_saved': '合照拍好啦！先放下手，慢慢欣赏。举起一只手可以重拍，双手举高就能再玩一次。',
    'photo_retry': '没关系，等画面恢复，我们再拍一次。',
    'photo_five': '五', 'photo_four': '四', 'photo_three': '三', 'photo_two': '二', 'photo_one': '一',
    'arcade_ready': '小英雄，城市需要你！站进镜头，把双手举高，和迪迦一起出发！',
    'arcade_final': '就快成功了！继续挥拳，保护城市！',
    'beam_reset': '先把双手收回来，再摆光线姿势，停一下。',
}


def main():
    import numpy as np
    import mlx.core as mx
    from mlx_audio.tts.utils import load_model
    root = Path(__file__).resolve().parents[1]
    pack = root / 'voice-packs/anime-hero'
    manifest = json.loads((pack / 'manifest.json').read_text())
    model = load_model(str(root / '.cache/voice-model-local'))
    for index, (key, text) in enumerate(LINES.items()):
        target = pack / (key+'.wav')
        if key in manifest['audio'] and target.exists():
            continue
        mx.random.seed(140+index)
        print('generating', key, text, flush=True)
        clips = list(model.generate(text=text, ref_audio=str(pack/'reference.wav'),
            ref_text=manifest['reference']['text'], lang_code='Chinese', temperature=.7,
            max_tokens=350, verbose=False))
        data = np.concatenate([np.array(clip.audio) for clip in clips])
        sr = model.sample_rate
        chunk = sr//100
        levels = np.array([np.sqrt(np.mean(data[i:i+chunk]**2)) for i in range(0,len(data),chunk)])
        active = np.flatnonzero(levels > max(.002, levels.max()*.025))
        if not len(active): raise RuntimeError('Empty speech '+key)
        data = data[max(0,int(active[0]*chunk-.025*sr)):min(len(data),int((active[-1]+1)*chunk+.08*sr))]
        data *= min(10**(-19/20)/np.sqrt(np.mean(data*data)), .82/np.max(np.abs(data)))
        data[:int(sr*.005)] *= np.linspace(0,1,int(sr*.005))
        data[-int(sr*.015):] *= np.linspace(1,0,int(sr*.015))
        with wave.open(str(target),'wb') as out:
            out.setnchannels(1);out.setsampwidth(2);out.setframerate(sr)
            out.writeframes(np.round(data*32767).astype('<i2').tobytes())
        duration = len(data)/sr
        if key.startswith('photo_') and key.split('_')[1] in ['five','four','three','two','one'] and duration>.9:
            raise RuntimeError('Countdown clip must fit one second: '+key)
        manifest['lines'][key] = text
        manifest['audio'][key] = {'file':target.name,'seconds':round(duration,5),'sample_rate':sr,
            'peak':round(float(np.max(np.abs(data))),5),'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'tempo':1.0}
        manifest['guided_extension'] = {'date':'2026-09-10','seed':'140 + line index','temperature':.7,'model':'same local model and synthetic reference'}
        (pack/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
        print('saved',key,round(duration,2),'seconds',flush=True)
        mx.clear_cache()

if __name__ == '__main__': main()
