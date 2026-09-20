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
            hud.Fade(new Rect(0,0,1280,145),new Color(.004f,.014f,.035f,.87f));
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
                hud.Text(new Rect(486,22,308,28),"火山大决战",18,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Line(new Vector2(558,60),new Vector2(628,60),cyan,1);
                hud.Dot(new Vector2(640,60),7,gold);hud.Line(new Vector2(652,60),new Vector2(722,60),cyan,1);
                float scorePulse=time<hitUntil?Mathf.Clamp01((hitUntil-time)*1.4f):0;
                hud.Rounded(new Rect(518,101,244,27),new Color(.01f,.04f,.08f,.86f),6);
                hud.Text(new Rect(524,101,232,27),$"SCORE  {ArcadeScore():000000}",13+(int)(scorePulse*2),new Color(1,.82f,.42f),TextAnchor.MiddleCenter,true);
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
                if(time<hitUntil)
                {
                    float age=1-(hitUntil-time),lift=Mathf.Clamp01(age)*32;
                    hud.Text(new Rect(800,213-lift,295,62),battle.Action==HeroAction.Beam?"光线爆发！":battle.Punches%5==0?"超棒连击！":"漂亮！",battle.Punches%5==0?35:29,new Color(1,.86f,.48f,Mathf.Clamp01((1-age)*2)),TextAnchor.MiddleCenter,true);
                }
                if(lastDamage>0&&time-damagePopAt<.9f)
                {
                    float age=Mathf.Clamp01((time-damagePopAt)/.9f),lift=Mathf.SmoothStep(0,1,age)*68;
                    float alpha=1-Mathf.SmoothStep(0,1,age);
                    bool special=lastDamage>1;
                    hud.Text(new Rect(924,228-lift,260,56),"−"+lastDamage.ToString("00"),42+(int)((1-age)*8),new Color(1,special?.30f:.66f,.18f,alpha),TextAnchor.MiddleCenter,true);
                    if(special)hud.Text(new Rect(924,276-lift,260,25),"CRITICAL",15,new Color(1,.78f,.28f,alpha),TextAnchor.MiddleCenter,true);
                }
                if(warning)
                {
                    float opacity=battle.Shield?.7f:.3f+Mathf.Sin(time*4)*.1f;
                    hud.Box(new Rect(0,130,4,420),new Color(1,.51f,.22f,opacity));
                    hud.Box(new Rect(1276,130,4,420),new Color(1,.51f,.22f,opacity));
                    hud.Text(new Rect(855,139,335,36),battle.Shield?"防御成功准备":"怪兽正在蓄力",20,battle.Shield?cyan:gold,TextAnchor.MiddleRight,true);
                }
            }
            if(waiting)
            {
                hud.Text(new Rect(345,53,590,67),"光之英雄 · 火山大决战",37,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(415,120,450,32),"这一次，由你守护火山基地",20,cyan,TextAnchor.MiddleCenter);
                DrawHeroSelection();
                Guide(keyboard?"按空格，和"+SelectedHero.Name+"一起出发":"双手举高，和"+SelectedHero.Name+"一起出发","transform",cyan,recognizer.TransformProgress);
            }
            else if(transforming)
            {
                hud.Text(new Rect(340,66,600,58),"光之英雄 · "+SelectedHero.Name,38,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(425,611,430,48),"光的力量，准备变身！",27,gold,TextAnchor.MiddleCenter,true);
            }
            else if(victory)
            {
                hud.Text(new Rect(330,67,620,68),"火山守护成功！",43,gold,TextAnchor.MiddleCenter,true);
                for(int i=0;i<3;i++)Star(new Vector2(582+i*58,161),17,Mathf.Clamp01((time-victoryAt-i*.35f)*2));
                hud.Text(new Rect(445,505,390,30),$"本局得分  {ArcadeScore():000000}",19,gold,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(390,571,500,45),"谢谢你，光之英雄！",28,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(360,631,560,30),"接下来和"+SelectedHero.Name+"合照 · 听引导自动拍照",18,cyan,TextAnchor.MiddleCenter);
            }
            else if(battle.Phase==GamePhase.Paused)
                Guide(paused?"休息一下 · 家长按 Esc 继续":"站进镜头，我们接着守护火山基地","transform",cyan,battle.ResumeProgress/1.2f);
            else if(warning)
                Guide(battle.Shield?"护盾已展开 · 保持住！":"双手放胸前，也可以交叉抱住！","shield",battle.Shield?cyan:gold,recognizer.ShieldProgress);
            else if(ready)
                Guide(recognizer.BeamProgress>0?"看见动作了 · 保持，释放光线！":"摆 L 形，或双手向前推，停一下","beam",gold,recognizer.BeamProgress);
            else if(time<captionUntil||battle.Punches<3)
                Guide(time<captionUntil?caption:"收回拳头，再向前挥出去","punch",cyan,0);
            DrawBattleStartCue();
            DrawArcadePreview();
            if(pose?.source=="synthetic")hud.Text(new Rect(20,695,400,20),"合成动作测试 · 非真人输入",11,HudPainter.Gold);
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
            float width=620*scale,height=98*scale,x=(1280-width)/2,y=286+(1-scale)*48;
            var cyan=new Color(.22f,.84f,1,alpha*.92f);
            var gold=new Color(1,.70f,.25f,alpha*.92f);
            hud.Rounded(new Rect(x+8,y+8,width,height),new Color(0,0,0,alpha*.35f),12);
            hud.Rounded(new Rect(x,y,width,height),new Color(.008f,.035f,.075f,alpha*.90f),12);
            hud.Line(new Vector2(x-54,y+18),new Vector2(x+22,y+18),cyan,3);
            hud.Line(new Vector2(x+width-22,y+18),new Vector2(x+width+54,y+18),gold,3);
            hud.Line(new Vector2(x-28,y+height-17),new Vector2(x+66,y+height-17),gold,2);
            hud.Line(new Vector2(x+width-66,y+height-17),new Vector2(x+width+28,y+height-17),cyan,2);
            hud.Text(new Rect(x+36,y+9,width-72,25),$"{SelectedHero.Name}  VS  哥尔赞",14,new Color(.74f,.88f,1,alpha),TextAnchor.MiddleCenter,true);
            hud.Text(new Rect(x+36,y+30,width-72,53),"开战！",39,new Color(1,.88f,.53f,alpha),TextAnchor.MiddleCenter,true);
            hud.Text(new Rect(x+width-112,y+36,76,22),"FIGHT!",12,new Color(.39f,.86f,1,alpha),TextAnchor.MiddleCenter,true);
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
            var r=new Rect(1100,566,156,117);
            hud.Rounded(new Rect(r.x-4,r.y-18,r.width+8,r.height+40),new Color(.01f,.025f,.05f,.84f),6);
            hud.Text(new Rect(r.x,r.y-18,r.width,17),"镜像取景",10,HudPainter.Cyan);
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
