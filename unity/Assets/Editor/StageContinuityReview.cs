using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class StageContinuityReview
    {
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/stage-continuity-20261012");
        public static void Before()=>Run("before",60,16f/9,true,false);
        public static void After()
        {
            Run("after",60,16f/9,true,true);
            var before=File.ReadAllLines(Folder+"/before/trace.csv");var after=File.ReadAllLines(Folder+"/after/trace.csv");
            if(before.Length!=after.Length)throw new Exception("Stage changed full-round length");
            for(int row=1;row<before.Length;row++)
            {
                var a=before[row].Split(',');var b=after[row].Split(',');
                for(int col=0;col<a.Length;col++)if((col<9||col>11)&&a[col]!=b[col])throw new Exception($"Stage changed gameplay/camera row={row} col={col}");
            }
            File.WriteAllText(Folder+"/identity-validation.txt",$"gameplay/camera unchanged rows={before.Length-1}");
        }
        public static void Coverage()
        {
            foreach(int hz in new[]{15,30,60})foreach(float aspect in new[]{16f/9,16f/10,4f/3})Run("coverage",hz,aspect,false,true);
        }
        static void Run(string version,int hz,float aspect,bool movie,bool strict)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(20260909);
            var world=new GameWorld();var b=new Battle();var input=new ReviewPlayback(clawCycle:true);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var plate=GameObject.Find("Realistic Mount Fuji night backdrop").transform;
            world.Camera.aspect=aspect;
            string folder=Folder+"/"+version+(movie?"":"-"+hz+"-"+aspect.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));Directory.CreateDirectory(folder+"/frames");
            var csv=new StringBuilder("frame,time,phase,action,enemy,health,energy,punches,blocks,scale,coverage,requiredBaseScale,cameraX,cameraY,cameraZ,lens\n");
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};if(movie){rt.Create();world.Camera.targetTexture=rt;}
            float dt=1f/hz,victory=-1,lastHealth=b.EnemyHealth,maxCoverage=0,maxRequired=0,minScale=1000,maxScale=0;int uncovered=0,frames=0;bool paused=false;int beams=0;
            string worstPhase="";int worstFrame=0;
            for(int frame=0;frame<hz*160;frame++)
            {
                float time=frame*dt;b.Tick(world.BattleDelta(dt,b),input.Next(b,dt));while(b.TryCue(out var cue))world.Cue(cue,b);
                hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);
                if(b.EnemyHealth<lastHealth)world.Hit(lastHealth-b.EnemyHealth>1,b);lastHealth=b.EnemyHealth;
                world.Tick(b,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);if(world.BeamStarted)beams++;if(b.Phase==GamePhase.Paused)paused=true;
                var plane=new Plane(plate.forward,plate.position);float coverage=0;
                foreach(Vector3 corner in new[]{Vector3.zero,Vector3.right,Vector3.up,new Vector3(1,1,0)})
                {
                    var ray=world.Camera.ViewportPointToRay(corner);
                    if(!plane.Raycast(ray,out float distance))throw new Exception("Backdrop lies behind camera");
                    Vector3 local=plate.InverseTransformPoint(ray.GetPoint(distance));coverage=Mathf.Max(coverage,Mathf.Abs(local.x)*2,Mathf.Abs(local.y)*2);
                }
                if(coverage>.998f)uncovered++;
                minScale=Mathf.Min(minScale,plate.localScale.y);maxScale=Mathf.Max(maxScale,plate.localScale.y);
                float required=coverage*plate.localScale.y/(160*Mathf.Tan(27*Mathf.Deg2Rad*.5f));maxRequired=Mathf.Max(maxRequired,required);
                if(coverage>maxCoverage){maxCoverage=coverage;worstPhase=b.Phase+"/"+b.Action+"/"+b.Enemy;worstFrame=frame;}
                var c=world.Camera.transform.position;
                csv.AppendLine(FormattableString.Invariant($"{frame},{time:F4},{b.Phase},{b.Action},{b.Enemy},{b.EnemyHealth},{b.Energy},{b.Punches},{b.Blocks},{plate.localScale.y:F5},{coverage:F6},{required:F6},{c.x:F5},{c.y:F5},{c.z:F5},{world.Camera.fieldOfView:F5}"));
                if(movie&&frame%3==0){CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{frames:D4}.jpg");frames++;}
                if(b.Phase==GamePhase.Victory){if(victory<0)victory=time;if(time-victory>5)break;}
            }
            File.WriteAllText(folder+"/trace.csv",csv.ToString());
            string result=$"{version} hz={hz} aspect={aspect:F4} frames={frames} uncovered={uncovered} maxCoverage={maxCoverage:F6} requiredScale={maxRequired:F6} worst={worstPhase} frame={worstFrame} scaleRange={minScale:F5}..{maxScale:F5} punches={b.Punches} beams={beams} blocks={b.Blocks} health={b.EnemyHealth} paused={paused}";
            File.WriteAllText(folder+"/validation.txt",result);Debug.Log("[StageContinuityReview] "+result);
            if(b.Phase!=GamePhase.Victory||b.Punches!=32||beams!=2||b.Blocks<4||b.HitsTaken!=1||!paused)throw new Exception("Incomplete full-round stage fixture");
            if(strict&&maxScale-minScale>.001f)throw new Exception("Landscape changes world scale during shot sequence");
            if(strict&&uncovered>0)throw new Exception("Backdrop does not cover all camera corners: "+result);
            world.Camera.targetTexture=null;RenderTexture.active=null;if(movie)rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
