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
                hud.Portrait(new Rect(24,12,86,86),false);
                hud.Text(new Rect(111,17,290,30),"迪迦",24,HudPainter.Ink,bold:true);
                hud.Text(new Rect(265,23,160,24),"复合型 · 光之英雄",11,HudPainter.Muted,TextAnchor.MiddleRight);
                for(int i=0;i<15;i++)hud.Box(new Rect(114+i*20,57,16,13),i<battle.Energy?(ready?gold:cyan):new Color(.10f,.21f,.29f));
                hud.Text(new Rect(116,76,270,20),ready?"必杀能量已充满":$"光线能量  {battle.Energy:0} / 15",11,ready?gold:HudPainter.Muted);
                BattlePlate(new Rect(832,18,424,76),new Color(1,.47f,.21f),true);
                hud.Portrait(new Rect(1175,12,84,84),true);
                hud.Text(new Rect(848,18,315,30),"哥尔赞",24,HudPainter.Ink,TextAnchor.MiddleRight,true);
                hud.Box(new Rect(853,55,310,17),new Color(.09f,.13f,.19f));
                float health=Mathf.Clamp01(battle.EnemyHealth/battle.MaxHealth);
                hud.Box(new Rect(853+310*(1-health),56,310*health,14),health<.3f?new Color(1,.28f,.16f):new Color(1,.63f,.23f));
                for(int i=1;i<10;i++)hud.Box(new Rect(853+i*31,56,1,14),new Color(.1f,.07f,.02f,.34f));
                hud.Text(new Rect(856,76,304,20),$"怪兽力量  {Mathf.CeilToInt(battle.EnemyHealth)} / {battle.MaxHealth}",11,HudPainter.Muted,TextAnchor.MiddleRight);
                hud.Text(new Rect(486,22,308,28),"火山大决战",18,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Line(new Vector2(558,60),new Vector2(628,60),cyan,1);
                hud.Dot(new Vector2(640,60),7,gold);hud.Line(new Vector2(652,60),new Vector2(722,60),cyan,1);
                if(battle.Punches>0)
                {
                    float pulse=time<hitUntil?Mathf.Clamp01(hitUntil-time):0;
                    hud.Text(new Rect(37,235,210,77),battle.Punches.ToString("00"),50+(int)(pulse*7),new Color(1,.88f,.52f),bold:true);
                    hud.Text(new Rect(43,306,215,25),"HIT  /  漂亮出击",14,HudPainter.Ink,bold:true);
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
                Guide("双手举高，和迪迦一起出发","transform",cyan,recognizer.TransformProgress);
            }
            else if(transforming)
            {
                hud.Text(new Rect(340,66,600,58),"光之英雄 · 迪迦",38,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(425,611,430,48),"光的力量，准备变身！",27,gold,TextAnchor.MiddleCenter,true);
            }
            else if(victory)
            {
                hud.Text(new Rect(330,67,620,68),"火山守护成功！",43,gold,TextAnchor.MiddleCenter,true);
                for(int i=0;i<3;i++)Star(new Vector2(582+i*58,161),17,Mathf.Clamp01((time-victoryAt-i*.35f)*2));
                hud.Text(new Rect(390,571,500,45),"谢谢你，光之英雄！",28,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(360,631,560,30),"接下来和迪迦合照 · 听引导自动拍照",18,cyan,TextAnchor.MiddleCenter);
            }
            else if(battle.Phase==GamePhase.Paused)
                Guide(paused?"休息一下 · 家长按 Esc 继续":"站进镜头，我们接着守护火山基地","transform",cyan,battle.ResumeProgress/1.2f);
            else if(warning)
                Guide(battle.Shield?"护盾已展开 · 保持住！":"双手放胸前，也可以交叉抱住！","shield",battle.Shield?cyan:gold,recognizer.ShieldProgress);
            else if(ready)
                Guide(recognizer.BeamProgress>0?"看见动作了 · 保持，释放光线！":"摆 L 形，或双手向前推，停一下","beam",gold,recognizer.BeamProgress);
            else if(time<captionUntil||battle.Punches<3)
                Guide(time<captionUntil?caption:"收回拳头，再向前挥出去","punch",cyan,0);
            DrawArcadePreview();
            if(pose?.source=="synthetic")hud.Text(new Rect(20,695,400,20),"合成动作测试 · 非真人输入",11,HudPainter.Gold);
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
            hud.Fade(new Rect(0,620,1280,100),new Color(.005f,.015f,.035f,.12f));
            hud.Rounded(new Rect(346,617,585,78),new Color(.007f,.025f,.053f,.84f),8);
            hud.Box(new Rect(346,632,3,47),accent);
            hud.Figure(new Rect(363,622,51,66),gesture,Time.unscaledTime,accent);
            hud.Text(new Rect(437,627,471,43),title,22,HudPainter.Ink,TextAnchor.MiddleCenter,true);
            hud.Bar(new Rect(453,681,438,3),progress,accent);
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
    }
}
