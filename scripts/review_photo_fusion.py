"""Verify local photo fusion against real Unity layers and save a visual review.

Only --request-model sends one clearly synthetic photo to the approved gateway.
No real camera frame, family picture, credentials or raw response is logged.
"""
import argparse
import hashlib
import importlib.util
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

import cv2
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from vision.photo_enhance import harmonise,enhance,MODEL

FOLDER=ROOT/'artifacts/photo-linear-fusion'
BASELINE='f7b26485b52461397b55e65c42d5f49d1bf32fc6'
RECIPE=dict(exposure_ev=-.45,red_gain=1,green_gain=1,blue_gain=1,
            saturation=1,edge_feather_px=.6,light_wrap=0,shadow_strength=.24)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def linear(value):
    x=value.astype(np.float64)/255
    return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)


def encoded(value):
    x=np.clip(value,0,1)
    return np.uint8(np.rint(np.where(x<=.0031308,x*12.92,1.055*x**(1/2.4)-.055)*255))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--request-model',action='store_true');args=parser.parse_args()
    cv2.setNumThreads(1);FOLDER.mkdir(parents=True,exist_ok=True)
    baseline_path=FOLDER/'baseline_photo_enhance.py'
    baseline_path.write_bytes(subprocess.check_output(['git','show',BASELINE+':vision/photo_enhance.py'],cwd=ROOT))
    spec=importlib.util.spec_from_file_location('photo_fusion_baseline',baseline_path)
    old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
    sources={}
    for line in (FOLDER/'render/sources.txt').read_text().splitlines():
        path,expected=line.rsplit(' ',1);file=ROOT/'unity/Assets'/path
        if digest(file)!=expected:raise RuntimeError('Stale Unity layers: '+path)
        sources[str(file.relative_to(ROOT))]=expected
    cases=[]
    for id in ('Tiga','Grigio'):
        for width in (1920,2560):
            for kind in ('full','half','translucent'):
                stem=f'{id}-{width}-{kind}';path=FOLDER/'render'/stem
                image=cv2.imread(str(path)+'.png');plate=cv2.imread(str(path)+'-plate.png');mask=cv2.imread(str(path)+'-mask.png',0)
                if any(x is None for x in (image,plate,mask)):raise RuntimeError('Missing Unity layers: '+stem)
                before=old.harmonise(image,plate,mask,RECIPE);after=harmonise(image,plate,mask,RECIPE)
                cv2.imwrite(str(FOLDER/(stem+'-before.png')),before);cv2.imwrite(str(FOLDER/(stem+'-after.png')),after)
                if not np.array_equal(after[:,:width//2],image[:,:width//2]):raise RuntimeError('Hero or left plate changed')
                case=dict(id=stem,left_hero_unchanged=True)
                if kind=='half' and not np.array_equal(image[mask==0],after[mask==0]):
                    raise RuntimeError('Cropped portrait received a floor shadow')
                if kind=='translucent':
                    # Source is a uniform RGB (193,117,52) at known alpha.
                    foreground=np.full_like(image,(52,117,193));a=mask[:,:,None]/255
                    expected=encoded(linear(foreground)*2**RECIPE['exposure_ev']*a+linear(plate)*(1-a))
                    errors={}
                    for alpha in (64,128,192,255):
                        safe=cv2.erode(np.uint8(mask==alpha),np.ones((25,25),np.uint8))>0
                        if np.count_nonzero(safe)<1000:raise RuntimeError('Missing GPU alpha band')
                        error=np.abs(after.astype(np.int16)-expected.astype(np.int16))[safe]
                        prior=np.abs(before.astype(np.int16)-expected.astype(np.int16))[safe]
                        if np.percentile(error,99)>2:raise RuntimeError('Linear fusion differs from physical exposure')
                        errors[str(alpha)]=dict(before_p99=float(np.percentile(prior,99)),after_p99=float(np.percentile(error,99)))
                    case['exposure_error']=errors
                else:
                    rows,cols=np.where(mask>128);bottom=int(rows.max())
                    case['visible_body_bottom']=bottom
                    case['shadow_pixels']=int(np.count_nonzero((mask==0)&np.any(after!=image,axis=2)))
                    if kind=='full' and case['shadow_pixels']==0:raise RuntimeError('Missing full-body contact shadow')
                cases.append(case)
    model_folder=FOLDER/'model';model_folder.mkdir(exist_ok=True)
    model_status=model_folder/'validation.json'
    if args.request_model:
        paths={name:FOLDER/'render'/('Tiga-1920-full'+suffix) for name,suffix in [('input','.png'),('plate','-plate.png'),('mask','-mask.png')]}
        source=model_folder/'synthetic-photo.png';shutil.copyfile(paths['input'],source)
        started=time.monotonic()
        try:
            output,recipe=enhance(source,paths['plate'],paths['mask'])
            original=cv2.imread(str(source));result=cv2.imread(str(output))
            if not np.array_equal(original[:,:960],result[:,:960]):raise RuntimeError('Model-directed job changed fixed hero')
            result=dict(result='passed',model=MODEL,seconds=round(time.monotonic()-started,2),synthetic_only=True,
                        original_sha256=digest(source),output_sha256=digest(output),recipe=recipe,
                        inputs={key:digest(path) for key,path in paths.items()},implementation=digest(ROOT/'vision/photo_enhance.py'))
        except Exception as error:
            # Never store provider exception bodies, URLs, headers or credentials.
            result=dict(result='unavailable',error_type=type(error).__name__,synthetic_only=True,model=MODEL)
        model_status.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    proof=dict(baseline_commit=BASELINE,baseline_python_sha256=digest(baseline_path),
               current_python_sha256=digest(ROOT/'vision/photo_enhance.py'),render_sources=sources,
               recipe=RECIPE,cases=cases,kind='Actual Unity GPU layers with synthetic subjects; no family photos')
    if model_status.exists():
        proof['model_job']=json.loads(model_status.read_text())
        if proof['model_job'].get('implementation') and proof['model_job']['implementation']!=proof['current_python_sha256']:
            raise RuntimeError('Model job predates current photo processing code')
    worker_status=FOLDER/'worker/validation.json'
    if worker_status.exists():
        worker=json.loads(worker_status.read_text())
        for key,path in [('worker_sha256','unity/Assets/Scripts/Runtime/LocalPhotoEnhancement.cs'),
                         ('python_sha256','vision/photo_enhance.py'),('entrypoint_sha256','scripts/enhance_photo.py')]:
            if worker[key]!=digest(ROOT/path):raise RuntimeError('Stale Unity AI worker proof')
        if worker['result']!='passed':raise RuntimeError('Unity AI worker did not complete')
        proof['unity_worker']=worker
    guided_status=FOLDER/'guided/guided-validation.json'
    if guided_status.exists():
        guided=json.loads(guided_status.read_text());build=json.loads((FOLDER/'guided/build.json').read_text())
        for key,path in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
            expected=digest(ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'/path)
            if build['before'][key]!=expected or build['after'][key]!=expected:raise RuntimeError('Stale guided player proof')
        if guided['result']!='passed' or guided['gesture_wobble']['unwanted_attacks'] or not guided['replay_battle_started']:
            raise RuntimeError('Guided photo or gesture regression')
        proof.update(guided=guided,build=build)
    (FOLDER/'validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>合照光色与边缘校正</title>
<style>body{margin:0;background:#101721;color:#edf5ff;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#b5cadb;max-width:1100px}select,button{font:inherit;padding:7px 16px;margin:4px;color:inherit;background:#20384a;border:1px solid #4a677e;border-radius:6px}img{width:100%}a{color:#75d8ed}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}</style>
<main><h1>合照：按实际光照处理人物边缘</h1><p>Unity 在线性光照下合成透明人物。旧后处理直接反算 PNG 亮度，导致半透明轮廓的颜色失真，同样的曝光参数也会过度压暗人物。现在曝光和边缘合成都与游戏一致，并分别在可见双脚下生成接触阴影；半身照片不加假落地阴影。奥特曼及左侧背景逐像素保持不变。</p>
<p>以下全部是合成测试图形，使用实际 Unity 相机输出的照片、底图和蒙版，目的是比较同一组参数下的算法差异。它们不能证明真人发丝抠像或生成式融合已经达到目标。</p>
<select id="hero"><option value="Tiga">迪迦</option><option value="Grigio">格力乔</option></select><select id="size"><option>1920</option><option>2560</option></select><select id="kind"><option value="full">全身与双脚阴影</option><option value="half">半身前景肖像</option><option value="translucent">半透明颜色校准</option></select>
<div class="pair"><div>修改前<img id="before"></div><div>当前<img id="after"></div></div>
<h2>同位置切换</h2><button id="original">Unity 原图</button><button id="old">旧后处理</button><button id="new">当前后处理</button><img id="large">
<h2>gpt-5.6-terra 实际调用</h2><p>独立验证使用一张全身 TEST 图和对应底图、蒙版；仍由模型分析场景后给出调色参数，本次没有接入生成式图像编辑。具体调用结果和参数见验证记录。</p><div class="pair"><img src="model/synthetic-photo.png" alt="合成测试原图"><img src="model/synthetic-photo_AI.png" alt="模型指导的光色处理结果"></div>
<p><a href="validation.json">像素检查与源码证据</a> · <a href="model/validation.json">实际模型调用结果</a> · <a href="worker/validation.json">Unity 后台处理</a> · <a href="guided/guided-validation.json">无键鼠合照续局</a></p>
<script>const el=id=>document.getElementById(id);function stem(){return el('hero').value+'-'+el('size').value+'-'+el('kind').value}function show(){el('before').src=stem()+'-before.png';el('after').src=stem()+'-after.png';el('large').src=el('after').src}['hero','size','kind'].forEach(id=>el(id).onchange=show);el('original').onclick=()=>el('large').src='render/'+stem()+'.png';el('old').onclick=()=>el('large').src=stem()+'-before.png';el('new').onclick=()=>el('large').src=stem()+'-after.png';show()</script></main></html>''')
    print(json.dumps(dict(cases=len(cases),model=proof.get('model_job',{}).get('result','not-requested'),review=str(FOLDER/'index.html')),ensure_ascii=False))


if __name__=='__main__':main()
