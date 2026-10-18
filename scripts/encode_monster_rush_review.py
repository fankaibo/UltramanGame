"""Encode the two actual Unity rush sequences after verifying equal gameplay."""
from pathlib import Path
import csv,hashlib,json,subprocess
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'artifacts/monster-rush-step-20261010'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    out=BASE/'comparison';out.mkdir(exist_ok=True);frames=0;states=0
    font=ImageFont.truetype(str(ROOT/'unity/Assets/Resources/Fonts/NotoSansSC-Regular.otf'),28)
    small=ImageFont.truetype(str(ROOT/'unity/Assets/Resources/Fonts/NotoSansSC-Regular.otf'),18)
    hashes={}
    for attack in [1,5]:
        folders=[BASE/version/f'{attack}-60' for version in ['before','after']]
        traces=[p/'states.csv' for p in folders];assert traces[0].read_bytes()==traces[1].read_bytes()
        states+=len(list(csv.DictReader(traces[0].open())))
        images=[sorted((p/'frames').glob('*.png')) for p in folders];assert len(images[0])==len(images[1])==90
        for p in traces:hashes[str(p.relative_to(ROOT))]=sha(p)
        for old,new in zip(*images):
            canvas=Image.new('RGB',(1920,620),'#08111f');draw=ImageDraw.Draw(canvas)
            for i,p in enumerate([old,new]):
                with Image.open(p) as im:canvas.paste(im.resize((960,540)),(i*960,55))
            draw.text((25,10),'此前：远距接近时支撑脚贴地滑移',font=font,fill='#bfd0e4')
            draw.text((985,10),'现在：抬脚迈进 · 落地挥爪 · 分步退回',font=font,fill='#62dcec')
            draw.text((25,598),('右爪' if attack==1 else '左爪')+'前冲；相同输入与命中时刻。Unity 离线 30 FPS 序列，不代表实机帧率。',font=small,fill='#bfd0e4')
            canvas.save(out/f'{frames:04d}.png');frames+=1
    video=BASE/'comparison.mp4'
    subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-framerate','30','-i',str(out/'%04d.png'),'-c:v','libx264','-crf','20','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(video)],check=True)
    sources=['unity/Assets/Scripts/Core/MonsterStepMotion.cs','unity/Assets/Scripts/Runtime/RiggedActor.cs','unity/Assets/Scripts/Runtime/CombatVfx.cs','unity/Assets/Scripts/Runtime/GameAudio.cs','unity/Assets/Scripts/Runtime/GameWorld.cs','unity/Assets/Scripts/Runtime/ArenaController.cs']
    before={}
    for name in sources:
        before[name]=hashlib.sha256(subprocess.check_output(['git','show','4173b1d:'+name])).hexdigest();hashes[name]=sha(ROOT/name)
    hashes[str(video.relative_to(ROOT))]=sha(video)
    metrics={str(p.relative_to(BASE)):p.read_text() for version in ['before','after'] for p in (BASE/version).glob('*/metrics.txt')}
    (BASE/'identity.json').write_text(json.dumps(dict(baseline='4173b1d',equal_gameplay_frames=states,frames=frames,metrics=metrics,before_sources=before,hashes=hashes,status='editor-verified-release-pending'),ensure_ascii=False,indent=2)+'\n')
    (BASE/'index.html').write_text('<!doctype html><html lang="zh"><meta charset="utf-8"><title>怪兽远距前冲步序</title><style>body{background:#08111f;color:#dce6f0;font:18px system-ui;max-width:1280px;margin:40px auto;padding:24px}video{width:100%}</style><h1>怪兽远距前冲：从贴地滑移到分步移动</h1><p>两侧相同输入、机位和命中时钟，先右爪，再左爪；左旧右新。</p><video src="comparison.mp4" controls loop muted></video><p>360 条玩法状态逐字节一致；离线编码帧率不代表实际游戏帧率。</p></html>')
    print(f'{video} — {states} matching gameplay frames')

if __name__=='__main__':main()
