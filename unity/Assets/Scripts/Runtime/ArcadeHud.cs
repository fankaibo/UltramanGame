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
            bool opening=battle.Phase==GamePhase.Battle&&battleStartCueAt>=0&&time-battleStartCueAt<2.8f;
            bool cinematic=battle.Action==HeroAction.Beam;
            DrawArcadeRails(time,cinematic,warning,victory,transforming);
            DrawMotionBursts(time,cinematic,warning,victory,transforming);
            if(waiting||victory)hud.Fade(new Rect(0,0,1280,145),new Color(.004f,.014f,.035f,.87f));
            if(!waiting&&!transforming&&!victory)
            {
                DrawBattleHeader(ready,opening,cinematic);
                if(cinematic)DrawFinisherCallout(time);
                if(battle.Punches>0&&!cinematic)
                {
                    float pulse=time<hitUntil?Mathf.Clamp01(hitUntil-time):0;
                    bool comboActive=comboCount>=2&&time<comboUntil;
                    // One counter changes meaning with the active string. Total
                    // hits and the same combo no longer occupy three rows.
                    // Keep the score cue legible at TV distance without
                    // letting this side rail compete with the two fighters.
                    // Side counters are secondary cabinet feedback. Keep them
                    // readable from a TV, but let the fighters and the actual
                    // contact flash remain the largest visual elements.
                    hud.Text(new Rect(32,244,122,38),(comboActive?comboCount:battle.Punches).ToString("00"),22+(int)(pulse*2),new Color(1,.88f,.52f),bold:true);
                    hud.Text(new Rect(34,282,122,15),comboActive?"连击":"命中",10,comboActive?gold:HudPainter.Muted,bold:true);
                    hud.Line(new Vector2(34,307),new Vector2(82,307),gold,1);
                    for(int i=0;i<5;i++)
                    {
                        float earned=Mathf.Clamp01((battle.Punches-i*3)/3f);
                        Star(new Vector2(1240,242+i*22),5.0f,.10f+.82f*earned);
                    }
                    if(comboActive)
                    {
                        // The reference cabinet keeps the current string in a
                        // bright central badge. It gives a four-year-old an
                        // immediate reward without covering either fighter.
                        float age=Mathf.Clamp01((comboUntil-time)/2.6f);
                        float pop=Mathf.Sin(Mathf.Clamp01(age*3f)*Mathf.PI)*.08f;
                        float width=180*(1+pop),height=42*(1+pop),x=640-width*.5f;
                        hud.Rounded(new Rect(x,91,width,height),new Color(.035f,.055f,.12f,.90f),9);
                        hud.Line(new Vector2(x+12,100),new Vector2(x+50,100),gold,2);
                        hud.Line(new Vector2(x+width-50,100),new Vector2(x+width-12,100),cyan,2);
                        hud.Text(new Rect(x+10,96,width-20,29),$"{comboCount:00} 连击",20,new Color(1,.88f,.48f,Mathf.Clamp01(age*2f)),TextAnchor.MiddleCenter,true);
                        StarBurst(new Vector2(x+8,114),10,Mathf.Clamp01(age*1.6f),gold);
                        StarBurst(new Vector2(x+width-8,114),10,Mathf.Clamp01(age*1.6f),cyan);
                    }
                }
                if(time<hitUntil&&!cinematic)
                {
                    // Keep praise beside the combo counter: the monster's
                    // face and contact area must stay clear in the closer lens.
                    float age=1-(hitUntil-time),lift=Mathf.Clamp01(age)*12;
                    hud.Text(new Rect(33,341-lift,200,23),battle.Punches%5==0?"超棒连击！":"漂亮！",battle.Punches%5==0?14:13,new Color(1,.86f,.48f,Mathf.Clamp01((1-age)*2)),TextAnchor.MiddleLeft,true);
                    float burstAge=Mathf.Clamp01((1-age)*1.4f);
                    Vector2 burstCenter=battle.Punches%5==0?new Vector2(515,270):new Vector2(225,350);
                    StarBurst(burstCenter,24+12*(1-burstAge),burstAge,battle.Punches%5==0?gold:cyan);
                }
                if(lastDamage>0&&time-damagePopAt<.9f&&!cinematic)
                {
                    float age=Mathf.Clamp01((time-damagePopAt)/.9f),lift=Mathf.SmoothStep(0,1,age)*6;
                    float alpha=1-Mathf.SmoothStep(0,1,age);
                    bool special=lastDamage>1;
                    hud.Text(new Rect(1038,76-lift,144,24),"−"+lastDamage.ToString("00"),special?19:16,new Color(1,special?.30f:.66f,.18f,alpha),TextAnchor.MiddleRight,true);
                }
                if(warning&&battle.Action!=HeroAction.Beam)
                {
                    float opacity=battle.Shield?.7f:.3f+Mathf.Sin(time*4)*.1f;
                    hud.Box(new Rect(0,130,4,420),new Color(1,.51f,.22f,opacity));
                    hud.Box(new Rect(1276,130,4,420),new Color(1,.51f,.22f,opacity));
                    string warningText=battle.Enemy==EnemyPhase.Attack?
                        (battle.Shield?"挡住它 · 保持护盾！":MonsterRockMotion.Active(battle)?"飞石来了 · 双手防御！":MonsterRayMotion.Active(battle)?"光线来了 · 双手防御！":"怪兽冲过来了！"):
                        (battle.Shield?"护盾准备好了":MonsterRockMotion.Active(battle)?"怪兽举起了石头":MonsterRayMotion.Active(battle)?"怪兽正在积蓄光线":"怪兽正在蓄力");
                    hud.Text(new Rect(855,105,335,26),warningText,14,battle.Shield?cyan:gold,TextAnchor.MiddleRight,true);
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
                bool monster=battle.TransformationAge>=MonsterEntranceMotion.Start;
                var accent=monster?gold:cyan;
                hud.Fade(new Rect(20,128,290,170),new Color(.006f,.023f,.048f,.78f));
                hud.Line(new Vector2(38,154),new Vector2(91,154),accent,2);
                hud.Text(new Rect(38,166,252,25),monster?"超古代怪兽":"光之英雄",16,accent,bold:true);
                hud.Text(new Rect(38,195,272,47),monster?"哥尔赞":SelectedHero.Name,34,HudPainter.Ink,bold:true);
                hud.Text(new Rect(38,250,272,28),monster?"守护基地，准备出发！":"光的力量，正在觉醒",16,gold);
            }
            else if(victory)
            {
                float age=time-victoryAt,alpha=Mathf.SmoothStep(0,1,(age-.85f)/.45f);
                var winColor=new Color(gold.r,gold.g,gold.b,alpha);
                // The reference cabinet gives the child a clear result card:
                // title, score and a five-star reward read in one glance. Keep
                // it compact on the left so the victory pose and volcanic
                // backdrop remain the hero of the shot.
                hud.Rounded(new Rect(30,58,350,310),new Color(.006f,.022f,.050f,.82f*alpha),12);
                hud.Line(new Vector2(48,78),new Vector2(132,78),cyan,2);
                hud.Line(new Vector2(278,78),new Vector2(362,78),gold,2);
                hud.Text(new Rect(50,82,310,28),"战斗结果",16,cyan,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(48,111,314,52),"守护成功！",32,winColor,TextAnchor.MiddleCenter,true);
                int stars=Mathf.Clamp(3+(battle.Blocks>0?1:0)+(battle.HitsTaken==0?1:0),3,5);
                float starStart=76,starStep=57,starAge=Mathf.Clamp01((age-1.0f)/1.15f);
                for(int i=0;i<5;i++)
                {
                    float reveal=Mathf.Clamp01((starAge-i*.12f)*3.2f);
                    Star(new Vector2(starStart+i*starStep,198),16,i<stars?reveal*.98f:.16f);
                }
                hud.Text(new Rect(52,227,306,20),$"本局得分  {ArcadeScore():000000}",18,winColor,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(52,253,306,18),$"命中 {battle.Punches:00}   防御 {battle.Blocks:00}   受击 {battle.HitsTaken:00}",11,HudPainter.Muted,TextAnchor.MiddleCenter);
                if(age>3)
                {
                    hud.Line(new Vector2(52,284),new Vector2(358,284),new Color(.22f,.50f,.70f,.55f),1);
                    string next=keyboard?(photoAvailable?"按 F7 合照，或按 R 再玩一次":"按 R，再守护一次火山基地"):
                        "接下来，和"+SelectedHero.Name+"合照";
                    hud.Text(new Rect(52,294,306,54),"谢谢你！\n"+next,16,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                }
            }
            else if(battle.Phase==GamePhase.Paused)
            {
                bool interrupted=!keyboard&&!PoseQuality.Fresh(pose,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                string text=paused?"休息一下 · 按 Esc 继续":keyboard?"准备好了，马上继续战斗":
                    interrupted?"相机连接中断 · 进度已保留":"站进镜头，我们接着守护火山基地";
                Guide(text,"transform",cyan,battle.ResumeProgress/1.2f);
            }
            else if(battle.Action!=HeroAction.Hurt&&(!battle.Finishing||battle.Action==HeroAction.Beam))
            {
                // The fall and recovery need the full lower part of the shot;
                // resume the normal instructions only after the hero stands.
                if(battle.Action==HeroAction.Beam)DrawBeamReleaseTitle();
                else if(ready&&!keyboard&&(recognizer.BeamProgress>0||held.BeamIntent))
                    // Keep the instruction aligned with the gesture that owns
                    // input, even if an enemy warning begins during its hold.
                    Guide(recognizer.BeamProgress>0?$"{SelectedHero.Beam}姿势已锁定 · 保持，释放光线！":"看见光线姿势 · 保持一下","beam",gold,recognizer.BeamProgress);
                else if(warning)
                    Guide(battle.Shield||(!keyboard&&held.GuardIntent)?(keyboard?"护盾已展开 · 继续按住 S":"护盾姿势已锁定 · 保持住！"):
                        keyboard?"按住 S，展开护盾！":"双手放胸前，也可以交叉抱住！","shield",battle.Shield?cyan:gold,keyboard?(battle.Shield?1:0):recognizer.ShieldProgress);
                else if(ready)
                    Guide(keyboard?"能量已满 · 按 J 释放光线":"双臂交叉成光线姿势，停一下","beam",gold,keyboard?1:recognizer.BeamProgress);
                else if(time<captionUntil||battle.Punches<3)
                    Guide(keyboard?"A / D 出拳 · Q / E 光弹":time<captionUntil?caption:SelectedHero.Id=="Mebius"?"左手侧挥光剑 · 向前挥拳发光弹":"向前挥拳发"+HeroArsenal.RangedName(SelectedHero.Id)+" · 快慢跟随你","punch",cyan,0);
            }
            DrawBattleStartCue();
            DrawArcadePreview();
            if(!waiting&&!transforming&&!victory&&ready&&!cinematic)
            {
                // A small pulsing ring around the full energy cue makes the
                // special move feel available before the spoken instruction.
                float pulse=.5f+.5f*Mathf.Sin(time*5.5f);
                StarBurst(new Vector2(302,54),13,.28f+.28f*pulse,gold);
            }
            if(pose?.source=="synthetic")hud.Text(new Rect(20,695,400,20),"合成动作测试 · 非真人输入",11,HudPainter.Gold);
        }

        // The reference cabinet keeps a lit physical frame around the playfield.
        // Recreate that read at screen space so it survives the Fuji wide shot,
        // beam cut-in and the small display used during living-room play. The
        // rails stay peripheral and never cover the actors or gesture guide.
        void DrawArcadeRails(float time,bool cinematic,bool warning,bool victory,bool transforming)
        {
            if(battle.Phase==GamePhase.Waiting)return;
            float pulse=.5f+.5f*Mathf.Sin(time*5.2f);
            float action=battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch
                ?Mathf.Sin(Mathf.Clamp01(battle.ActionAge/.42f)*Mathf.PI):0;
            float threat=warning?.5f+.5f*Mathf.Sin(time*8.5f):0;
            float beam=cinematic?Mathf.Clamp01(battle.ActionAge/1.15f):0;
            float intensity=Mathf.Max(action,Mathf.Max(threat,beam));
            float alpha=transforming?.22f:victory?.16f:.24f+.10f*pulse+.16f*intensity;
            Color left=new Color(.18f,.78f,1,alpha),right=new Color(1,.34f,.12f,alpha);
            if(warning&&!cinematic)
            {
                left=Color.Lerp(left,new Color(1,.30f,.10f,alpha),threat*.72f);
                right=new Color(1,.26f,.08f,alpha+.12f*threat);
            }
            if(cinematic)
            {
                left=Color.Lerp(left,new Color(.24f,.92f,1,alpha+.25f),beam);
                right=Color.Lerp(right,new Color(.52f,.78f,1,alpha+.25f),beam);
            }
            if(action>.02f)
            {
                left.a=Mathf.Clamp01(left.a+.18f*action);right.a=Mathf.Clamp01(right.a+.12f*action);
            }
            const float top=112,bottom=660;
            hud.Box(new Rect(8,top,3,bottom-top),new Color(left.r,left.g,left.b,left.a*.28f));
            hud.Box(new Rect(1269,top,3,bottom-top),new Color(right.r,right.g,right.b,right.a*.28f));
            int railCount=intensity>.05f?6:4;
            for(int i=0;i<railCount;i++)
            {
                float y=top+34+i*((bottom-top-68)/Mathf.Max(1,railCount-1));
                float wave=.55f+.45f*Mathf.Sin(time*4.1f+i*.85f);
                float width=14+6*wave;
                hud.Line(new Vector2(13,y),new Vector2(13+width,y-8),new Color(left.r,left.g,left.b,left.a*(.34f+.28f*wave)),2);
                hud.Line(new Vector2(1267,y),new Vector2(1267-width,y-8),new Color(right.r,right.g,right.b,right.a*(.34f+.28f*wave)),2);
            }
            // A small lower chevron locks the frame to the ground plane during
            // a hit, giving the child a clear visual rhythm without a panel.
            float groundPulse=.10f+.70f*Mathf.Max(action,beam,threat);
            hud.Line(new Vector2(13,674),new Vector2(39,674),new Color(left.r,left.g,left.b,left.a*groundPulse),2);
            hud.Line(new Vector2(1267,674),new Vector2(1241,674),new Color(right.r,right.g,right.b,right.a*groundPulse),2);
        }

        // The reference cabinet uses brief directional streaks to connect a
        // child's movement with the fighter's contact. Keep them peripheral
        // and short: they add speed to a hit without hiding the actual pose or
        // turning the TV view into a permanent post-processing overlay.
        void DrawMotionBursts(float time,bool cinematic,bool warning,bool victory,bool transforming)
        {
            if(cinematic||victory||transforming)return;
            bool punch=battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch;
            float punchPulse=punch?Mathf.Sin(Mathf.Clamp01(battle.ActionAge/.38f)*Mathf.PI):0;
            float rushPulse=battle.Enemy==EnemyPhase.Attack?Mathf.Sin(Mathf.Clamp01(battle.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI):0;
            float hurtPulse=battle.Action==HeroAction.Hurt?Mathf.Sin(Mathf.Clamp01(battle.ActionAge/KnockdownMotion.Duration)*Mathf.PI):0;
            if(punchPulse>.015f)
                // The old eight-ray burst reached well into both actors and
                // read like a debug overlay on a television. Keep a compact
                // six-ray accent at the contact lane; the world-space flash,
                // hand wake and sparks still carry the actual hit.
                ScreenBurst(new Vector2(642,380),punchPulse,new Color(.26f,.84f,1),time,0,6,10,128);
            if(rushPulse>.015f)
                ScreenBurst(new Vector2(1010,350),rushPulse,new Color(1,.34f,.12f),time,1,6,12,146);
            if(hurtPulse>.015f)
                ScreenBurst(new Vector2(438,420),hurtPulse,new Color(1,.18f,.12f),time,2,5,10,122);
            if(warning&&!punch&&rushPulse<=.015f)
            {
                float pulse=.18f+.12f*(.5f+.5f*Mathf.Sin(time*7.2f));
                ScreenBurst(new Vector2(1020,350),pulse,new Color(1,.46f,.15f),time,3,4,12,112);
            }
        }

        void ScreenBurst(Vector2 center,float pulse,Color color,float time,int seed,int count,float inner,float outer)
        {
            for(int i=0;i<count;i++)
            {
                float angle=(i+seed*.37f)*Mathf.PI*2/count+time*(.16f+seed*.03f);
                float wobble=.84f+.16f*Mathf.Sin(time*4.2f+i*1.7f+seed);
                // Grow from a small contact tick to a short, rounded ember
                // fragment. Long fixed rays read like a debug overlay on a
                // television; a tapered dash plus a hot endpoint keeps the
                // same timing while leaving the actor and volcanic stage clear.
                float start=inner*(.82f+.18f*wobble),end=outer*(.18f+.82f*pulse)*(.82f+.18f*wobble);
                Vector2 direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                // Keep the arcade punctuation close to the contact. Long
                // screen-space rays landed over the Fuji ruins and read as
                // red debug lines instead of heat or dust. A short ember dash
                // plus a soft endpoint preserves the timing without slicing
                // across the background.
                float travel=end*.22f,alpha=Mathf.Clamp01(pulse*(.045f+.018f*(i%3)));
                var fragment=new Color(color.r,color.g,color.b,alpha);
                hud.Line(center+direction*start,center+direction*(start+travel),fragment,i%3==0?2:1);
                hud.Dot(center+direction*(start+travel),1.2f+1.8f*pulse,
                    new Color(Mathf.Min(1,color.r+.12f),Mathf.Min(1,color.g+.12f),color.b,alpha*.92f));
            }
        }

        void DrawBattleHeader(bool ready,bool opening,bool cinematic)
        {
            // During the beam, retain only peripheral energy/health strips.
            // The reaction lens can bring either actor through the old portraits.
            Rect energy=cinematic?new Rect(24,18,236,5):BattleHudLayout.Energy;
            Rect healthBar=cinematic?new Rect(1020,18,236,5):BattleHudLayout.EnemyHealth;
            var energyColor=ready?HudPainter.Gold:HudPainter.Cyan;
            if(!cinematic)
            {
                hud.Fade(new Rect(0,0,1280,86),new Color(.004f,.014f,.035f,.74f));
                BattlePlate(BattleHudLayout.HeroPlate,HudPainter.Cyan,false);
                BattlePlate(BattleHudLayout.EnemyPlate,new Color(1,.47f,.21f),true);
                hud.HeroPortrait(new Rect(28,12,46,46),SelectedHero.Id);
                hud.Portrait(new Rect(1206,12,46,46),true);
                hud.Text(new Rect(84,13,132,23),SelectedHero.Name,14,HudPainter.Ink,bold:true);
                hud.Text(new Rect(212,17,130,17),ready?"必杀已就绪":$"光线 {battle.Energy:0} / 15",10,ready?HudPainter.Gold:HudPainter.Muted,TextAnchor.MiddleRight);
                hud.Text(new Rect(1032,13,166,23),"哥尔赞",14,HudPainter.Ink,TextAnchor.MiddleRight,true);
                hud.Text(new Rect(932,17,92,17),$"{Mathf.CeilToInt(battle.EnemyHealth)} / {battle.MaxHealth}",10,HudPainter.Muted);
                if(!opening)
                {
                    hud.Text(new Rect(485,11,310,19),"火山大决战",11,HudPainter.Muted,TextAnchor.MiddleCenter);
                    hud.Text(new Rect(485,31,310,24),$"{ArcadeScore():000000}",17,new Color(1,.82f,.42f),TextAnchor.MiddleCenter,true);
                }
            }
            for(int i=0;i<Battle.MaxEnergy;i++)
                hud.Box(new Rect(energy.x+i*energy.width/Battle.MaxEnergy,energy.y,energy.width/Battle.MaxEnergy-(cinematic?2:4),energy.height),
                    i<battle.Energy?energyColor:new Color(.10f,.21f,.29f));
            float health=Mathf.Clamp01(battle.EnemyHealth/battle.MaxHealth),ghost=Mathf.Clamp01(enemyHealthDisplay/battle.MaxHealth);
            hud.Box(new Rect(healthBar.x-1,healthBar.y-1,healthBar.width+2,healthBar.height+2),new Color(.09f,.13f,.19f));
            if(ghost>health+.001f)
                hud.Box(new Rect(healthBar.x+healthBar.width*(1-ghost),healthBar.y,healthBar.width*(ghost-health),healthBar.height),new Color(1,.84f,.35f,.78f));
            hud.Box(new Rect(healthBar.x+healthBar.width*(1-health),healthBar.y,healthBar.width*health,healthBar.height),
                health<.3f?new Color(1,.28f,.16f):new Color(1,.63f,.23f));
            if(!cinematic)for(int i=1;i<10;i++)
                hud.Box(new Rect(healthBar.x+i*healthBar.width/10,healthBar.y,1,healthBar.height),new Color(.1f,.07f,.02f,.34f));
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
            hud.Rounded(BattleHudLayout.BeamTitle,new Color(.006f,.02f,.055f,.80f*alpha),8);
            hud.Line(new Vector2(438,716),new Vector2(842,716),new Color(.22f,.72f,1,.65f*alpha),1);
            hud.Text(new Rect(432,700,416,14),$"{SelectedHero.Beam} · {SelectedHero.BeamJapanese}",12,new Color(.77f,.94f,1,alpha),TextAnchor.MiddleCenter,true);
        }

        void DrawFinisherCallout(float time)
        {
            // The cabinet reference uses a compact character/skill cut-in when
            // the finisher takes over the screen. Keep it in the clear left
            // rail so the hero's hands, beam lane and monster reaction remain
            // unobstructed in the dedicated close-up.
            float age=Mathf.Max(0,battle.ActionAge);
            float intro=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.16f));
            float outro=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.55f)/.32f));
            float alpha=Mathf.Clamp01(intro*outro);
            float x=Mathf.Lerp(-228,28,intro),y=142,w=226,h=148;
            var cyan=new Color(.24f,.86f,1,alpha*.92f);
            var gold=new Color(1,.72f,.30f,alpha*.94f);
            hud.Rounded(new Rect(x+4,y+5,w,h),new Color(0,0,0,.24f*alpha),12);
            hud.Rounded(new Rect(x,y,w,h),new Color(.008f,.026f,.060f,.88f*alpha),12);
            hud.Line(new Vector2(x+14,y+13),new Vector2(x+78,y+13),cyan,2);
            hud.Line(new Vector2(x+w-78,y+13),new Vector2(x+w-14,y+13),gold,2);
            hud.HeroPortrait(new Rect(x+14,y+28,74,104),SelectedHero.Id);
            hud.Text(new Rect(x+99,y+29,110,18),"必杀技",12,gold,TextAnchor.MiddleLeft,true);
            hud.Text(new Rect(x+99,y+51,112,42),SelectedHero.Beam,17,HudPainter.Ink,TextAnchor.MiddleLeft,true);
            hud.Text(new Rect(x+99,y+96,112,25),SelectedHero.BeamJapanese,10,new Color(.56f,.86f,1,alpha),TextAnchor.MiddleLeft);
            float pulse=.52f+.48f*Mathf.Sin(time*8.5f);
            hud.Line(new Vector2(x+99,y+126),new Vector2(x+202,y+126),new Color(.24f,.72f,1,alpha*(.45f+.35f*pulse)),2);
        }

        // A short cabinet-style cut-in makes the transition out of the
        // transformation readable on a TV. It fades before the first enemy
        // warning so the actors and the camera remain the focus of the round.
        void DrawBattleStartCue()
        {
            if(battle.Phase!=GamePhase.Battle)return;
            float age=Time.unscaledTime-battleStartCueAt;
            if(battleStartCueAt<0||age<0||age>2.8f)return;
            float intro=Mathf.SmoothStep(.42f,1f,Mathf.Clamp01(age/.16f));
            float outro=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.80f)/1.0f));
            float alpha=Mathf.Clamp01(intro*outro);
            float scale=Mathf.Lerp(.88f,1f,intro);
            // Keep the opening cue legible at TV distance without stealing the
            // upper third of the playfield.  The reference cabinet uses a
            // compact versus badge; the fighters should remain the largest
            // shapes from the first battle frame.
            float width=360*scale,height=48*scale,x=(1280-width)/2,y=8;
            var cyan=new Color(.22f,.84f,1,alpha*.92f);
            var gold=new Color(1,.70f,.25f,alpha*.92f);
            hud.Rounded(new Rect(x,y,width,height),new Color(.008f,.035f,.075f,alpha*.95f),8);
            hud.Line(new Vector2(x+10,y+14),new Vector2(x+68,y+14),cyan,2);
            hud.Line(new Vector2(x+width-68,y+14),new Vector2(x+width-10,y+14),gold,2);
            hud.Text(new Rect(x+14,y+4,126,16),SelectedHero.Name,11,new Color(.74f,.88f,1,alpha),TextAnchor.MiddleLeft,true);
            hud.Text(new Rect(x+width-140,y+4,126,16),"哥尔赞",11,new Color(1,.72f,.42f,alpha),TextAnchor.MiddleRight,true);
            hud.Text(new Rect(x+14,y+20,width-28,23),"VS · 开战！",18,new Color(1,.88f,.53f,alpha),TextAnchor.MiddleCenter,true);
            float fightAge=Mathf.Clamp01((age-.42f)/.74f);
            float fightFade=Mathf.Clamp01(Mathf.Min(fightAge*3f,(1-fightAge)*3f))*alpha;
            if(fightFade>.001f)
            {
                float center=640,top=98;
                hud.Line(new Vector2(center-150,top),new Vector2(center+150,top),new Color(.24f,.82f,1,fightFade*.24f),2);
                hud.Line(new Vector2(center-128,top+48),new Vector2(center+128,top+48),new Color(1,.57f,.20f,fightFade*.18f),2);
                hud.Text(new Rect(center-190,top+5,380,38),"FIGHT!",30,new Color(1,.91f,.62f,fightFade),TextAnchor.MiddleCenter,true);
            }
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
            hud.Fade(new Rect(0,690,1280,30),new Color(.005f,.015f,.035f,.12f));
            hud.Rounded(BattleHudLayout.Guide,new Color(.007f,.025f,.053f,.82f),8);
            hud.Box(new Rect(370,701,2,12),accent);
            hud.Figure(new Rect(382,696,24,20),gesture,Time.unscaledTime,accent);
            hud.Text(new Rect(414,700,480,14),title,12,HudPainter.Ink,TextAnchor.MiddleCenter,true);
            hud.Bar(new Rect(456,716,410,1),progress,accent);
        }
        void DrawArcadePreview()
        {
            if(keyboard)return;
            if(!showPreview)
            {if(hud.Button(new Rect(1100,653,156,30),"显示取景 · F3",HudPainter.Cyan,12))showPreview=true;return;}
            // Keep the camera available as a confidence check, but give the
            // cabinet lane back to the fighters while an action is actually
            // playing. The preview never disappears; it only breathes from
            // the neutral 156x117 card to a compact 124x93 card and returns
            // before the next gesture instruction.
            bool actionFocus=battle.Phase==GamePhase.Battle&&
                (battle.Action!=HeroAction.None||world.Closeup.Active||battle.Enemy==EnemyPhase.Attack);
            previewFocus=Mathf.MoveTowards(previewFocus,actionFocus?.78f:1f,Time.unscaledDeltaTime*4.5f);
            float width=Mathf.Lerp(124,156,previewFocus),height=Mathf.Lerp(93,117,previewFocus);
            var r=new Rect(1256-width,683-height,width,height);
            float alpha=Mathf.Lerp(.68f,.84f,previewFocus);
            hud.Rounded(new Rect(r.x-4,r.y-18,r.width+8,r.height+40),new Color(.01f,.025f,.05f,alpha),6);
            hud.Text(new Rect(r.x,r.y-18,r.width-48,17),"镜像取景",10,HudPainter.Cyan);
            if(hud.Button(new Rect(r.xMax-43,r.y-18,43,17),"收起",HudPainter.Muted,10))showPreview=false;
            if(previewTexture&&previewFrame!=null&&previewFrame.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))hud.Image(r,previewTexture,ScaleMode.ScaleToFit);
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
        void StarBurst(Vector2 center,float radius,float opacity,Color color)
        {
            opacity=Mathf.Clamp01(opacity);if(opacity<=.001f)return;
            for(int i=0;i<8;i++)
            {
                float a=(i*Mathf.PI/4f)+Time.unscaledTime*.35f;
                float inner=radius*.28f,outer=radius*(i%2==0?1:.72f);
                var c=new Color(color.r,color.g,color.b,opacity*.78f);
                hud.Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*inner,
                    center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*outer,c,i%2==0?2:1);
            }
            hud.Dot(center,radius*.18f,new Color(1,1,1,opacity*.9f));
        }
        int ArcadeScore()
        {
            int score=battle.Punches*100+battle.Blocks*250-battle.HitsTaken*50;
            if(battle.Phase==GamePhase.Victory)score+=1000;
            return Mathf.Max(0,score);
        }
    }
}
