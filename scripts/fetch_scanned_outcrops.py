"""Fetch unchanged CC0 source geometry and maps from Poly Haven's official API."""
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'unity/Assets/Resources/Environment/ScannedRocks'
AUTHORS = {'rock_07': 'Jenelle van Heerden', 'rock_09': 'Jenelle van Heerden'}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = dict(license='CC0-1.0', license_url='https://polyhaven.com/license', assets=[], files=[])
    for asset, author in AUTHORS.items():
        metadata = ROOT / '.cache/scanned-rocks' / (asset + '-files.json')
        if not metadata.exists():
            metadata.parent.mkdir(parents=True, exist_ok=True)
            subprocess.run(['curl', '-sS', '--fail', '--location', '--retry', '2', '--max-time', '40',
                            'https://api.polyhaven.com/files/' + asset, '-o', str(metadata)], check=True)
        data = json.loads(metadata.read_text())
        model = data['fbx']['2k']['fbx']
        files = [model] + [v for k, v in data['gltf']['2k']['gltf']['include'].items() if k.endswith('.jpg')]
        manifest['assets'].append(dict(id=asset, author=author, url='https://polyhaven.com/a/' + asset))
        for info in files:
            url = info['url']
            if urlparse(url).hostname != 'dl.polyhaven.org':
                raise ValueError('Unexpected download host')
            path = OUT / Path(urlparse(url).path).name
            if not path.exists() or hashlib.md5(path.read_bytes()).hexdigest() != info['md5']:
                temporary = path.with_suffix(path.suffix + '.download')
                subprocess.run(['curl', '-sS', '--http1.1', '--fail', '--location', '--retry', '2', '--retry-all-errors', '--max-time', '90',
                                url, '-o', str(temporary)], check=True)
                if temporary.stat().st_size != info['size'] or hashlib.md5(temporary.read_bytes()).hexdigest() != info['md5']:
                    raise ValueError('Downloaded file differs from source metadata: ' + path.name)
                temporary.replace(path)
            manifest['files'].append(dict(file=path.name, url=url, bytes=path.stat().st_size,
                                          sha256=hashlib.sha256(path.read_bytes()).hexdigest(), source_md5=info['md5']))
            print(path.name, path.stat().st_size, flush=True)
    (OUT / 'sources.json').write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    main()
