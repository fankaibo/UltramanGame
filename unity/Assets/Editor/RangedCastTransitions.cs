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
            bool liveBody=Array.IndexOf(Environment.GetCommandLineArgs(),"--live-body")>=0;
            bool liveSupport=liveBody||Array.IndexOf(Environment.GetCommandLineArgs(),"--live-support")>=0;
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
                string handNote="";Battle b=null;float dt=1f/hz,time=0,health=200,handStep=0;Vector3 oldLeft=Vector3.zero,oldRight=Vector3.zero;
                float footStep=0,peakGuardFoot=0;var oldLeftFoot=hero.FootPosition(true);var oldRightFoot=hero.FootPosition(false);
                int sequence=0;float peakSupport=0,peakBody=0;
                void Observe(PlayerInput input)
                {
                    if(!liveSupport)return;
                    var points=new PosePoint[33];for(int i=0;i<33;i++)points[i]=new PosePoint(.5f,.5f);
                    points[11]=new PosePoint(.65f,.3f);points[12]=new PosePoint(.35f,.3f);
                    points[15]=new PosePoint(.72f,.12f);points[16]=new PosePoint(.28f,.12f);
                    if(liveBody)
                    {
                        points[23]=new PosePoint(.60f,.74f);points[24]=new PosePoint(.40f,.74f);
                        foreach(int index in new[]{0,11,12,13,14,15,16})points[index].x+=.11f;
                        points[11].y+=.03f;points[12].y-=.03f;
                    }
                    var frame=new PoseFrame{schema=1,streamId="live-support-transition",sequence=++sequence,capturedMs=100000+Mathf.RoundToInt(time*1000),tracked=true,points=points};
                    hero.ObserveReadyPose(frame,frame.capturedMs,input,b,dt,true);
                }
                void Draw(float step)
                {hero.Update(b,world.Camera,step,time);enemy.Update(b,world.Camera,step,time);world.Tick(b,step,time);}
                void Step(PlayerInput input)
                {
                    time+=dt;b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    Observe(input);
                    hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);if(b.EnemyHealth<health)world.Hit(b.Action==HeroAction.Beam,b);health=b.EnemyHealth;world.Tick(b,dt,time);
                    peakSupport=Mathf.Max(peakSupport,hero.SupportLift(true),hero.SupportLift(false));
                    peakBody=Mathf.Max(peakBody,Mathf.Abs(hero.RangedBodySide));
                    var lf=hero.FootPosition(true);var rf=hero.FootPosition(false);
                    footStep=Mathf.Max(footStep,Vector3.Distance(lf,oldLeftFoot),Vector3.Distance(rf,oldRightFoot));oldLeftFoot=lf;oldRightFoot=rf;
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
                        handStep=footStep=0;
                        if(intentFirst)for(int f=0;f<Mathf.CeilToInt(.12f/dt);f++)Step(new PlayerInput{Tracking=true,GuardIntent=true});
                        Idle(.8f,true);
                        peakGuard=Mathf.Max(peakGuard,handStep);
                        peakGuardFoot=Mathf.Max(peakGuardFoot,footStep);
                        if(id=="Tiga"&&hz==60&&footStep>.20f)throw new Exception($"Guard boot jump {left}/{speed}/{released}: {footStep}");
                        if(!b.Shield||b.Punches!=(released?1:0)||world.Projectile.Impacts!=(released?1:0))throw new Exception($"Guard takeover {id}-{hz} left={left} speed={speed} released={released}: shield={b.Shield} punches={b.Punches} impacts={world.Projectile.Impacts} action={b.Action}");
                        if(hz==60&&handStep>.65f)throw new Exception($"Guard hand jump {id}-{hz} left={left} speed={speed} released={released}: {handStep} {handNote}");
                    }
                    Start();Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,RangedAttack=true,AttackSpeed=speed});while(!b.Shot.Active)Step(new PlayerInput{Tracking=true});
                    b.Pause();Draw(0);var frozen=hero.HandPosition;var frozenRoot=hero.Root.position;var frozenLeftFoot=hero.FootPosition(true);var frozenRightFoot=hero.FootPosition(false);for(int i=0;i<5;i++)Draw(0);
                    if(Vector3.Distance(frozen,hero.HandPosition)>.001f||Vector3.Distance(frozenRoot,hero.Root.position)>.001f||world.Projectile.Visible)throw new Exception("Paused cast accumulates");
                    if(Vector3.Distance(frozenLeftFoot,hero.FootPosition(true))>.001f||Vector3.Distance(frozenRightFoot,hero.FootPosition(false))>.001f)throw new Exception("Paused boots accumulate");
                    Start();if(world.Projectile.Visible||world.Projectile.LaunchVisible)throw new Exception("New round retained cast");
                    Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,RangedAttack=true,AttackSpeed=speed});Idle(.8f);
                    Step(new PlayerInput{Tracking=true,LeftPunch=!left,RightPunch=left});Idle(.6f,true);
                    if(b.Punches!=1||!b.Shield)throw new Exception("Melee cancellation after range failed");
                }
                if(liveBody&&(id=="Tiga"?peakBody<.20f:peakBody!=0))throw new Exception("Ranged torso ownership not verified: "+id+"/"+peakBody);
                if(liveSupport&&peakSupport<.3f)throw new Exception("Live support fixture did not move support arm");
                string line=$"{id}-{hz} bothHands=true speeds=0.65/1.70 intentAndConfirmed=true preReleaseGuard=pass flightGuard=pass pause=pass newRound=pass rangeToMeleeGuard=pass guardHandStep={peakGuard:F4} guardFootStep={peakGuardFoot:F4} liveSupport={liveSupport} peakSupport={peakSupport:F4} liveBody={liveBody} peakBody={peakBody:F4}";report.AppendLine(line);Debug.Log("[RangedCastTransitions] "+line);
            }
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--ranged-cast-output");
            string folder=Path.GetFullPath(at>=0?args[at+1]:"../artifacts/ranged-cast-20261009");Directory.CreateDirectory(folder);File.WriteAllText(folder+"/transitions.txt",report.ToString());
        }
    }
}
