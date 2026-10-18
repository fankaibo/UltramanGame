"""Verify real Unity finisher mixes and export an old/new listening comparison."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '7f3f1771ce16106cc51660a2168acfa05baa5578'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder', type=Path, default=ROOT/'artifacts/a165-beam-audio')
    folder = parser.parse_args().folder.resolve()
    before_events = (folder/'before/events.csv').read_bytes()
    if before_events != (folder/'after/events.csv').read_bytes():
        raise RuntimeError('Cry, release or contact clock changed')
    if b'beam-cry' not in before_events or b'beam-launch' not in before_events or b'beam-contact' not in before_events:
        raise RuntimeError('Incomplete finisher recording')
    required = ('pause clears', 'speech-free gain .65', 'speaking gain .4', 'other hero sample preserved')
    if not all(x in (folder/'after/checks.txt').read_text() for x in required):
        raise RuntimeError('Missing audio lifecycle evidence')
    result = {'baseline': BASELINE, 'capture': 'Unity AudioListener output; no microphone; music disabled to isolate combat', 'events': 'identical', 'versions': {}}
    mixes = []
    sample_rate = None
    for version in ('before', 'after'):
        for line in (folder/version/'sources.txt').read_text().splitlines():
            name, digest = line.rsplit(' ', 1)
            name = 'unity/Assets/Scripts/'+name
            content = (ROOT/name).read_bytes() if version == 'after' else subprocess.check_output(['git', 'show', BASELINE+':'+name], cwd=ROOT)
            if hashlib.sha256(content).hexdigest() != digest:
                raise RuntimeError('Stale recording source: '+version+' '+name)
        rate, mix = wavfile.read(folder/version/'mix.wav')
        if not np.isfinite(mix).all() or mix.ndim != 2 or mix.shape[1] != 2:
            raise RuntimeError('Invalid stereo mix')
        peak = float(np.abs(mix).max())
        if not .05 < peak < .98 or not 4.1 < len(mix)/rate < 4.4:
            raise RuntimeError('Clipping, silence or incomplete mix')
        if sample_rate is not None and rate != sample_rate:
            raise RuntimeError('Comparison sample rates differ')
        sample_rate = rate
        mixes.append(mix)
        sr, clip = wavfile.read(folder/version/'beam-release.wav')
        if not np.isfinite(clip).all() or max(abs(clip[0]), abs(clip[-1])) > .001:
            raise RuntimeError('Invalid discharge endpoint')
        energy = np.convolve(clip.astype(np.float64)**2, np.ones(round(sr*.025))/round(sr*.025), 'valid')
        result['versions'][version] = {'mix_peak': peak, 'mix_seconds': len(mix)/rate,
            'release_rms_peak_seconds': float(np.argmax(energy)/sr),
            'first_100ms_rms': float(np.sqrt(np.mean(clip[:round(sr*.1)]**2))),
            'release_peak': float(np.abs(clip).max())}
    if result['versions']['before']['release_rms_peak_seconds'] < .5 or result['versions']['after']['release_rms_peak_seconds'] > .05:
        raise RuntimeError('Original late swell or new immediate pressure onset not reproduced')
    gap = np.zeros((round(sample_rate*.7), 2), dtype=np.float32)
    joined = np.concatenate((mixes[0], gap, mixes[1]))
    wavfile.write(folder/'old-then-new.wav', sample_rate, np.rint(np.clip(joined, -1, 1)*32767).astype(np.int16))
    (folder/'audio-validation.json').write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
