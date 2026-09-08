"""Exercise the real TCP bridge and C# receiver with synthetic poses, without a camera."""
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def main():
    process=subprocess.Popen([sys.executable,'-m','vision','--demo','--port','0','--seconds','30'],cwd=ROOT,
                             stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    try:
        line=process.stdout.readline()
        if not line:
            raise RuntimeError(process.stderr.read())
        print(line.strip(),flush=True)
        port=int(line.strip().rsplit(':',1)[1])
        subprocess.run(['dotnet','run','--project','tests/CoreChecks','--configuration','Release',
                        '--no-build','--','--bridge',str(port)],cwd=ROOT,check=True,timeout=20)
    finally:
        process.terminate()
        try:process.wait(timeout=3)
        except subprocess.TimeoutExpired:process.kill();process.wait()


if __name__=='__main__':main()
