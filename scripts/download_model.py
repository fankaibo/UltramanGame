"""Fetch the official model atomically and verify the repository-pinned digest."""
import hashlib
import json
import os
from pathlib import Path
import urllib.request

ROOT=Path(__file__).resolve().parents[1]


def main():
    manifest=json.loads((ROOT/'models/manifest.json').read_text())
    expected=manifest['sha256']
    destination=ROOT/'models'/manifest['filename']
    if destination.is_file() and expected and hashlib.sha256(destination.read_bytes()).hexdigest()==expected:
        print('人体姿态模型已就绪，校验通过。')
        return
    temporary=destination.with_suffix('.download')
    destination.parent.mkdir(parents=True,exist_ok=True)
    try:
        with urllib.request.urlopen(manifest['url'],timeout=60) as response, temporary.open('wb') as output:
            total=0
            while chunk:=response.read(65536):
                total+=len(chunk)
                if total>30_000_000:raise RuntimeError('模型响应超出预期大小')
                output.write(chunk)
        digest=hashlib.sha256(temporary.read_bytes()).hexdigest()
        if not expected or digest!=expected:
            raise RuntimeError('模型校验值缺失或不匹配；请核对官方发布与 models/manifest.json。实际 SHA-256: '+digest)
        os.replace(temporary,destination)
        print('人体姿态模型下载完成，SHA-256 校验通过。')
    finally:
        temporary.unlink(missing_ok=True)


if __name__=='__main__':main()
