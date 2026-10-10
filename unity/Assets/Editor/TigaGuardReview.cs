using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class TigaGuardReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/tiga-guard"));
        public static void Release(){Check();SurfaceImpactReview.CheckPunchRecovery();RosterPunchReview.CheckGuardTransitions();ComboStrikeReview.Validate();ComboStrikeReview.Interruptions();ComboStrikeReview.After();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception("Missing "+name);}
        public static void Check()
        {
            Directory.CreateDirectory(Folder+"/poses");File.Delete(Folder+"/guard-validation.txt");var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(932);
                var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int n=0;n<(Battle.TransformationSeconds+0.2f)/(.02f);n++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
                var lShoulder=Bone(hero.Root,"armBase_L");var rShoulder=Bone(hero.Root,"armBase_R");
                float time=0,dt=1f/rate;void Step(PlayerInput input){state.Tick(dt,input);while(state.TryCue(out _)){}hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);time+=dt;}
                for(int n=0;n<rate;n++)Step(new PlayerInput{Tracking=true});
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                float high=-100,forward=-100,gap=0,separation=100,maxStep=0,health=state.EnemyHealth;int contacts=0,fistContacts=0,samples=0,maxStrike=0,maxFrame=0;HeroAction maxAction=HeroAction.None;
                var oldLeft=hero.Root.InverseTransformPoint(hero.StrikeOrigin(HeroAction.LeftPunch));var oldRight=hero.Root.InverseTransformPoint(hero.HandPosition);
                var mesh=new Mesh();
                try
                {
                    CharacterReview.Save(world.Camera,target,$"{Folder}/poses/idle-{rate}.png");
                    for(int n=1;n<=10;n++)for(int frame=0;frame<rate;frame++)
                    {
                        bool left=n%2==1;Step(new PlayerInput{Tracking=true,LeftPunch=frame==0&&left,RightPunch=frame==0&&!left});
                        var l=hero.StrikeOrigin(HeroAction.LeftPunch);var r=hero.HandPosition;
                        var localL=hero.Root.InverseTransformPoint(l);var localR=hero.Root.InverseTransformPoint(r);
                        float handStep=Mathf.Max(Vector3.Distance(localL,oldLeft),Vector3.Distance(localR,oldRight));
                        if(handStep>maxStep){maxStep=handStep;maxStrike=n;maxFrame=frame;maxAction=state.Action;}
                        oldLeft=localL;oldRight=localR;
                        bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                        // The support hand stays close during contact; after
                        // .20 s both arms return to the relaxed forward stance.
                        if(punch&&state.ActionAge>=.07f&&state.ActionAge<=.205f)
                        {
                            var off=left?r:l;var shoulder=left?rShoulder:lShoulder;
                            high=Mathf.Max(high,off.y-shoulder.position.y);forward=Mathf.Max(forward,Vector3.Dot(off-shoulder.position,world.BattleAxis));samples++;
                        }
                        if(state.EnemyHealth<health)
                        {
                            contacts++;
                            // The fifth accepted attack is now a kick. Its wrist
                            // is a support hand, not the point hitting the chest.
                            if(!HeroKickMotion.Active(state))
                            {
                                fistContacts++;Vector3 active=left?l:r,off=left?r:l;separation=Mathf.Min(separation,Vector3.Dot(active-off,world.BattleAxis));
                                float nearest=100;foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                                {
                                    skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                                    for(int v=0;v<vertices.Length;v++)
                                    {
                                        var w=weights[v];float Chest(int b,float weight)=>skin.bones[b].name.StartsWith("bip_spine",StringComparison.Ordinal)?weight:0;
                                        if(Chest(w.boneIndex0,w.weight0)+Chest(w.boneIndex1,w.weight1)+Chest(w.boneIndex2,w.weight2)+Chest(w.boneIndex3,w.weight3)<.75f)continue;
                                        nearest=Mathf.Min(nearest,Vector3.Distance(active+world.BattleAxis*.12f,skin.transform.TransformPoint(vertices[v])));
                                    }
                                }
                                gap=Mathf.Max(gap,nearest);
                            }
                            if(n==1||n==2||n==5||n==10)CharacterReview.Save(world.Camera,target,$"{Folder}/poses/{rate}-contact-{n}.png");
                        }
                        health=state.EnemyHealth;
                    }
                    string result=$"{rate}Hz contacts={contacts} fistContacts={fistContacts} guardSamples={samples} offhandAboveShoulder={high:F4} offhandForward={forward:F4} handSeparation={separation:F4} chestGap={gap:F4} localHandStep={maxStep:F4} maxStrike={maxStrike} maxFrame={maxFrame} maxAction={maxAction}";
                    Debug.Log("[TigaGuardCheck] "+result);report.AppendLine(result);
                    if(contacts!=10||fistContacts!=9||samples<20||state.EnemyHealth!=40||state.Energy!=10||high>.03f||forward>.36f||separation<.65f||gap>.36f||maxStep>dt*36)
                        throw new Exception("Tiga guard/contact continuity failed: "+result);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(Folder+"/guard-validation.txt",report.ToString());
        }
    }
}
