using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedCastTransitions
    {
        public static void Validate()
        {
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
                string handNote="";Battle b=null;float dt=1f/hz,time=0,health=200,handStep=0;Vector3 oldLeft=Vector3.zero,oldRight=Vector3.zero;
                void Draw(float step)
                {hero.Update(b,world.Camera,step,time);enemy.Update(b,world.Camera,step,time);world.Tick(b,step,time);}
                void Step(PlayerInput input)
                {
                    time+=dt;b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);if(b.EnemyHealth<health)world.Hit(b.Action==HeroAction.Beam,b);health=b.EnemyHealth;world.Tick(b,dt,time);
                    var left=hero.StrikeOrigin(HeroAction.LeftPunch);var right=hero.HandPosition;float jump=Mathf.Max(Vector3.Distance(left,oldLeft),Vector3.Distance(right,oldRight));if(jump>handStep){handStep=jump;handNote=$"action={b.Action} age={b.ActionAge} shield={b.Shield}";}oldLeft=left;oldRight=right;
                }
                // The recognizer sends ownership together with a confirmed
                // shield. A bare keyboard shield intentionally waits for an
                // unreleased strike; it is not the body-tracking protocol.
                void Idle(float seconds,bool shield=false){for(int f=0;f<Mathf.CeilToInt(seconds/dt);f++)Step(new PlayerInput{Tracking=true,Shield=shield,GuardIntent=shield});}
                void Start()
                {b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}world.ResetPresentation();health=200;Idle(.4f);handStep=0;}
                float peakGuard=0;
                foreach(bool left in new[]{true,false})foreach(float speed in new[]{.65f,1.7f})
                {
                    // Defence acquires both before and after release; only an
                    // already released shot may finish while the hand folds in.
                    foreach(bool released in new[]{false,true})foreach(bool intentFirst in new[]{false,true})
                    {
                        Start();Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,RangedAttack=true,AttackSpeed=speed});
                        while(released?!b.Shot.Active:b.ActionAge<.04f)Step(new PlayerInput{Tracking=true});
                        handStep=0;
                        if(intentFirst)for(int f=0;f<Mathf.CeilToInt(.12f/dt);f++)Step(new PlayerInput{Tracking=true,GuardIntent=true});
                        Idle(.8f,true);
                        peakGuard=Mathf.Max(peakGuard,handStep);
                        if(!b.Shield||b.Punches!=(released?1:0)||world.Projectile.Impacts!=(released?1:0))throw new Exception($"Guard takeover {id}-{hz} left={left} speed={speed} released={released}: shield={b.Shield} punches={b.Punches} impacts={world.Projectile.Impacts} action={b.Action}");
                        if(hz==60&&handStep>.65f)throw new Exception($"Guard hand jump {id}-{hz} left={left} speed={speed} released={released}: {handStep} {handNote}");
                    }
                    Start();Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,RangedAttack=true,AttackSpeed=speed});while(!b.Shot.Active)Step(new PlayerInput{Tracking=true});
                    b.Pause();Draw(0);var frozen=hero.HandPosition;var frozenRoot=hero.Root.position;for(int i=0;i<5;i++)Draw(0);
                    if(Vector3.Distance(frozen,hero.HandPosition)>.001f||Vector3.Distance(frozenRoot,hero.Root.position)>.001f||world.Projectile.Visible)throw new Exception("Paused cast accumulates");
                    Start();if(world.Projectile.Visible||world.Projectile.LaunchVisible)throw new Exception("New round retained cast");
                    Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,RangedAttack=true,AttackSpeed=speed});Idle(.8f);
                    Step(new PlayerInput{Tracking=true,LeftPunch=!left,RightPunch=left});Idle(.6f,true);
                    if(b.Punches!=1||!b.Shield)throw new Exception("Melee cancellation after range failed");
                }
                string line=$"{id}-{hz} bothHands=true speeds=0.65/1.70 intentAndConfirmed=true preReleaseGuard=pass flightGuard=pass pause=pass newRound=pass rangeToMeleeGuard=pass guardHandStep={peakGuard:F4}";report.AppendLine(line);Debug.Log("[RangedCastTransitions] "+line);
            }
            string folder=Path.GetFullPath("../artifacts/ranged-cast-20261009");Directory.CreateDirectory(folder);File.WriteAllText(folder+"/transitions.txt",report.ToString());
        }
    }
}
