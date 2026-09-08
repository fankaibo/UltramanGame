"""Deterministic original training-game score and effects. No downloaded music or recordings."""
from pathlib import Path
import wave
import numpy as np

RATE = 32000
ROOT = Path(__file__).resolve().parents[1] / 'unity/Assets/Resources/Audio'
RNG = np.random.default_rng(20260908)


def save(name, data, peak=.72):
    data = np.nan_to_num(data)
    data *= min(1, peak/max(.001, np.max(np.abs(data))))
    samples = np.round(np.clip(data, -1, 1)*32767).astype('<i2')
    ROOT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(ROOT/(name+'.wav')), 'wb') as out:
        out.setnchannels(2 if data.ndim == 2 else 1)
        out.setsampwidth(2); out.setframerate(RATE); out.writeframes(samples.tobytes())


def tone(midi, seconds, kind='pluck'):
    t = np.arange(round(seconds*RATE))/RATE
    x = t/max(seconds, .001)
    hz = 440*2**((midi-69)/12)
    phase = 2*np.pi*hz*t
    if kind == 'pad':
        body = np.sin(phase)+.25*np.sin(phase*2+.12*np.sin(t*3))+.1*np.sin(phase*3)
        envelope = np.sin(np.pi*x)**1.3
    elif kind == 'bass':
        body = np.sin(phase)+.22*np.sin(phase*2)
        envelope = (1-np.exp(-t/0.009))*np.exp(-x*3)*np.minimum(1,(1-x)*14)
    else:
        body = np.sin(phase)+.32*np.sin(2*phase)+.14*np.sin(3*phase)+.08*np.sin(4*phase)
        envelope = (1-np.exp(-t/0.006))*np.exp(-x*4)*np.minimum(1,(1-x)*20)
    return body*envelope


def drum(kind):
    t = np.arange(int(.3*RATE))/RATE
    noise = RNG.normal(0, 1, len(t))
    if kind == 'kick': return np.sin(2*np.pi*(48*t+1.8*(1-np.exp(-t*28))))*np.exp(-t*18)
    if kind == 'snare': return (noise*.23+np.sin(2*np.pi*170*t)*.3)*np.exp(-t*28)*(1-np.exp(-t*1500))
    return np.concatenate(([0], np.diff(noise)))*.10*np.exp(-t*85)


def score(name, bpm, bars, active):
    beat = 60/bpm
    count = round(beat*4*bars*RATE)
    data = np.zeros((count, 2), np.float64)
    def add(sound, at, volume, pan=0):
        index = (np.arange(len(sound))+round(at*RATE)) % count
        data[index, 0] += sound*volume*(1-pan*.45)
        data[index, 1] += sound*volume*(1+pan*.45)
    chords = [(50,54,57), (47,50,54), (43,47,50), (45,49,52)]
    melody = [74,None,78,81,78,76,74,69, 71,74,78,None,76,74,73,69,
              71,None,74,79,78,74,71,69, 73,76,81,None,79,76,73,None]
    for bar in range(bars):
        chord = chords[(bar//2)%4]
        for note in chord: add(tone(note+12, beat*4, 'pad'), bar*4*beat, .048, (note%3-1)*.5)
        for step in range(8):
            moment = (bar*4+step*.5)*beat
            add(tone(chord[step%3]+24, beat*.75), moment, .055 if active else .040, (-1)**step*.55)
            if active: add(drum('hat'), moment, .25)
        for step in range(4):
            moment = (bar*4+step)*beat
            add(tone(chord[0]-12, beat*.8, 'bass'), moment, .15)
            if active: add(drum('kick' if step%2==0 else 'snare'), moment, .22)
        if active:
            for step in range(4):
                note = melody[(bar*4+step)%len(melody)]
                if note: add(tone(note, beat*1.4), (bar*4+step)*beat, .16, .05)
    # Quiet cross-channel room reflections wrap across the loop boundary.
    data += .16*np.roll(data[:, ::-1], round(.18*RATE), axis=0)
    data += .08*np.roll(data[:, ::-1], round(.31*RATE), axis=0)
    save(name, data)


def effect(name, seconds, style):
    t = np.arange(round(seconds*RATE))/RATE
    x = t/seconds
    noise = RNG.normal(0, 1, len(t))
    if style == 'whoosh':
        data = (noise*.11+np.sin(2*np.pi*(190*t-65*t*t))*.15)*np.sin(np.pi*x)**2
    elif style == 'rush':
        data = (np.sin(2*np.pi*(95*t-35*t*t))*.28+noise*.20)*np.sin(np.pi*x)**1.3
    elif style == 'hit':
        data = (np.sin(2*np.pi*(62*t+2*(1-np.exp(-t*35))))*.55+noise*.12)*np.exp(-t*18)*(1-np.exp(-t*1700))
    elif style == 'beam':
        data = (np.sin(2*np.pi*(160*t+70*t*t))*.28+noise*.09)*np.sin(np.pi*x)**.7
        data += .18*np.sin(2*np.pi*660*t)*np.sin(np.pi*x)**2
    elif style == 'shield':
        data = sum(tone(n, seconds)*v for n,v in [(81,.28),(86,.19),(90,.13)])
    elif style == 'rise':
        data = np.sin(2*np.pi*(220*t+180*t*t))*.12*np.sin(np.pi*x)**2
        for i,n in enumerate((62,66,69,74,78,81)):
            part=tone(n, .6)*.17; start=round(i*.16*RATE)
            data[start:min(len(data),start+len(part))] += part[:max(0,min(len(part),len(data)-start))]
    else:
        data = tone(57, seconds, 'bass')*.3
    save(name, data)


if __name__ == '__main__':
    score('music_ready', 90, 8, False)
    score('music_battle', 108, 16, True)
    for args in [('swing',.24,'whoosh'),('impact',.32,'hit'),('beam',1.5,'beam'),
                 ('shield',.75,'shield'),('transform',1.65,'rise'),('recover',.45,'soft'),('enemy_rush',.45,'rush')]:
        effect(*args)
    win=np.zeros((RATE*4,2))
    for i,n in enumerate((62,66,69,74,78,81,86)):
        part=tone(n,1.6)*.25;start=int(i*.27*RATE)
        win[start:start+len(part),:] += part[:,None]
    save('victory',win)
    print('Generated 2 original music loops and 8 effects in '+str(ROOT))
