"""Extract RenderWare model data from a GTAall MMRC archive without running its installer."""
import argparse
from pathlib import Path
import struct
import zipfile


def extract(archive: Path, name: str, destination: Path):
    if name not in ('Geed', 'Grigio'):
        raise ValueError('Unsupported roster character')
    with zipfile.ZipFile(archive) as z:
        entries=[e for e in z.infolist() if e.filename.lower().endswith('.mmrc')]
        if len(entries)!=1 or entries[0].file_size>64*1024*1024:
            raise ValueError('Expected one bounded MMRC payload')
        payload=z.read(entries[0])
    if not payload.startswith(b'INS_BASE'):
        raise ValueError('Unrecognized MMRC header')
    kind,size,version=struct.unpack_from('<III',payload,8)
    end=20+size
    if kind!=0x10 or end>len(payload):
        raise ValueError('Invalid RenderWare clump')
    # The skin packages contain a short filename record between clump and TXD.
    marker=struct.pack('<I',0x16)
    offset=payload.find(marker,end,min(end+4096,len(payload)))
    if offset<0 or offset+12>len(payload):
        raise ValueError('Missing texture dictionary')
    _,texture_size,texture_version=struct.unpack_from('<III',payload,offset)
    texture_end=offset+12+texture_size
    if texture_end>len(payload) or texture_version!=version:
        raise ValueError('Invalid texture dictionary')
    destination.mkdir(parents=True,exist_ok=True)
    (destination/(name+'.dff')).write_bytes(payload[8:end])
    (destination/(name+'.txd')).write_bytes(payload[offset:texture_end])
    print(f'{name}: extracted model and textures; installer was not executed')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('archive',type=Path);p.add_argument('name');p.add_argument('destination',type=Path)
    a=p.parse_args();extract(a.archive,a.name,a.destination)
