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
            // Open centre: only a shallow header and the current spoken action overlay the arena.
            for(int i=0;i<8;i++)hud.Box(new Rect(0,i*10,1280,10),new Color(.004f,.014f,.035f,.75f-i*.09f));
            hud.Text(new Rect(28,17,520,36),waiting?"光之英雄 · 城市大决战":"迪迦",waiting?27:23,HudPainter.Ink,bold:true);
            if(!waiting&&!transforming&&!victory)
            {
                hud.Text(new Rect(849,17,400,24),battle.EnemyHealth<=battle.MaxHealth*.3f?"哥尔赞 · 最后的对决":"哥尔赞 · 城市守护战",16,HudPainter.Ink,TextAnchor.MiddleRight);
                hud.Bar(new Rect(853,51,397,9),battle.EnemyHealth/battle.MaxHealth,new Color(1,.46f,.24f));
                hud.Text(new Rect(1020,64,230,24),$"{Mathf.CeilToInt(battle.EnemyHealth)} / {battle.MaxHealth}",12,HudPainter.Muted,TextAnchor.MiddleRight);
                hud.Text(new Rect(28,57,350,28),$"{battle.Punches:00}  HIT",22,cyan,bold:true);
                // A readable fifteen-segment light meter takes the place of the old settings/action cards.
                hud.Text(new Rect(32,593,290,26),ready?"光线准备好了！":"光之能量",18,ready?gold:cyan,bold:true);
                for(int i=0;i<15;i++)hud.Box(new Rect(32+i*15,630,10,22),i<battle.Energy?gold:new Color(.15f,.25f,.38f,.8f));
                hud.Text(new Rect(33,661,264,23),ready?"双手前推，停一下":$"{battle.Energy:0} / 15",14,HudPainter.Muted);
                if(time<hitUntil)
                {
                    float age=1-(hitUntil-time),lift=Mathf.Clamp01(age)*25;
                    hud.Text(new Rect(763,185-lift,250,60),battle.Action==HeroAction.Beam?"光线命中！":battle.Punches%5==0?"漂亮连击！":"命中！",battle.Punches%5==0?34:27,new Color(1,.79f,.39f,Mathf.Clamp01((1-age)*2)),TextAnchor.MiddleCenter,true);
                }
            }
            if(waiting)
            {
                hud.Text(new Rect(355,92,570,38),"城市正在呼唤光之英雄",25,cyan,TextAnchor.MiddleCenter,true);
                Guide("双手举高，和迪迦一起出发", "transform", cyan, recognizer.TransformProgress);
            }
            else if(transforming)
            {
                float age=time-phaseStarted;
                hud.Text(new Rect(340,72,600,48),"光之英雄 · 迪迦",38,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(425,619,430,43),"光的力量，准备变身！",27,gold,TextAnchor.MiddleCenter,true);
                hud.Bar(new Rect(450,674,380,5),age/2.2f,cyan);
            }
            else if(victory)
            {
                hud.Text(new Rect(340,82,600,66),"城市守护成功！",44,gold,TextAnchor.MiddleCenter,true);
                for(int i=0;i<3;i++)Star(new Vector2(570+i*70,184),23,Mathf.Clamp01((time-victoryAt-i*.35f)*2));
                hud.Text(new Rect(398,574,484,40),"谢谢你，光之英雄！",29,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(376,630,528,34),"接下来和迪迦合照 · 听引导自动拍照",20,cyan,TextAnchor.MiddleCenter);
            }
            else if(battle.Phase==GamePhase.Paused)
            {
                Guide(paused?"休息一下 · 家长按 Esc 继续":"站进镜头，我们接着保护城市", "transform", cyan,battle.ResumeProgress/1.2f);
            }
            else if(warning)
                Guide("双手护住胸前，挡住怪兽！","shield",gold,1-battle.EnemyAge/(battle.Enemy==EnemyPhase.Windup?battle.WarningDuration:Battle.EnemyAttackSeconds));
            else if(ready)
                Guide("双手向前推，停一下！","beam",gold,recognizer.BeamProgress);
            else if(time<captionUntil||battle.Punches<3)
                Guide(time<captionUntil?caption:"收回拳头，再向前挥出去","punch",cyan,0);
            DrawArcadePreview();
            if(pose?.source=="synthetic")hud.Text(new Rect(20,695,400,20),"合成动作测试 · 非真人输入",11,HudPainter.Gold);
        }
        void Guide(string title,string gesture,Color accent,float progress)
        {
            hud.Rounded(new Rect(336,609,608,88),new Color(.012f,.035f,.075f,.8f),20);
            hud.Figure(new Rect(356,617,62,72),gesture,Time.unscaledTime,accent);
            hud.Text(new Rect(441,620,483,49),title,25,HudPainter.Ink,TextAnchor.MiddleCenter,true);
            hud.Bar(new Rect(454,683,452,4),progress,accent);
        }
        void DrawArcadePreview()
        {
            var r=new Rect(1085,550,175,131.25f);
            hud.Rounded(new Rect(r.x-4,r.y-23,r.width+8,r.height+49),new Color(.015f,.04f,.075f,.88f),10);
            hud.Text(new Rect(r.x,r.y-23,r.width,22),"镜像取景",11,HudPainter.Cyan);
            if(previewTexture&&previewFrame!=null&&previewFrame.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                GUI.DrawTexture(r,previewTexture,ScaleMode.ScaleToFit);
            else hud.Text(r,"镜头准备中",14,HudPainter.Gold,TextAnchor.MiddleCenter);
            hud.Text(new Rect(r.x-1,r.yMax+2,r.width+2,21),Time.unscaledTime<gestureFeedbackUntil?gestureFeedback:PoseQuality.Present(pose,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())?"看见你了":"请站进镜头",11,HudPainter.Cyan,TextAnchor.MiddleCenter);
        }
        void Star(Vector2 center,float radius,float opacity)
        {
            var color=new Color(1,.79f,.37f,opacity);
            for(int i=0;i<10;i++)
            {
                float a=(i*36-90)*Mathf.Deg2Rad,b=((i+1)*36-90)*Mathf.Deg2Rad;
                hud.Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(i%2==0?1:.45f),
                    center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius*(i%2==1?1:.45f),color,4);
            }
        }
    }
}
