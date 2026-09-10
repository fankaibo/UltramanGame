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
        readonly Material background,hero,person;
        readonly Transform personQuad,heroQuad;
        readonly Texture2D heroAtlas;
        readonly RectInt heroBounds;
        float lastShoulder,lastCenter,lastCrown;
        bool bodyMeasured;
        bool disposed;
        public PhotoComposition()
        {
            root=new GameObject("Victory photo composition");root.transform.position=new Vector3(10000,10000,0);
            var c=new GameObject("Photo camera");c.transform.SetParent(root.transform,false);c.transform.localPosition=new Vector3(0,0,-10);
            camera=c.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=4.5f;
            camera.aspect=16f/9;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.06f,.12f);
            camera.nearClipPlane=.1f;camera.farClipPlane=30;
            Preview=new RenderTexture(Width,Height,16,RenderTextureFormat.ARGB32);Preview.Create();camera.targetTexture=Preview;
            var city=Resources.Load<Texture2D>("Art/CityDusk");
            background=Layer("City",city,out var cityQuad);background.renderQueue=3000;
            float scale=Mathf.Max(16f/city.width,9f/city.height);cityQuad.localScale=new Vector3(city.width*scale,city.height*scale,1);cityQuad.localPosition=new Vector3(0,(city.height*scale-9)/2,2);
            var atlas=Resources.Load<Texture2D>("Art/TigaPhotoActions");
            if(!atlas)throw new InvalidOperationException("Missing photo Tiga atlas");
            heroAtlas=atlas;
            hero=Layer("Tiga front victory",atlas,out heroQuad);hero.SetFloat("_KeyGreen",1);hero.renderQueue=3001;
            int w=atlas.width/4,h=atlas.height/2;
            var bounds=Bounds(atlas,true,new RectInt(w*3,0,w,h));heroBounds=bounds;
            Place(heroQuad,hero,atlas,bounds,-3.9f);
            person=Layer("Person",null,out personQuad);person.renderQueue=3002;personQuad.gameObject.SetActive(false);
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
        static void Place(Transform quad,Material material,Texture2D texture,RectInt bounds,float x)
        {
            material.SetVector("_Frame",new Vector4(bounds.x/(float)texture.width,bounds.y/(float)texture.height,bounds.width/(float)texture.width,bounds.height/(float)texture.height));
            float scale=Mathf.Min(6.5f/bounds.width,7.45f/bounds.height),height=bounds.height*scale;
            quad.localScale=new Vector3(bounds.width*scale,height,1);quad.localPosition=new Vector3(x,-3.9f+height/2,0);
        }
        public bool SetPerson(Texture2D texture,PoseFrame pose=null)
        {
            var bounds=Bounds(texture,false,new RectInt(0,0,texture.width,texture.height));
            bool valid=bounds.width>0&&bounds.height>0;personQuad.gameObject.SetActive(valid);
            if(!valid)return false;
            float shoulder,center,crown;
            if(pose!=null&&PoseQuality.Present(pose,pose.capturedMs)&&PoseQuality.Reliable(pose.points[0],.45f))
            {
                var p=pose.points;
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
            float tileW=heroAtlas.width/4f,tileH=heroAtlas.height/2f;
            // Landmarks of this fixed victory illustration, relative to its atlas cell.
            float heroShoulder=tileH*.57f,heroCrown=tileH*.74f,heroCenter=tileW*3.53f;
            var personBody=new PhotoBody(bounds.xMin,bounds.xMax,bounds.yMin,bounds.yMax,center,shoulder,crown);
            var heroBody=new PhotoBody(heroBounds.xMin,heroBounds.xMax,heroBounds.yMin,heroBounds.yMax,heroCenter,heroShoulder,heroCrown);
            if(!PhotoLayout.TryFit(personBody,heroBody,out var layout))return false;
            var crop=heroBounds;
            // A half-body camera view gets a matching half-body hero; no full-body/giant-head pairing.
            int bottom=Mathf.Max(crop.yMin,Mathf.FloorToInt(layout.HeroBottom));
            crop=new RectInt(crop.x,bottom,crop.width,crop.yMax-bottom);
            person.mainTexture=texture;
            PlaceBody(personQuad,person,texture,bounds,layout.PersonScale,center,shoulder,3.7f,layout.ShoulderY);
            PlaceBody(heroQuad,hero,heroAtlas,crop,layout.HeroScale,heroCenter,heroShoulder,-3.7f,layout.HeroShoulderY);
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
        public void ResetFraming() {bodyMeasured=false;}
        public void HidePerson()=>personQuad.gameObject.SetActive(false);
        public void Render()=>camera.Render();
        public Texture2D Snapshot()
        {
            Render();var old=RenderTexture.active;
            try
            {
                RenderTexture.active=Preview;var texture=new Texture2D(Width,Height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,Width,Height),0,0);texture.Apply();return texture;
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
            Release(Preview);Release(background);Release(hero);Release(person);Release(root);
        }
    }
}
