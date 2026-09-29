using System;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // A dedicated offscreen camera exports the composition alone, without HUD/countdown/buttons.
    public sealed class PhotoComposition : IDisposable
    {
        public const int Width=1920,Height=1080;
        public readonly RenderTexture Preview;
        readonly GameObject root;
        readonly Camera camera;
        readonly Material background,person;
        readonly Transform personQuad,backgroundQuad;
        readonly PhotoLighting lighting;
        public PhotoHero Hero {get;private set;}
        float lastShoulder,lastCenter,lastCrown;
        bool bodyMeasured;
        bool? measuredFullBody;
        public bool FullBody {get;private set;}
        bool disposed,dirty=true;
        public PhotoComposition(int width=Width,int height=Height,string heroId="Tiga")
        {
            try
            {
                root=new GameObject("Victory photo composition");root.transform.position=new Vector3(10000,10000,0);
                var c=new GameObject("Photo camera");c.transform.SetParent(root.transform,false);c.transform.localPosition=new Vector3(0,0,-10);
                camera=c.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=4.5f;
                camera.aspect=16f/9;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.06f,.12f);
                camera.nearClipPlane=.1f;camera.farClipPlane=30;
                camera.renderingPath=RenderingPath.Forward;
                Preview=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};Preview.Create();camera.targetTexture=Preview;
                var fuji=Resources.Load<Texture2D>("Art/VolcanoFujiNight");
                // Background has no depth writes and draws before the opaque hero.
                background=Layer("Realistic Mount Fuji night",fuji,out var fujiQuad);background.renderQueue=1000;
                backgroundQuad=fujiQuad;
                float scale=Mathf.Max(16f/fuji.width,9f/fuji.height);fujiQuad.localScale=new Vector3(fuji.width*scale,fuji.height*scale,1);fujiQuad.localPosition=new Vector3(0,(fuji.height*scale-9)/2,2);
                Hero=new PhotoHero(heroId,root.transform,camera);lighting=new PhotoLighting(root.transform);
                person=Layer("Person",null,out personQuad);person.renderQueue=3002;personQuad.gameObject.SetActive(false);
            }
            catch{Dispose();throw;}
        }
        Material Layer(string name,Texture texture,out Transform quad)
        {
            var material=new Material(Resources.Load<Shader>("PhotoLayer"));material.mainTexture=texture;
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name=name;obj.layer=31;quad=obj.transform;quad.SetParent(root.transform,false);
            obj.GetComponent<Renderer>().sharedMaterial=material;Release(obj.GetComponent<Collider>());return material;
        }
        static RectInt Bounds(Texture2D texture,bool key,RectInt region)
        {
            var pixels=texture.GetPixels32();int left=region.xMax,bottom=region.yMax,right=region.x,top=region.y;
            for(int y=region.y;y<region.yMax;y++)for(int x=region.x;x<region.xMax;x++)
            {
                var p=pixels[y*texture.width+x];bool solid=key?p.g-Mathf.Max(p.r,p.b)<32:p.a>100;
                if(!solid)continue;left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
            }
            if(right<=left||top<=bottom)return new RectInt();
            left=Math.Max(region.x,left-5);bottom=Math.Max(region.y,bottom-5);right=Math.Min(region.xMax,right+6);top=Math.Min(region.yMax,top+6);
            return new RectInt(left,bottom,right-left,top-bottom);
        }
        public bool SetPerson(Texture2D texture,PoseFrame pose=null)
        {
            dirty=true;
            var bounds=Bounds(texture,false,new RectInt(0,0,texture.width,texture.height));
            bool valid=bounds.width>0&&bounds.height>0;personQuad.gameObject.SetActive(valid);
            if(!valid)return false;
            float shoulder,center,crown;
            if(pose!=null&&PoseQuality.Present(pose,pose.capturedMs)&&PoseQuality.Reliable(pose.points[0],.45f))
            {
                var p=pose.points;
                // Visible knees and ankles prove full-body framing without assuming
                // a four-year-old has the same head/body proportions as an adult hero.
                bool feet=true;
                foreach(int side in new[]{0,1})
                {
                    var knee=p[25+side];var ankle=p[27+side];
                    feet&=PoseQuality.Reliable(knee,.6f)&&PoseQuality.Reliable(ankle,.6f)&&
                        ankle.x>.02f&&ankle.x<.98f&&ankle.y<.98f&&ankle.y>knee.y+.04f&&knee.y>p[11+side].y+.16f;
                }
                measuredFullBody=feet?(bool?)true:null;
                shoulder=(1-(p[11].y+p[12].y)/2)*texture.height;
                center=(1-p[0].x)*texture.width; // Camera pixels are mirrored once by the photo service.
                float nose=(1-p[0].y)*texture.height;
                float span=Math.Abs(p[11].x-p[12].x)*texture.width;
                crown=HeadTop(texture,bounds,center,nose,span);
                if(crown-shoulder<texture.height*.06f||crown-shoulder>texture.height*.65f)
                    return false;
                float blend=bodyMeasured?.3f:1;
                lastShoulder=Mathf.Lerp(lastShoulder,shoulder,blend);lastCenter=Mathf.Lerp(lastCenter,center,blend);
                lastCrown=Mathf.Lerp(lastCrown,crown,blend);bodyMeasured=true;
            }
            else if(!bodyMeasured)
            {
                // Conservative silhouette fallback for previews without pose data; fit both actors to the same body reference.
                lastCenter=bounds.center.x;lastCrown=bounds.yMax;
                lastShoulder=lastCrown-Mathf.Min(bounds.height*.40f,bounds.width*.65f);
            }
            shoulder=lastShoulder;center=lastCenter;crown=lastCrown;
            var personBody=new PhotoBody(bounds.xMin,bounds.xMax,bounds.yMin,bounds.yMax,center,shoulder,crown);
            if(!PhotoLayout.TryFit(personBody,Hero.Body,out var layout,measuredFullBody))return false;
            FullBody=layout.FullBody;
            person.mainTexture=texture;
            PlaceBody(personQuad,person,texture,bounds,layout.PersonScale,center,shoulder,3.7f,layout.ShoulderY);
            // Hero placement, crop and scale stay exactly as constructed, across every camera frame and retake.
            return true;
        }
        static float HeadTop(Texture2D texture,RectInt bounds,float center,float nose,float span)
        {
            var pixels=texture.GetPixels32();int half=Mathf.Max(3,Mathf.RoundToInt(span*.14f));
            int left=Mathf.Clamp(Mathf.RoundToInt(center)-half,0,texture.width-1),right=Mathf.Clamp(Mathf.RoundToInt(center)+half,0,texture.width-1);
            // Search only the narrow face column above the nose, excluding raised hands beside the head.
            for(int y=bounds.yMax-1;y>Mathf.Max(bounds.yMin,nose);y--)
            {
                int solid=0;for(int x=left;x<=right;x++)if(pixels[y*texture.width+x].a>128)solid++;
                if(solid>(right-left+1)*.65f)return y+1;
            }
            return nose+span*.28f;
        }
        static void PlaceBody(Transform quad,Material material,Texture2D texture,RectInt bounds,float scale,float anchorX,float anchorY,float x,float y)
        {
            material.SetVector("_Frame",new Vector4(bounds.x/(float)texture.width,bounds.y/(float)texture.height,bounds.width/(float)texture.width,bounds.height/(float)texture.height));
            quad.localScale=new Vector3(bounds.width*scale,bounds.height*scale,1);
            quad.localPosition=new Vector3(x+(bounds.center.x-anchorX)*scale,y+(bounds.center.y-anchorY)*scale,0);
        }
        public void ResetFraming() {bodyMeasured=false;measuredFullBody=null;}
        public void HidePerson()
        {
            if(!personQuad.gameObject.activeSelf)return;
            personQuad.gameObject.SetActive(false);dirty=true;
        }
        // Camera cutouts arrive at 8 FPS. Keep the composed texture between
        // deliveries instead of drawing the same 1080P/2K layers every frame.
        public void Render(bool force=false)
        {
            if(!dirty&&!force)return;
            lighting.Render(camera);dirty=false;
        }
        public byte[] CleanPlate()
        {
            bool visible=personQuad.gameObject.activeSelf;
            try {personQuad.gameObject.SetActive(false);var picture=Snapshot();try{return picture.EncodeToPNG();}finally{Release(picture);}}
            finally{personQuad.gameObject.SetActive(visible);dirty=true;}
        }
        public byte[] PersonMatte()
        {
            var color=camera.backgroundColor;
            var previousTarget=camera.targetTexture;
            bool heroVisible=Hero.Root.gameObject.activeSelf,backgroundVisible=backgroundQuad.gameObject.activeSelf;
            // Alpha is data, not a display color. An sRGB target would encode
            // a 50% edge as 73.5%, widening the AI integration mask.
            var matte=RenderTexture.GetTemporary(Preview.width,Preview.height,16,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            try
            {
                backgroundQuad.gameObject.SetActive(false);Hero.Root.gameObject.SetActive(false);camera.backgroundColor=Color.black;person.SetFloat("_MaskOnly",1);
                camera.targetTexture=matte;camera.Render();
                var picture=Readback(matte,true);try{return picture.EncodeToPNG();}finally{Release(picture);}
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.ReleaseTemporary(matte);
                backgroundQuad.gameObject.SetActive(backgroundVisible);Hero.Root.gameObject.SetActive(heroVisible);camera.backgroundColor=color;person.SetFloat("_MaskOnly",0);dirty=true;
            }
        }
        public Texture2D Snapshot()
        {
            Render(true);return Readback(Preview,false);
        }
        static Texture2D Readback(RenderTexture target,bool linear)
        {
            var old=RenderTexture.active;
            try
            {
                RenderTexture.active=target;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false,linear);
                texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();return texture;
            }
            finally {RenderTexture.active=old;}
        }
        static void Release(UnityEngine.Object obj)
        {if(!obj)return;if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj);}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            // Unity may destroy scene cameras before ArenaController.OnDestroy on quit.
            if(camera)camera.targetTexture=null;
            if(Preview)Preview.Release();
            Release(Preview);Release(background);Release(person);Release(root);
        }
    }
}
