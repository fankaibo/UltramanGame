"""GPT-directed, bounded photo harmonisation. Never executes model-authored code.

The model sees the finished composition, reads the scene lighting and chooses
local colour/edge adjustments. Geometry, face details and the hero's size are fixed.
"""
import base64
import json
import os
import re
import urllib.request
from pathlib import Path

MODEL = 'gpt-5.6-terra'


def configuration(home=None):
    home = Path(home or Path.home())
    candidates = [home/'Downloads'/n for n in ('hlclaw', 'hlclaw.json', 'hlclaude 2')]
    url = None
    for path in candidates:
        if path.is_file():
            match = re.search(r'^HUALAI_BASE_URL=[\"\x27](https://[^\"\x27]+)', path.read_text(), re.M)
            if match:
                url = match[1]
                break
    if not url:
        raise ValueError('configuration_missing')
    # This exact source was explicitly approved by the user. Do not search other credentials.
    key = ''
    env = home/'.claude/.env'
    if env.is_file():
        for line in env.read_text().splitlines():
            match = re.match(r'\s*(?:export\s+)?LITELLM_API_KEY\s*=\s*(.*)', line)
            if match:
                key = match[1].strip().strip('\"\x27')
    if not key:
        raise ValueError('credential_missing')
    return url.rstrip('/'), key


def make_prompt(width, height):
    return f'''You are a film compositing colourist. Inspect this {width}x{height} family game photograph.
The left figure is the selected Ultraman hero and the person is on the right. Infer the actual environment,
light direction, temperature, brightness and remaining cutout edge halo from the image.
Choose subtle corrections ONLY for the right-hand person's integration with the scene.
Keep identity, face, body, pose, clothing, composition, mountain/background and the Ultraman hero unchanged.
Do not beautify facial features, slim bodies, invent limbs, add objects or change either figure's scale.
Return ONLY a JSON object with these fields (numbers, no code):
scene_summary: short description of observed lighting;
exposure_ev: -0.45 to 0.15; red_gain/green_gain/blue_gain: 0.85 to 1.15;
saturation: 0.75 to 1.05; edge_feather_px: 0.6 to 2.5 at 1080p;
light_wrap: 0 to 0.18; shadow_strength: 0 to 0.24.
Prefer restrained local matching and gentle edge blending. Leave skin recognisable.'''


LIMITS = {'exposure_ev':(-.45,.15), 'red_gain':(.85,1.15), 'green_gain':(.85,1.15),
          'blue_gain':(.85,1.15), 'saturation':(.75,1.05), 'edge_feather_px':(.6,2.5),
          'light_wrap':(0,.18), 'shadow_strength':(0,.24)}


def parse_recipe(text):
    if text.strip().startswith('```'):
        text = re.sub(r'^```(?:json)?\s*|\s*```$', '', text.strip())
    data = json.loads(text)
    recipe = {}
    import math
    for name, (low, high) in LIMITS.items():
        value = float(data[name])
        if not math.isfinite(value):
            raise ValueError('nonfinite_recipe')
        recipe[name] = max(low, min(high, value))
    recipe['scene_summary'] = str(data.get('scene_summary', ''))[:300]
    return recipe


def ask_model(image, url, key):
    import cv2
    h,w = image.shape[:2]
    scale = min(1,1280/w)
    preview=cv2.resize(image,(round(w*scale),round(h*scale)))
    ok,encoded=cv2.imencode('.jpg',preview,[cv2.IMWRITE_JPEG_QUALITY,90])
    if not ok:raise ValueError('image_encoding_failed')
    payload={'model':MODEL,'messages':[{'role':'user','content':[
        {'type':'text','text':make_prompt(w,h)},
        {'type':'image_url','image_url':{'url':'data:image/jpeg;base64,'+base64.b64encode(encoded).decode()}}]}],
        'max_tokens':900,'temperature':.2}
    endpoint=url+('/chat/completions' if url.endswith('/v1') else '/v1/chat/completions')
    request=urllib.request.Request(endpoint,data=json.dumps(payload).encode(),
        headers={'Authorization':'Bearer '+key,'Content-Type':'application/json'})
    # Credentials are never forwarded to a redirect destination.
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self,*args,**kwargs):return None
    with urllib.request.build_opener(NoRedirect).open(request,timeout=65) as response:
        raw=response.read(1024*1024)
    data=json.loads(raw)
    return parse_recipe(data['choices'][0]['message']['content'])


def harmonise(composite, plate, mask, recipe):
    import cv2
    import numpy as np
    cv2.setNumThreads(1)
    if composite.shape!=plate.shape or composite.shape[:2]!=mask.shape[:2]:
        raise ValueError('layer_size_mismatch')
    a=mask.astype(np.float32)/255
    if a.ndim==3:a=a[:,:,0]
    c=composite.astype(np.float32)/255;b=plate.astype(np.float32)/255
    person=np.clip((c-b*(1-a[:,:,None]))/np.maximum(a[:,:,None],.04),0,1)
    corrected=person*2**recipe['exposure_ev']*np.array([recipe['blue_gain'],recipe['green_gain'],recipe['red_gain']])
    gray=corrected.mean(axis=2,keepdims=True)
    corrected=np.clip(gray+(corrected-gray)*recipe['saturation'],0,1)
    feather=recipe['edge_feather_px']*composite.shape[0]/1080
    soft=cv2.GaussianBlur(a,(0,0),max(.5,feather))
    # Never invent body pixels outside the original matte; pull a halo inward.
    alpha=np.minimum(a,soft)
    edge=(1-cv2.erode(a,np.ones((5,5),np.uint8)))*alpha
    wrap=cv2.GaussianBlur(b,(0,0),max(2,composite.shape[0]/80))
    corrected=corrected*(1-edge[:,:,None]*recipe['light_wrap'])+wrap*edge[:,:,None]*recipe['light_wrap']
    background=b.copy()
    rows,cols=np.where(a>.5)
    if len(rows):
        bottom=int(rows.max());left=int(cols.min());right=int(cols.max())
        # Only cast a soft grounding shadow where the visible silhouette meets the floor.
        if bottom>composite.shape[0]*.82:
            shadow=np.zeros(a.shape,np.float32)
            cv2.ellipse(shadow,((left+right)//2,bottom),(max(1,(right-left)//3),max(1,round(feather*3))),0,0,360,1,-1)
            shadow=cv2.GaussianBlur(shadow,(0,0),max(2,feather*3))
            background*=1-shadow[:,:,None]*recipe['shadow_strength']
    result=corrected*alpha[:,:,None]+background*(1-alpha[:,:,None])
    return np.clip(np.rint(result*255),0,255).astype(np.uint8)


def enhance(source, plate_path, mask_path):
    url,key=configuration()
    import cv2
    source=Path(source)
    image=cv2.imread(str(source));plate=cv2.imread(str(plate_path));mask=cv2.imread(str(mask_path),0)
    if image is None or plate is None or mask is None:raise ValueError('missing_photo_layers')
    recipe=ask_model(image,url,key)
    result=harmonise(image,plate,mask,recipe)
    output=source.with_name(source.stem+'_AI.png')
    ok,png=cv2.imencode('.png',result)
    if not ok:raise ValueError('output_encoding_failed')
    pending=output.with_suffix('.png.tmp')
    try:
        with pending.open('xb') as stream:stream.write(png)
        os.replace(pending,output)
    finally:
        pending.unlink(missing_ok=True)
    return output,recipe
