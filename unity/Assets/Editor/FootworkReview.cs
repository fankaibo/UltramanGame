using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class FootworkReview
    {
        static Transform Bone(Transform root,string name)
        {foreach(var joint in root.GetComponentsInChildren<Transform>())if(joint.name==name)return joint;throw new Exception("Missing "+name+"; candidates="+string.Join(",",System.Array.ConvertAll(root.GetComponentsInChildren<Transform>(),x=>x.name.Contains("oot",StringComparison.OrdinalIgnoreCase)?x.name:"")));}
        public static void SamplePunch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<130;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            var left=Bone(hero.Root,"Foot_L");var right=Bone(hero.Root,"Foot_R");var axis=world.BattleAxis;var baselineL=left.position;var baselineR=right.position;
            var samples=new List<string>();float maxL=0,maxR=0,maxGap=0;
            for(int frame=0;frame<31;frame++)
            {
                float t=frame/60f;state.Tick(1/60f,new PlayerInput{Tracking=true,LeftPunch=frame==0});hero.Update(state,world.Camera,1/60f,t);enemy.Update(state,world.Camera,1/60f,t);world.Tick(state,1/60f,t);
                float l=Vector3.Dot(left.position-baselineL,axis),r=Vector3.Dot(right.position-baselineR,axis),gap=Mathf.Abs(l-r);maxL=Mathf.Max(maxL,Mathf.Abs(l));maxR=Mathf.Max(maxR,Mathf.Abs(r));maxGap=Mathf.Max(maxGap,gap);
                if(frame%3==0||frame==7||frame==9)samples.Add($"frame={frame} age={state.ActionAge:F3} root={Vector3.Dot(hero.Root.position-world.HeroHome,axis):F3} left={l:F3} right={r:F3} gap={gap:F3}");
            }
            foreach(var sample in samples)Debug.Log("[Footwork] "+sample);
            Debug.Log($"[Footwork] maxAbsFootTravel={Mathf.Max(maxL,maxR):F3} maxSupportGap={maxGap:F3}");
        }
    }
}
