"""Check actual Unity stereo captures; no microphone or external audio input."""
import hashlib
import json
from pathlib import Path
import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/combat-stereo-20261012'


def main():
    if (FOLDER/'before/events.csv').read_bytes() != (FOLDER/'after/events.csv').read_bytes():
        raise RuntimeError('Event schedule changed')
    result = {'capture': 'Unity AudioListener, music disabled, no microphone',
              'baseline_control': 'same clips and clocks, SetStage omitted so pan stays centered',
              'events_equal': True, 'mixes': {}}
    windows = [('launch-left', .4, 1.1, 'left'), ('contact-right', 1.4, 2.2, 'right'),
               ('launch-right', 2.7, 3.5, 'right'), ('contact-left', 3.7, 4.5, 'left'),
               ('center-probe', 5.3, 6.5, 'center')]
    for version in ('before', 'after'):
        for line in (FOLDER/version/'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            if hashlib.sha256((ROOT/'unity/Assets/Scripts'/name).read_bytes()).hexdigest() != expected:
                raise RuntimeError('Capture source no longer matches '+name)
        rate, pcm = wavfile.read(FOLDER/version/'mix.wav')
        if pcm.ndim != 2 or pcm.shape[1] != 2 or not np.isfinite(pcm).all():
            raise RuntimeError('Invalid stereo mix')
        peak = float(np.abs(pcm).max())
        if not .02 < peak < .98 or not 7.9 <= len(pcm)/rate <= 8.4:
            raise RuntimeError('Clipping, silence or incomplete capture')
        rows = {}
        for name, start, end, side in windows:
            data = pcm[round(start*rate):round(end*rate)].astype(np.float64)
            energy = (data*data).sum(axis=0)
            if min(energy) < 1e-6:
                raise RuntimeError('Silent event '+name)
            ratio = float(energy[0]/energy[1])
            if version == 'before' or side == 'center':
                if abs(ratio-1) > .01:
                    raise RuntimeError('Centered event changed: '+name)
            elif side == 'left' and ratio < 1.15 or side == 'right' and ratio > 1/1.15:
                raise RuntimeError('Incorrect or inaudible direction: '+name)
            rows[name] = {'left_right_energy_ratio': ratio, 'energy': energy.tolist(),
                          'mono_rms': float(np.sqrt(np.mean(np.mean(data, axis=1)**2)))}
        result['mixes'][version] = {'peak': peak, 'seconds': len(pcm)/rate, 'events': rows}
        wavfile.write(FOLDER/version/'preview.wav', rate, np.rint(np.clip(pcm[:round(4.7*rate)],-1,1)*32767).astype(np.int16))
    for name, *_ in windows:
        mono = result['mixes']['after']['events'][name]['mono_rms']/result['mixes']['before']['events'][name]['mono_rms']
        if not .75 < mono < 1.25:
            raise RuntimeError('Mono downmix changed level materially: '+name)
    checks=(FOLDER/'after/spatial-checks.txt').read_text()
    if 'tail retained' not in checks or 'pause clear: passed' not in checks:
        raise RuntimeError('Missing source lifecycle checks')
    (FOLDER/'validation.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__ == '__main__':
    main()
