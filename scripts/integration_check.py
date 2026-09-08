"""Exercise the real TCP bridge and C# receiver with synthetic poses, without a camera."""
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from vision.bridge import LatestBridge
from vision.preview import encode_preview


def main():
    process=subprocess.Popen([sys.executable,'-m','vision','--demo','--port','0','--seconds','30'],cwd=ROOT,
                             stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    try:
        line=process.stdout.readline()
        if not line:
            raise RuntimeError(process.stderr.read())
        print(line.strip(),flush=True)
        port=int(line.strip().rsplit(':',1)[1])
        with LatestBridge(0) as preview:
            # Minimal JPEG header fixture; actual image decoding is checked in the Unity player.
            jpeg=bytes([255,216,255,192,0,11,8,0,240,1,64,1,1,17,0,255,217])
            preview.publish(encode_preview(jpeg,123456789,'synthetic',1))
            subprocess.run(['dotnet','run','--project','tests/CoreChecks','--configuration','Release',
                            '--no-build','--','--bridge',str(port),str(preview.address[1])],cwd=ROOT,check=True,timeout=20)
    finally:
        process.terminate()
        try:process.wait(timeout=3)
        except subprocess.TimeoutExpired:process.kill();process.wait()


if __name__=='__main__':main()
