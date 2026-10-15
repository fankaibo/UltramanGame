using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class HeroDamageEffectsReview
    {
        static void Require(bool pass,string message){if(!pass)throw new Exception(message);}
        static int Changed(Color32[] a,Color32[] b)
        {int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>3)n++;return n;}
        public static void Check()
        {
            string folder=Path.GetFullPath(Application.dataPath+"/../../artifacts/hero-contact-20261012/gpu");Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Damage review").transform;
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2.5f;
            camera.transform.position=new Vector3(0,0,-7);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(512,512,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(512,512,TextureFormat.RGB24,false);
            var fx=new CombatVfx(root);var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);
            void Tick(float dt)=>fx.Tick(state,camera,dt,Vector3.zero,Vector3.forward,Vector3.zero,Vector3.right,false,0,false,Vector3.forward,0,Vector3.zero,Vector3.zero);
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();if(name!=null)File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            void Damage()=>fx.Impact(Vector3.zero,false,hurt:true,direction:Vector3.right);
            var report=new StringBuilder();
            try
            {
                Tick(0);var empty=Read(null);Damage();Tick(.02f);
                Require(fx.ActiveHurtContactCount==1&&fx.ActiveContactCount==0,"Hurt flash missing or credited as outgoing punch");
                foreach(var line in root.GetComponentsInChildren<LineRenderer>())
                    if(line.name=="Arcade contact shockwave"||line.name=="Arcade impact ray")Require(!line.enabled,"Hurt retained a ring/radial ray");
                int sparks=0;foreach(var line in root.GetComponentsInChildren<LineRenderer>())
                    if(line.name=="Impact streak"&&line.enabled){Require(line.GetPosition(0).x<0,"Hurt spark went into surface");sparks++;}
                Require(sparks==16,"Hurt spark count changed");
                var first=Read("contact");int visible=Changed(empty,first);Require(visible>30,"No visible GPU damage flash");
                Tick(0);Require(Changed(first,Read(null))==0,"Zero-time damage redraw moved");
                Tick(.07f);Require(Changed(first,Read("dispersal"))>30,"Damage burst is static");
                foreach(var position in new[]{new Vector3(-5,1,-5),new Vector3(5,1,-5)})
                {camera.transform.position=position;camera.transform.LookAt(Vector3.zero);Require(Changed(empty,Read("view-"+Mathf.Sign(position.x)))>30,"Side-view flash invisible");}
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*3,camera.transform.rotation);blocker.transform.localScale=new Vector3(8,8,.1f);
                var opaque=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.3f,.4f)};blocker.GetComponent<Renderer>().sharedMaterial=opaque;
                var hidden=Read("occluded");fx.Clear();Require(Changed(hidden,Read(null))==0,"Damage leaked through foreground");
                UnityEngine.Object.DestroyImmediate(blocker);UnityEngine.Object.DestroyImmediate(opaque);
                for(int n=0;n<40;n++)Damage();Tick(0);Require(fx.ActiveHurtContactCount==6,"Damage pool did not stay bounded");
                Tick(.3f);Require(fx.ActiveHurtContactCount==0,"Contact outlived landing lead-in");Tick(1.3f);Require(Changed(empty,Read(null))==0,"Expired damage left pixels");
                for(int kind=0;kind<4;kind++)
                {
                    fx.Clear();fx.Impact(Vector3.zero,kind==1,blocked:kind==2,hurt:kind==3,direction:Vector3.right);Tick(.02f);
                    Require((fx.ActiveContactCount>0)==(kind==0),"Outgoing punch routing changed");
                    Require((fx.ActiveHurtContactCount>0)==(kind==3),"Damage leaked into shield or beam");
                }
                var monster=new MonsterAttackEffects(root,Vector3.right*2,Vector3.left*2);monster.Impact(false,Vector3.zero,1);monster.Tick(state,camera,.02f);
                foreach(var line in root.GetComponentsInChildren<LineRenderer>())Require(line.name!="Monster contact"||!line.enabled,"Monster overlaid a second damage ring");
                state.Pause();Tick(.02f);monster.Tick(state,camera,.02f);Require(fx.ActiveHurtContactCount==0,"Pause retained damage");
                state=new Battle();Damage();Tick(.02f);Require(fx.ActiveHurtContactCount==0,"Waiting retained damage");
                report.AppendLine($"PASS visiblePixels={visible} outwardSparks={sparks} views=3 zeroTime=unchanged motion=visible depth=occluded pool=6 expiry=passed routes=4 pause=cleared waiting=cleared duplicateRing=absent");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[HeroDamageEffectsReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
