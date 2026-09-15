using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;
namespace UltramanGame.Editor
{
    public static class RosterReview
    {
        [MenuItem("UltramanGame/Review hero roster")]
        public static void Render()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-roster"));Directory.CreateDirectory(folder);
            for(int i=0;i<HeroRoster.Count;i++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var def=HeroRoster.At(i);var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(def.Id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                if(!hero.IsRigged)throw new Exception(def.Name+" has no skeletal model");world.BindActors(hero,enemy);
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                var target=new RenderTexture(1920,1080,24);target.antiAliasing=4;target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                CharacterReview.Save(world.Camera,target,Path.Combine(folder,def.Id+"-idle.png"));
                foreach(bool left in new[]{true,false})
                {
                    state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                    for(int step=0;step<180;step++)state.Tick(1/60f,new PlayerInput{Tracking=true});
                    hero.Update(state,world.Camera,0,0);var l=hero.StrikeOrigin(HeroAction.LeftPunch);var r=hero.StrikeOrigin(HeroAction.RightPunch);
                    state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left});
                    for(int step=0;step<6;step++)state.Tick(1/60f,new PlayerInput{Tracking=true});
                    hero.Update(state,world.Camera,0,0);
                    float ld=Vector3.Distance(l,hero.StrikeOrigin(HeroAction.LeftPunch)),rd=Vector3.Distance(r,hero.StrikeOrigin(HeroAction.RightPunch));
                    if((left?ld:rd)<.25f)throw new Exception(def.Id+" punch failed to move the selected hand");
                    Debug.Log($"[RosterMotion] hero={def.Id} hand={(left?"left":"right")} leftTravel={ld:F3} rightTravel={rd:F3}");
                    CharacterReview.Save(world.Camera,target,Path.Combine(folder,def.Id+(left?"-left.png":"-right.png")));
                }
                using(var photo=new PhotoComposition(1920,1080,def.Id))
                {
                    var person=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    string testPerson=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/photo-review/synthetic-person.png"));
                    var points=new PosePoint[33];for(int j=0;j<points.Length;j++)points[j]=new PosePoint(.5f,.5f);
                    points[0]=new PosePoint(.5f,128/480f);points[11]=new PosePoint(395/640f,192/480f);points[12]=new PosePoint(245/640f,192/480f);
                    points[25]=new PosePoint(.58f,.74f);points[26]=new PosePoint(.42f,.74f);points[27]=new PosePoint(.60f,.94f);points[28]=new PosePoint(.40f,.94f);
                    var pose=new PoseFrame{schema=1,source="synthetic",streamId="roster-photo",tracked=true,sequence=1,capturedMs=10000,points=points};
                    if(!person.LoadImage(File.ReadAllBytes(testPerson))||!photo.SetPerson(person,pose)||!photo.FullBody)throw new Exception("Synthetic full-body photo framing failed");
                    var shot=photo.Snapshot();File.WriteAllBytes(Path.Combine(folder,def.Id+"-photo.png"),shot.EncodeToPNG());UnityEngine.Object.DestroyImmediate(shot);
                    if(def.Id=="Zero")
                    {File.WriteAllBytes(Path.Combine(folder,"Zero-plate.png"),photo.CleanPlate());File.WriteAllBytes(Path.Combine(folder,"Zero-mask.png"),photo.PersonMatte());}
                    UnityEngine.Object.DestroyImmediate(person);
                }
                world.Camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                Debug.Log($"[RosterReview] hero={def.Id} model=rigged actions=passed photo=passed");
            }
            RiggedReview.ValidateMotion();
        }
    }
}
