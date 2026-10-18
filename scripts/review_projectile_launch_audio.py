"""Compare actual Unity projectile mixes and source assets; no microphone input."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import os

import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--folder', type=Path, required=True)
    parser.add_argument('--baseline', required=True)
    args = parser.parse_args()
    folder = args.folder.resolve()
    for version in ('before', 'after'):
        for line in (folder / version / 'sources.txt').read_text().splitlines():
            name, digest = line.rsplit(' ', 1)
            path = 'unity/Assets/Scripts/' + name
            data = (ROOT / path).read_bytes() if version == 'after' else subprocess.check_output(['git', 'show', args.baseline + ':' + path], cwd=ROOT)
            assert hashlib.sha256(data).hexdigest() == digest, (version, name)
    events = (folder / 'before/events.csv').read_bytes()
    assert events == (folder / 'after/events.csv').read_bytes(), 'Launch/contact event frames changed'
    assert events.count(b'projectile-launch') == events.count(b'projectile-contact') == 6
    assert (folder / 'before/legacy-launch.wav').read_bytes() == (folder / 'after/legacy-launch.wav').read_bytes()
    report = dict(baseline=args.baseline, capture='Unity AudioListener stereo mix', event_frames='identical', clips={}, mixes={})
    for version in ('before', 'after'):
        paths = sorted((folder / version).glob('launch-*.wav'))
        assert len(paths) == (1 if version == 'before' else 3)
        metrics = []
        for path in paths:
            rate, data = wavfile.read(path)
            data = data.astype(np.float64)
            assert data.ndim == 1 and np.isfinite(data).all() and .01 < abs(data).max() < .98
            assert abs(data[0]) < .001 and abs(data[-1]) < .001
            window = round(rate * .010)
            power = np.convolve(data * data, np.ones(window) / window, 'valid')
            cumulative = np.cumsum(data * data)
            metrics.append(dict(name=path.name, rate=rate, seconds=len(data)/rate, peak=float(abs(data).max()),
                rms=float(np.sqrt(np.mean(data*data))), crest_seconds=(int(power.argmax()) + window/2)/rate,
                energy_half_seconds=float(np.searchsorted(cumulative, cumulative[-1]*.5)/rate),
                first_20ms_energy_share=float(cumulative[round(rate*.020)]/cumulative[-1])))
        report['clips'][version] = metrics
        rate, mix = wavfile.read(folder / version / 'mix.wav')
        assert mix.ndim == 2 and mix.shape[1] == 2 and np.isfinite(mix).all()
        assert 9.7 < len(mix)/rate < 10.4 and .05 < abs(mix).max() < .98
        report['mixes'][version] = dict(rate=rate, seconds=len(mix)/rate, peak=float(abs(mix).max()))
        wavfile.write(folder / version / 'audition.wav', rate, np.rint(np.clip(mix, -1, 1)*32767).astype(np.int16))
    assert len({hashlib.sha256(p.read_bytes()).hexdigest() for p in (folder / 'after').glob('launch-*.wav')}) == 3
    old = report['clips']['before'][0]
    for new in report['clips']['after']:
        assert new['crest_seconds'] < .025 and new['crest_seconds'] < old['crest_seconds']
        assert new['first_20ms_energy_share'] > old['first_20ms_energy_share']
    checks = (folder / 'after/checks.txt').read_text()
    for expected in ('three cached launch variants', 'other hero launch preserved', 'mute rejects new transients', 'fixed pool under 40 hits'):
        assert expected in checks, expected
    report['limitations'] = 'Isolated effects and guidance, music disabled for measurement. Faster onset and three variants are measured; realism and TV listening quality require human audition.'
    (folder / 'comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    os.environ.setdefault('MPLCONFIGDIR', str(ROOT / '.cache/matplotlib'))
    import matplotlib
    matplotlib.use('Agg')
    from matplotlib import pyplot as plt
    fig, axes = plt.subplots(2, 1, figsize=(11, 6), constrained_layout=True)
    for version, color in (('before', '#d78037'), ('after', '#008ba3')):
        rate, samples = wavfile.read(folder / version / 'launch-0.wav')
        window = round(rate*.005)
        envelope = np.sqrt(np.convolve(samples*samples, np.ones(window)/window, 'valid'))
        axes[0].plot((np.arange(len(envelope))+window/2)*1000/rate, envelope, color=color, label=version)
        rate, mix = wavfile.read(folder / version / 'mix.wav')
        window = round(rate*.010)
        mono_power = np.mean(mix*mix, axis=1)
        envelope = np.sqrt(np.convolve(mono_power, np.ones(window)/window, 'valid'))
        axes[1].plot((np.arange(len(envelope))+window/2)/rate, envelope, color=color, label=version, alpha=.8)
    axes[0].set(title='Launch source: 5 ms RMS envelope (variant 0)', xlabel='Time from source start (ms)', ylabel='Amplitude', xlim=(0, 280))
    axes[1].set(title='Unity output: slow cast, normal cast, rapid casts, voice cue, contact reference', xlabel='Recording time (seconds)', ylabel='10 ms RMS', xlim=(0, 10))
    for ax in axes:
        ax.legend();ax.grid(alpha=.2)
    fig.savefig(folder / 'waveform-comparison.png', dpi=160);plt.close(fig)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
