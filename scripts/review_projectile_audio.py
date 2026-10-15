"""Validate the bounded Tiga projectile audio review and export short A/B clips."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '86a0915a07baf2bc160ef059aeae9c5a00944a22'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder', type=Path, default=ROOT/'artifacts/projectile-audio-20261011')
    folder = parser.parse_args().folder.resolve()
    for version in ('before', 'after'):
        for line in (folder/version/'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            path = 'unity/Assets/Scripts/' + name
            data = (ROOT/path).read_bytes() if version == 'after' else subprocess.check_output(['git', 'show', BASELINE+':'+path], cwd=ROOT)
            if sha(data) != expected:
                raise RuntimeError('Recording source does not match '+version+': '+path)
    if (folder/'before/events.csv').read_bytes() != (folder/'after/events.csv').read_bytes():
        raise RuntimeError('Changed attack or contact timing')
    checks = (folder/'after/checks.txt').read_text()
    if any(text not in checks for text in ('mute rejects', '40 hits', 'other hero contact preserved')):
        raise RuntimeError('Missing lifecycle checks')
    result = dict(baseline=BASELINE, event_timing='identical', capture='Unity AudioListener final mix, no microphone', mixes={}, contacts={})
    for version in ('before', 'after'):
        rate, pcm = wavfile.read(folder/version/'mix.wav')
        if pcm.ndim != 2 or pcm.shape[1] != 2 or not np.isfinite(pcm).all():
            raise RuntimeError('Invalid stereo mix')
        peak = float(np.abs(pcm).max())
        if not .05 < peak < .98 or not 9.8 < len(pcm)/rate < 10.3:
            raise RuntimeError('Clipping, silence or incomplete review')
        result['mixes'][version] = dict(seconds=len(pcm)/rate, peak=peak, sha256=sha((folder/version/'mix.wav').read_bytes()))
        # Both captures have the same scheduled input; callback start offsets
        # can vary. These broad sections include the complete three-hit burst.
        clip = pcm[round(3.2*rate):round(5.7*rate)]
        wavfile.write(folder/version/'fast-burst.wav', rate, np.rint(np.clip(clip,-1,1)*32767).astype(np.int16))
    for name, pitch in [('legacy-contact',1.22), *[(f'projectile-contact-{i}',1) for i in range(3)]]:
        rate, data = wavfile.read(folder/'after'/(name+'.wav'))
        data = data.astype(np.float64)
        energy = np.cumsum(data*data)
        if not np.isfinite(data).all() or not .1 < np.abs(data).max() < .98 or energy[-1] <= 0:
            raise RuntimeError('Invalid isolated contact '+name)
        if max(abs(data[0]),abs(data[-1])) > .001:
            raise RuntimeError('Abrupt contact endpoint')
        t95 = int(np.searchsorted(energy,energy[-1]*.95))/(rate*pitch)
        result['contacts'][name] = dict(playback_pitch=pitch, playback_seconds=len(data)/(rate*pitch), energy95_seconds=t95,
            energy_after_200ms=float(1-energy[min(len(data)-1,round(.2*rate*pitch))]/energy[-1]), sha256=sha((folder/'after'/(name+'.wav')).read_bytes()))
    old = result['contacts']['legacy-contact']
    for i in range(3):
        new = result['contacts'][f'projectile-contact-{i}']
        if new['energy95_seconds'] >= old['energy95_seconds'] or new['energy_after_200ms'] >= old['energy_after_200ms']:
            raise RuntimeError('New ordinary contacts did not shorten the tail')
    (folder/'comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(result,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
