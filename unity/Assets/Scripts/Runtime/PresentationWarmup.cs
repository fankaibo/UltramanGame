using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public static class PresentationWarmup
    {
        // Warmup is a startup cache, not a hidden loading screen. Keep a hard
        // render budget so a first-run shader compile cannot hold the child at
        // the title screen indefinitely; the live battle still renders every
        // subsequent pose normally.
        // A first Metal render can block for shader compilation on a cold
        // machine. Keep the title screen responsive and let the live camera
        // perform that compile on its normal frame budget.
        const int MaxWarmupRenders=0;
        // Exercise the actual skins, fade and dynamic-light passes before play.
        // This never consumes player input or plays sound, and restores the waiting pose.
        public static void Run(GameWorld world,AnimatedActor hero,AnimatedActor enemy)
        {
            if(MaxWarmupRenders==0)
            {
                if(Debug.isDebugBuild)Debug.Log("[PresentationWarmup] skipped render warmup; live frame path remains enabled");
                return;
            }
            var camera=world.Camera;var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(256,144,24,RenderTextureFormat.DefaultHDR){antiAliasing=4};target.Create();
            var state=new Battle();float time=0;int rendered=0;
            void Draw(float dt)
            {
                time+=dt;hero.Update(state,camera,dt,time);enemy.Update(state,camera,dt,time);world.Tick(state,dt,time);
                enemy.SetPresentationOpacity(world.EnemyOpacity);
                if(rendered<MaxWarmupRenders){camera.Render();rendered++;}
            }
            try
            {
                camera.targetTexture=target;
                state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});world.Cue(GameCue.Transform);Draw(.05f);
                for(int i=0;i<(Battle.TransformationSeconds+.2f)/.1f;i++)state.Tick(.1f,new PlayerInput{Tracking=true});Draw(.05f);
                state.Tick(.01f,new PlayerInput{Tracking=true,Shield=true});world.Cue(GameCue.Block);Draw(.02f);
                // A hand ribbon needs multiple sampled points to create visible
                // geometry; skipping straight to the end never warms its shader.
                for(int side=0;side<2;side++)
                    for(int frame=0;frame<30;frame++)
                    {
                        state.Tick(1/60f,new PlayerInput{Tracking=true,LeftPunch=side==0&&frame==0,RightPunch=side==1&&frame==0});
                        Draw(1/60f);
                    }
                for(int punch=0;punch<15;punch++)
                {state.Tick(.01f,new PlayerInput{Tracking=true,LeftPunch=true});for(int j=0;j<5;j++)state.Tick(.1f,new PlayerInput{Tracking=true});}
                state.Tick(.01f,new PlayerInput{Tracking=true,Beam=true});world.Cue(GameCue.Beam);Draw(.04f);Draw(.05f);
                for(int i=0;i<12;i++)Draw(.1f);
                for(int i=0;i<3;i++)state.Tick(.1f,new PlayerInput{Tracking=true});world.Hit(true,state);Draw(.12f);
                world.Cue(GameCue.EnemyAttack);world.Cue(GameCue.Hurt);Draw(.02f);
                world.WarmDefeatImpact();
                if(rendered<MaxWarmupRenders){camera.Render();rendered++;}
            }
            finally
            {
                world.ResetPresentation();state=new Battle();hero.Update(state,camera,0,0);enemy.Update(state,camera,0,0);world.Tick(state,1,0);
                camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);
            }
            if(Debug.isDebugBuild)Debug.Log($"[PresentationWarmup] complete renders={rendered}/{MaxWarmupRenders}; player round untouched");
        }
    }
}
