using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Editor
{
    public static class CombatSampleReview
    {
        const int Fps=30,FrameCount=600;
        static readonly Vector3 Axis=new Vector3(1,0,1).normalized;
        static readonly Color Blue=new Color(.24f,.72f,1,.6f),Gold=new Color(1,.64f,.2f,.8f);
        static Material strokeMaterial;
        [MenuItem("UltramanGame/Review two-character combat sample")]
        public static void Render() => Run(false);
        public static void Keyframes() => Run(true);
        static void Run(bool keyframes)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Sample camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.015f,.03f,.08f);camera.nearClipPlane=.1f;camera.farClipPlane=120;camera.aspect=16/9f;
            var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);backdrop.name="Existing city backdrop";
            UnityEngine.Object.DestroyImmediate(backdrop.GetComponent<Collider>());backdrop.transform.SetParent(camera.transform,false);
            var backdropMat=new Material(Resources.Load<Shader>("Backdrop")){mainTexture=Resources.Load<Texture2D>("Art/CityDusk")};backdrop.GetComponent<Renderer>().sharedMaterial=backdropMat;
            Light("Sun",Quaternion.Euler(38,-40,0),.85f,new Color(1,.83f,.68f),true);
            Light("Sky fill",Quaternion.Euler(30,150,0),.65f,new Color(.35f,.6f,1),false);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.23f,.28f,.37f);RenderSettings.fog=false;
            float savedShadowDistance=QualitySettings.shadowDistance;
            QualitySettings.shadowDistance=30;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Transparent shadow receiver";
            ground.transform.position=Vector3.down*.015f;ground.transform.localScale=Vector3.one*3;
            ground.GetComponent<Renderer>().sharedMaterial=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/CombatSample/ContactShadow.shader"));
            ground.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var shadowCamera=new GameObject("Projected contact shadow").AddComponent<Camera>();shadowCamera.enabled=false;
            shadowCamera.transform.position=Vector3.up*10;shadowCamera.transform.LookAt(Vector3.zero,Vector3.forward);
            shadowCamera.orthographic=true;shadowCamera.orthographicSize=5;shadowCamera.aspect=1;
            shadowCamera.clearFlags=CameraClearFlags.SolidColor;shadowCamera.backgroundColor=Color.black;shadowCamera.cullingMask=1<<30;
            var shadowTexture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32){wrapMode=TextureWrapMode.Clamp};shadowTexture.Create();shadowCamera.targetTexture=shadowTexture;
            ground.GetComponent<Renderer>().sharedMaterial.mainTexture=shadowTexture;
            var shadowShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/CombatSample/ShadowSilhouette.shader");
            var hero=new SampleActor("Tiga");var monster=new SampleActor("Golza");
            var fx=new GameObject("Per-frame sample effects");
            strokeMaterial=new Material(Resources.Load<Shader>("SoftGlow")){color=Color.white};
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();camera.targetTexture=target;
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/combat-sample"));Directory.CreateDirectory(output);
            string frames=Path.Combine(output,keyframes?"keyframes":"frames");Directory.CreateDirectory(frames);
            int[] checkpoints={0,60,105,134,137,164,225,267,280,355,390,430,485,570};
            float minFoot=float.MaxValue,maxFoot=-float.MaxValue,maxHandStep=0;Vector3 lastHand=Vector3.zero;
            try
            {
                for(int frame=0;frame<FrameCount;frame++)
                {
                    float t=frame/(float)Fps;
                    while(fx.transform.childCount>0)UnityEngine.Object.DestroyImmediate(fx.transform.GetChild(0).gameObject);
                    float approach=Ease(1.5f,3.5f,t),retreat=Ease(10.2f,11.7f,t);
                    var hp=-Axis*Mathf.Lerp(2.1f,.82f,approach)-Axis*.65f*retreat;
                    var mp=Axis*Mathf.Lerp(1.8f,1.08f,approach)+Axis*.6f*retreat;
                    string hc="Idle",mc="Idle";float ha=t%2,ma=t%2;
                    if(t>=1.5f&&t<3.5f){hc=mc="Walk";ha=ma=t-1.5f;}
                    foreach(float start in new[]{4.2f,5.2f})
                    {
                        if(t>=start&&t<start+.68f){hc=start<5?"LeftPunch":"RightPunch";ha=ImpactTime(t-start,.28f);}
                        float hit=start+.28f;
                        if(t>=hit&&t<hit+.4f){mc="Hurt";ma=t-hit;mp+=Axis*.16f*Mathf.Sin(Mathf.PI*(t-hit)/.4f);}
                    }
                    if(t>=7&&t<8.5f){mc="Windup";ma=t-7;}
                    if(t>=7.7f&&t<10){hc="Guard";ha=Mathf.Min(.65f,t-7.7f);}
                    if(t>=8.5f&&t<9.55f){mc="Attack";ma=ImpactTime(t-8.5f,.4f);mp-=Axis*.12f*Mathf.Sin(Mathf.PI*(t-8.5f)/1.05f);}
                    if(t>=10.2f&&t<11.7f){hc=mc="Walk";ha=ma=2-(t-10.2f)*1.3f;}
                    if(t>=12&&t<17.9f){hc="Beam";ha=Mathf.Min(1.8f,(t-12)*.75f);}
                    if(t>=14&&t<16){mc="Hurt";ma=.08f+.08f*Mathf.Sin(t*15);mp+=Axis*(.06f+.05f*Mathf.Sin(t*12));}
                    if(t>=16){mc="Defeat";ma=Mathf.Min(2.4f,t-16);}
                    if(t>=18.1f){hc="Victory";ha=t-18.1f;}
                    hero.Pose(hc,ha,hp,Axis);monster.Pose(mc,ma,mp,-Axis);
                    float focus=Ease(12,12.8f,t)*(1-Ease(13.4f,14,t));
                    monster.Opacity(1-focus);
                    var wide=new Vector3(.4f,3.25f,-9.2f);var aim=new Vector3(0,1.65f,.12f);
                    // Orbit toward the hero's front for the dedicated beam shot, then return to the duel axis.
                    var close=hp+Axis*4+new Vector3(2.7f,2.8f,-1.4f);
                    camera.transform.position=Vector3.Lerp(wide+new Vector3(-.15f*Mathf.Sin(t*.2f),0,.35f*approach),close,focus);
                    camera.transform.LookAt(Vector3.Lerp(aim,hp+Vector3.up*2.62f,focus));camera.fieldOfView=Mathf.Lerp(33,29,focus);
                    float shake=Pulse(t,4.48f,.16f)*.035f+Pulse(t,5.48f,.16f)*.045f+Pulse(t,8.9f,.22f)*.05f;
                    if(t>=14&&t<16)shake+=.012f;
                    camera.transform.position+=camera.transform.right*Mathf.Sin(t*93)*shake;
                    float height=160*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),aspect=backdropMat.mainTexture.width/(float)backdropMat.mainTexture.height;
                    float scale=Mathf.Max(1,camera.aspect/aspect)*1.5f;
                    backdrop.transform.localScale=new Vector3(height*aspect*scale,height*scale,1);backdrop.transform.localPosition=new Vector3(0,height*(scale-1)*.5f,80);
                    foreach(float hit in new[]{4.48f,5.48f})
                    {
                        float age=t-hit;
                        if(age>=0&&age<.45f)Burst(fx.transform,mp-Axis*.35f+Vector3.up*2.25f,age,Gold,camera);
                    }
                    if(t>=8.68f&&t<9.10f)
                    {
                        var hand=monster.Joint("bip_hand_R").position;
                        Arc(fx.transform,hand,Axis,new Color(1,.46f,.13f,.55f),.5f,.05f,(t-8.68f)/.42f);
                    }
                    if(t>=7.95f&&t<9.6f)
                    {
                        float fade=Ease(7.95f,8.2f,t)*(1-Ease(9.2f,9.6f,t));
                        Ring(fx.transform,hp+Axis*.73f+Vector3.up*2,Axis,.8f,.025f,new Color(.25f,.75f,1,fade*.5f));
                    }
                    if(t>=8.9f&&t<9.4f)Burst(fx.transform,hp+Axis*.73f+Vector3.up*2,t-8.9f,Blue,camera);
                    var beamStart=Vector3.Lerp(hero.Joint("ForearmBase_R").position,hero.Joint("HandBase_R").position,.6f);
                    if(t>=12.65f&&t<14)
                    {
                        float charge=Ease(12.65f,14,t);
                        Ring(fx.transform,beamStart,camera.transform.forward,.14f+.16f*charge,.02f,Blue);
                        Star(fx.transform,beamStart,.09f+.08f*charge,Blue,camera);
                    }
                    if(t>=14&&t<16)
                    {
                        var end=mp+Vector3.up*2.3f;
                        Line(fx.transform,beamStart,end,.20f+Mathf.Sin(t*43)*.012f,new Color(.18f,.6f,1,.45f));
                        Line(fx.transform,beamStart,end,.08f,new Color(.8f,.93f,1,.9f));
                        for(int i=0;i<8;i++)
                        {float u=Mathf.Repeat(t*1.8f+i/8f,1);Ring(fx.transform,Vector3.Lerp(beamStart,end,u),end-beamStart,.09f+.05f*Mathf.Sin(u*12+t*20),.015f,Blue);}
                        Burst(fx.transform,end,(t*2)% .4f,Blue,camera);Star(fx.transform,beamStart,.22f,Blue,camera);
                    }
                    var foot=monster.Joint("bip_foot_L").position;minFoot=Mathf.Min(minFoot,foot.y);maxFoot=Mathf.Max(maxFoot,foot.y);
                    var handNow=hero.Joint("HandBase_R").position;if(frame>0)maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(lastHand,handNow));lastHand=handNow;
                    if(!float.IsFinite(handNow.x)||!float.IsFinite(foot.y))throw new Exception("Invalid skeletal transform");
                    if(!keyframes||Array.IndexOf(checkpoints,frame)>=0)CharacterReview.Save(camera,target,Path.Combine(frames,$"frame-{frame:D4}.png"),()=>shadowCamera.RenderWithShader(shadowShader,""));
                    if(frame%120==0)Debug.Log($"[CombatSample] frame {frame}/{FrameCount}");
                }
                if(maxHandStep>.65f)throw new Exception("Discontinuous hero transition: "+maxHandStep);
                File.WriteAllText(Path.Combine(output,"validation.txt"),$"duration=20s frames=600 fps=30\nmode={(keyframes?"keyframes":"full")}\nmonsterFootJointY={minFoot:F3}..{maxFoot:F3}\nmaxHeroHandStep={maxHandStep:F3}\nBoth imported meshes and all clips sampled. Webcam and playable game were not started.\n");
                Debug.Log($"[CombatSample] passed maxHandStep={maxHandStep:F3} output={frames}");
            }
            finally {QualitySettings.shadowDistance=savedShadowDistance;camera.targetTexture=null;shadowCamera.targetTexture=null;RenderTexture.active=null;target.Release();shadowTexture.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(shadowTexture);}
        }
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static float Pulse(float t,float start,float duration)=>t>=start&&t<start+duration?1-(t-start)/duration:0;
        // Brief contact hold; the recovery catches up without changing the sequence duration.
        static float ImpactTime(float t,float hit)=>t<hit?t:t<hit+.055f?hit:Mathf.Lerp(hit,t,Mathf.Clamp01((t-hit-.055f)/.10f));
        static void Light(string name,Quaternion rotation,float power,Color color,bool shadows)
        {var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;l.transform.rotation=rotation;l.intensity=power;l.color=color;l.shadows=shadows?LightShadows.Soft:LightShadows.None;l.shadowBias=.02f;l.shadowNormalBias=.08f;}
        static LineRenderer NewLine(Transform parent,float width,Color color,int count)
        {var go=new GameObject("Light stroke");go.transform.SetParent(parent);var l=go.AddComponent<LineRenderer>();l.sharedMaterial=strokeMaterial;l.startColor=l.endColor=color;l.positionCount=count;l.widthMultiplier=width;l.numCapVertices=4;return l;}
        static void Line(Transform parent,Vector3 a,Vector3 b,float width,Color color)
        {var l=NewLine(parent,width,color,2);l.SetPosition(0,a);l.SetPosition(1,b);}
        static void Ring(Transform parent,Vector3 p,Vector3 normal,float radius,float width,Color color)
        {var l=NewLine(parent,width,color,64);l.loop=true;var q=Quaternion.FromToRotation(Vector3.forward,normal);for(int i=0;i<64;i++){float a=i*Mathf.PI/32;l.SetPosition(i,p+q*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius);}}
        static void Arc(Transform parent,Vector3 p,Vector3 normal,Color c,float radius,float width,float age)
        {var l=NewLine(parent,width,c,24);var q=Quaternion.FromToRotation(Vector3.forward,normal);for(int i=0;i<24;i++){float a=(i/23f-.5f)*2.4f+age*3;l.SetPosition(i,p+q*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius);}}
        static void Star(Transform parent,Vector3 p,float size,Color c,Camera camera)
        {Line(parent,p-camera.transform.right*size,p+camera.transform.right*size,.02f,c);Line(parent,p-camera.transform.up*size,p+camera.transform.up*size,.02f,c);}
        static void Burst(Transform parent,Vector3 p,float age,Color color,Camera camera)
        {
            float fade=1-Mathf.Clamp01(age/.45f);color.a*=fade;
            for(int i=0;i<16;i++)
            {float a=i*2.39996f;var d=(camera.transform.right*Mathf.Cos(a)+camera.transform.up*Mathf.Sin(a)+camera.transform.forward*Mathf.Sin(i*7)*.4f).normalized;
             var v=d*(.15f+age*(1.5f+i%4)) + Vector3.down*age*age;Line(parent,p+v*.7f,p+v,.015f+fade*.018f,color);}
            if(age<.16f)Ring(parent,p,camera.transform.forward,.10f+age*2.8f,.028f,color);
        }
    }
}
