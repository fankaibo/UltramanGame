using System;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed partial class ArenaController
    {
        void DrawArcadeHud()
        {
            float time=Time.unscaledTime;
            bool waiting=battle.Phase==GamePhase.Waiting,transforming=battle.Phase==GamePhase.Transforming;
            bool victory=battle.Phase==GamePhase.Victory,ready=battle.Energy>=Battle.MaxEnergy;
            bool warning=battle.Enemy==EnemyPhase.Windup||battle.Enemy==EnemyPhase.Attack;
            var gold=HudPainter.Gold;var cyan=HudPainter.Cyan;
            bool opening=battle.Phase==GamePhase.Battle&&battleStartCueAt>=0&&time-battleStartCueAt<2.1f;
            if(!transforming)hud.Fade(new Rect(0,0,1280,145),new Color(.004f,.014f,.035f,.87f));
            if(!waiting&&!transforming&&!victory)
            {
                BattlePlate(new Rect(24,18,424,76),cyan,false);
                hud.HeroPortrait(new Rect(24,12,86,86),SelectedHero.Id);
                hud.Text(new Rect(111,17,290,30),SelectedHero.Name,24,HudPainter.Ink,bold:true);
                hud.Text(new Rect(265,23,160,24),SelectedHero.Form+" · 光之英雄",11,HudPainter.Muted,TextAnchor.MiddleRight);
                for(int i=0;i<15;i++)hud.Box(new Rect(114+i*20,57,16,13),i<battle.Energy?(ready?gold:cyan):new Color(.10f,.21f,.29f));
                hud.Text(new Rect(116,76,270,20),ready?"必杀能量已充满":$"光线能量  {battle.Energy:0} / 15",11,ready?gold:HudPainter.Muted);
                BattlePlate(new Rect(832,18,424,76),new Color(1,.47f,.21f),true);
                hud.Portrait(new Rect(1175,12,84,84),true);
                hud.Text(new Rect(848,18,315,30),"哥尔赞",24,HudPainter.Ink,TextAnchor.MiddleRight,true);
                hud.Box(new Rect(853,55,310,17),new Color(.09f,.13f,.19f));
                float health=Mathf.Clamp01(battle.EnemyHealth/battle.MaxHealth);
                float healthGhost=Mathf.Clamp01(enemyHealthDisplay/battle.MaxHealth);
                if(healthGhost>health+.001f)
                    hud.Box(new Rect(853+310*(1-healthGhost),56,310*(healthGhost-health),14),new Color(1,.84f,.35f,.78f));
                hud.Box(new Rect(853+310*(1-health),56,310*health,14),health<.3f?new Color(1,.28f,.16f):new Color(1,.63f,.23f));
                for(int i=1;i<10;i++)hud.Box(new Rect(853+i*31,56,1,14),new Color(.1f,.07f,.02f,.34f));
                hud.Text(new Rect(856,76,304,20),$"怪兽力量  {Mathf.CeilToInt(battle.EnemyHealth)} / {battle.MaxHealth}",11,HudPainter.Muted,TextAnchor.MiddleRight);
                if(!opening)
                {
                    hud.Text(new Rect(486,22,308,28),"火山大决战",18,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                    hud.Line(new Vector2(558,60),new Vector2(628,60),cyan,1);
                    hud.Dot(new Vector2(640,60),7,gold);hud.Line(new Vector2(652,60),new Vector2(722,60),cyan,1);
                }
                float scorePulse=time<hitUntil?Mathf.Clamp01((hitUntil-time)*1.4f):0;
                if(battle.Action!=HeroAction.Beam&&!opening)
                {
                    // The monster leans through the centre in both rush and
                    // recoil. Keep the score below the hero plate, off its face.
                    hud.Rounded(new Rect(36,110,244,27),new Color(.01f,.04f,.08f,.86f),6);
                    hud.Text(new Rect(42,110,232,27),$"SCORE  {ArcadeScore():000000}",13+(int)(scorePulse*2),new Color(1,.82f,.42f),TextAnchor.MiddleCenter,true);
                }
                if(battle.Punches>0)
                {
                    float pulse=time<hitUntil?Mathf.Clamp01(hitUntil-time):0;
                    hud.Text(new Rect(37,235,210,77),battle.Punches.ToString("00"),50+(int)(pulse*7),new Color(1,.88f,.52f),bold:true);
                    bool comboActive=comboCount>=2&&time<comboUntil;
                    hud.Text(new Rect(43,306,215,25),comboActive?$"HIT  /  {comboCount:00} COMBO":"HIT  /  漂亮出击",14,comboActive?gold:HudPainter.Ink,bold:true);
                    if(comboActive)
                    {
                        float comboAge=Mathf.Clamp01((comboUntil-time)/.65f);
                        hud.Text(new Rect(39,337,230,30),$"{comboCount:00} 连击",20+Mathf.RoundToInt(comboAge*4),new Color(1,.78f,.30f,Mathf.Clamp01(comboAge)),TextAnchor.MiddleLeft,true);
                    }
                    hud.Line(new Vector2(42,335),new Vector2(121,335),gold,2);

                    // The reference cabinet keeps its score at the edge of the playfield.
                    // Five quiet stars make the combo feel earned without covering either actor.
                    hud.Text(new Rect(1190,198,60,20),"评分",10,HudPainter.Muted,TextAnchor.MiddleCenter);
                    for(int i=0;i<5;i++)
                    {
                        float earned=Mathf.Clamp01((battle.Punches-i*3)/3f);
                        Star(new Vector2(1220,236+i*38),11,.16f+.84f*earned);
                    }
                }
                if(time<hitUntil&&battle.Action!=HeroAction.Beam)
                {
                    // Keep praise beside the combo counter: the monster's
                    // face and contact area must stay clear in the closer lens.
                    float age=1-(hitUntil-time),lift=Mathf.Clamp01(age)*12;
                    hud.Text(new Rect(42,388-lift,244,40),battle.Punches%5==0?"超棒连击！":"漂亮！",battle.Punches%5==0?25:22,new Color(1,.86f,.48f,Mathf.Clamp01((1-age)*2)),TextAnchor.MiddleLeft,true);
                }
                if(lastDamage>0&&time-damagePopAt<.9f)
                {
                    float age=Mathf.Clamp01((time-damagePopAt)/.9f),lift=Mathf.SmoothStep(0,1,age)*6;
                    float alpha=1-Mathf.SmoothStep(0,1,age);
                    bool special=lastDamage>1;
                    hud.Text(new Rect(1020,107-lift,144,31),"−"+lastDamage.ToString("00"),special?28:24,new Color(1,special?.30f:.66f,.18f,alpha),TextAnchor.MiddleRight,true);
                }
                if(warning)
                {
                    float opacity=battle.Shield?.7f:.3f+Mathf.Sin(time*4)*.1f;
                    hud.Box(new Rect(0,130,4,420),new Color(1,.51f,.22f,opacity));
                    hud.Box(new Rect(1276,130,4,420),new Color(1,.51f,.22f,opacity));
                    string warningText=battle.Enemy==EnemyPhase.Attack?
                        (battle.Shield?"挡住它 · 保持护盾！":"怪兽冲过来了！"):
                        (battle.Shield?"护盾准备好了":"怪兽正在蓄力");
                    hud.Text(new Rect(855,139,335,36),warningText,20,battle.Shield?cyan:gold,TextAnchor.MiddleRight,true);
                }
            }
            if(waiting)
            {
                hud.Text(new Rect(345,53,590,67),"光之英雄 · 火山大决战",37,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(415,120,450,32),"这一次，由你守护火山基地",20,cyan,TextAnchor.MiddleCenter);
                DrawHeroSelection();
                Guide(keyboard?"按空格，和"+SelectedHero.Name+"一起出发":"双手举高，和"+SelectedHero.Name+"一起出发","transform",cyan,keyboard?0:recognizer.TransformProgress);
            }
            else if(transforming)
            {
                hud.Fade(new Rect(20,128,290,170),new Color(.006f,.023f,.048f,.78f));
                hud.Line(new Vector2(38,154),new Vector2(91,154),cyan,2);
                hud.Text(new Rect(38,166,252,25),"光之英雄",16,cyan,bold:true);
                hud.Text(new Rect(38,195,272,47),SelectedHero.Name,34,HudPainter.Ink,bold:true);
                hud.Text(new Rect(38,250,272,28),"光的力量，正在觉醒",16,gold);
            }
            else if(victory)
            {
                float age=time-victoryAt,alpha=Mathf.SmoothStep(0,1,(age-.85f)/.45f);
                var winColor=new Color(gold.r,gold.g,gold.b,alpha);
                hud.Text(new Rect(38,78,325,62),"守护成功！",35,winColor,TextAnchor.MiddleLeft,true);
                for(int i=0;i<3;i++)Star(new Vector2(65+i*53,164),15,Mathf.Clamp01((age-1.1f-i*.25f)*2));
                hud.Text(new Rect(41,213,285,30),$"本局得分  {ArcadeScore():000000}",18,winColor,TextAnchor.MiddleLeft,true);
                if(age>3)
                {
                    hud.Rounded(new Rect(32,264,292,76),new Color(.007f,.025f,.053f,.82f),8);
                    string next=keyboard?(photoAvailable?"按 F7 合照，或按 R 再玩一次":"按 R，再守护一次火山基地"):
                        "接下来，和"+SelectedHero.Name+"合照";
                    hud.Text(new Rect(46,272,266,60),"谢谢你！\n"+next,18,HudPainter.Ink,TextAnchor.MiddleLeft,true);
                }
            }
            else if(battle.Phase==GamePhase.Paused)
            {
                bool interrupted=!keyboard&&!PoseQuality.Fresh(pose,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                string text=paused?"休息一下 · 按 Esc 继续":keyboard?"准备好了，马上继续战斗":
                    interrupted?"相机连接中断 · 进度已保留":"站进镜头，我们接着守护火山基地";
                Guide(text,"transform",cyan,battle.ResumeProgress/1.2f);
            }
            else if(battle.Action!=HeroAction.Hurt)
            {
                // The fall and recovery need the full lower part of the shot;
                // resume the normal instructions only after the hero stands.
                if(battle.Action==HeroAction.Beam)DrawBeamReleaseTitle();
                else if(warning)
                    Guide(battle.Shield?(keyboard?"护盾已展开 · 继续按住 S":"护盾已展开 · 保持住！"):
                        keyboard?"按住 S，展开护盾！":"双手放胸前，也可以交叉抱住！","shield",battle.Shield?cyan:gold,keyboard?(battle.Shield?1:0):recognizer.ShieldProgress);
                else if(ready)
                    Guide(keyboard?"能量已满 · 按 J 释放光线":recognizer.BeamProgress>0?"看见动作了 · 保持，释放光线！":"摆 L 形，或双手向前推，停一下","beam",gold,keyboard?1:recognizer.BeamProgress);
                else if(time<captionUntil||battle.Punches<3)
                    Guide(keyboard?"交替按 A / D，挥拳出击！":time<captionUntil?caption:"收回拳头，再向前挥出去","punch",cyan,0);
            }
            DrawBattleStartCue();
            DrawArcadePreview();
            if(keyboard)hud.Text(new Rect(20,649,332,20),"按键练习 · A/D 挥拳 · S 防御 · J 光线",11,HudPainter.Muted);
            if(pose?.source=="synthetic")hud.Text(new Rect(20,695,400,20),"合成动作测试 · 非真人输入",11,HudPainter.Gold);
        }

        void DrawKeyboardActions()
        {
            if(!keyboard||world.Closeup.Active||battle.Phase==GamePhase.Waiting||battle.Phase==GamePhase.Transforming)return;
            // Parent controls stay at the edge. Switching input modes must not
            // bring back the old panels over the fighters and their footwork.
            if(battle.Phase==GamePhase.Victory)
            {
                if(photoAvailable&&hud.Button(new Rect(930,674,158,30),"合照 · F7",HudPainter.Cyan,13))photo.Open();
            }
            else if(hud.Button(new Rect(930,674,158,30),paused?"继续 · Esc":"暂停 · Esc",HudPainter.Muted,13))paused=!paused;
            if(hud.Button(new Rect(1100,674,156,30),"再来一局 · R",HudPainter.Muted,13))Restart();
        }

        void DrawBeamReleaseTitle()
        {
            // Keep the forearm, flight path and monster's reaction unobstructed
            // when handing the closeup back to the two-fighter battle shot.
            float alpha=1-Mathf.SmoothStep(0,1,(battle.ActionAge-1.25f)/.25f);
            hud.Rounded(new Rect(406,630,468,42),new Color(.006f,.02f,.055f,.80f*alpha),8);
            hud.Line(new Vector2(424,668),new Vector2(856,668),new Color(.22f,.72f,1,.65f*alpha),2);
            hud.Text(new Rect(420,633,440,34),SelectedHero.Beam+"！",22,new Color(.77f,.94f,1,alpha),TextAnchor.MiddleCenter,true);
        }

        // A short cabinet-style cut-in makes the transition out of the
        // transformation readable on a TV. It fades before the first enemy
        // warning so the actors and the camera remain the focus of the round.
        void DrawBattleStartCue()
        {
            if(battle.Phase!=GamePhase.Battle)return;
            float age=Time.unscaledTime-battleStartCueAt;
            if(battleStartCueAt<0||age<0||age>2.1f)return;
            float intro=Mathf.SmoothStep(.42f,1f,Mathf.Clamp01(age/.16f));
            float outro=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.28f)/.82f));
            float alpha=Mathf.Clamp01(intro*outro);
            float scale=Mathf.Lerp(.88f,1f,intro);
            float width=330*scale,height=60*scale,x=(1280-width)/2,y=3;
            var cyan=new Color(.22f,.84f,1,alpha*.92f);
            var gold=new Color(1,.70f,.25f,alpha*.92f);
            hud.Rounded(new Rect(x,y,width,height),new Color(.008f,.035f,.075f,alpha*.95f),8);
            hud.Line(new Vector2(x+8,y+16),new Vector2(x+50,y+16),cyan,2);
            hud.Line(new Vector2(x+width-50,y+16),new Vector2(x+width-8,y+16),gold,2);
            hud.Text(new Rect(x+18,y+5,width-36,20),$"{SelectedHero.Name}  VS  哥尔赞",12,new Color(.74f,.88f,1,alpha),TextAnchor.MiddleCenter,true);
            hud.Text(new Rect(x+18,y+24,width-36,34),"开战！",25,new Color(1,.88f,.53f,alpha),TextAnchor.MiddleCenter,true);
        }
        void BattlePlate(Rect r,Color accent,bool right)
        {
            hud.Fade(r,new Color(.025f,.075f,.125f,.9f));
            hud.Line(new Vector2(r.x+12,r.y),new Vector2(r.xMax-12,r.y),accent,2);
            hud.Line(new Vector2(r.x+12,r.y),new Vector2(r.x,r.y+12),accent,2);
            hud.Line(new Vector2(r.xMax-12,r.y),new Vector2(r.xMax,r.y+12),accent,2);
            hud.Box(new Rect(right?r.x:r.xMax-2,r.y+14,2,r.height-18),new Color(accent.r,accent.g,accent.b,.35f));
        }
        void Guide(string title,string gesture,Color accent,float progress)
        {
            // Keep the playfield dominant, as on the reference cabinet: the
            // instruction is a compact rail at the very bottom instead of a
            // large card covering the fighters' legs and effects.
            hud.Fade(new Rect(0,640,1280,80),new Color(.005f,.015f,.035f,.10f));
            hud.Rounded(new Rect(370,638,540,65),new Color(.007f,.025f,.053f,.82f),8);
            hud.Box(new Rect(370,650,3,41),accent);
            hud.Figure(new Rect(385,645,43,54),gesture,Time.unscaledTime,accent);
            hud.Text(new Rect(438,645,440,36),title,18,HudPainter.Ink,TextAnchor.MiddleCenter,true);
            hud.Bar(new Rect(455,690,410,3),progress,accent);
        }
        void DrawArcadePreview()
        {
            if(keyboard)return;
            if(!showPreview)
            {if(hud.Button(new Rect(1100,653,156,30),"显示取景 · F3",HudPainter.Cyan,12))showPreview=true;return;}
            var r=new Rect(1100,566,156,117);
            hud.Rounded(new Rect(r.x-4,r.y-18,r.width+8,r.height+40),new Color(.01f,.025f,.05f,.84f),6);
            hud.Text(new Rect(r.x,r.y-18,r.width-48,17),"镜像取景",10,HudPainter.Cyan);
            if(hud.Button(new Rect(r.xMax-43,r.y-18,43,17),"收起",HudPainter.Muted,10))showPreview=false;
            if(previewTexture&&previewFrame!=null&&previewFrame.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))GUI.DrawTexture(r,previewTexture,ScaleMode.ScaleToFit);
            else hud.Text(r,"镜头准备中",12,HudPainter.Gold,TextAnchor.MiddleCenter);
            hud.Text(new Rect(r.x,r.yMax+1,r.width,18),Time.unscaledTime<gestureFeedbackUntil?gestureFeedback:PoseQuality.Present(pose,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())?"看见你了":"请站进镜头",10,HudPainter.Cyan,TextAnchor.MiddleCenter);
        }
        void Star(Vector2 center,float radius,float opacity)
        {
            var color=new Color(1,.79f,.37f,opacity);
            for(int i=0;i<10;i++)
            {
                float a=(i*36-90)*Mathf.Deg2Rad,b=((i+1)*36-90)*Mathf.Deg2Rad;
                hud.Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(i%2==0?1:.45f),center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius*(i%2==1?1:.45f),color,3);
            }
        }
        int ArcadeScore()
        {
            int score=battle.Punches*100+battle.Blocks*250-battle.HitsTaken*50;
            if(battle.Phase==GamePhase.Victory)score+=1000;
            return Mathf.Max(0,score);
        }
    }
}
