using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // All effects share Battle's attack clock; no delayed callbacks survive pause or restart.
    public sealed class MonsterAttackEffects
    {
        readonly Vector3 home,target,forward;
        readonly LineRenderer charge,shock;
        readonly LineRenderer[] trails=new LineRenderer[3],claws=new LineRenderer[3];
        readonly Material glow;
        static readonly Color Amber=new Color(1,.40f,.10f),Ice=new Color(.25f,.86f,1);
        float hitAge=10;
        Color hitColor;
        public bool SlashVisible => claws[0].enabled;
        public MonsterAttackEffects(Transform parent,Vector3 monsterHome,Vector3 heroHome)
        {
            home=monsterHome;target=heroHome;forward=(target-home).normalized;
            glow=new Material(Resources.Load<Shader>("SoftGlow")) {color=Color.white};
            charge=Line(parent,"Monster charge",48,.035f,true);
            shock=Line(parent,"Monster contact",48,.07f,true);
            for(int i=0;i<3;i++)
            {trails[i]=Line(parent,"Monster rush trail",2,.055f);claws[i]=Line(parent,"Monster claw sweep",20,.08f);}
        }
        LineRenderer Line(Transform parent,string name,int points,float width,bool loop=false)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
            line.sharedMaterial=glow;line.useWorldSpace=true;line.positionCount=points;line.widthMultiplier=width;
            line.loop=loop;line.numCapVertices=4;line.enabled=false;return line;
        }
        static void ColorLine(LineRenderer line,Color color,float alpha)
        {color.a=alpha;line.startColor=line.endColor=color;}
        static void Circle(LineRenderer line,Vector3 center,Camera camera,float radius)
        {
            for(int i=0;i<line.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/line.positionCount;
                line.SetPosition(i,center+camera.transform.right*Mathf.Cos(angle)*radius+camera.transform.up*Mathf.Sin(angle)*radius);
            }
        }
        public void Impact(bool blocked) {hitAge=0;hitColor=blocked?Ice:Amber;}
        public void Tick(Battle state,Camera camera,float dt)
        {
            bool active=state.Phase==GamePhase.Battle;
            if(!active)hitAge=10;else hitAge+=dt;
            bool warning=active&&state.Enemy==EnemyPhase.Windup;
            bool attack=active&&state.Enemy==EnemyPhase.Attack;
            Vector3 monster=home+forward*AnimatedActor.MonsterAdvance(state);
            Vector3 contact=target-forward*.7f+Vector3.up*2.15f-camera.transform.forward*.3f;
            charge.enabled=warning;
            if(warning)
            {
                float p=Mathf.Clamp01(state.EnemyAge/state.WarningDuration);
                Circle(charge,monster+Vector3.up*2.75f-camera.transform.forward*.2f,camera,.35f+p*.24f);
                ColorLine(charge,Amber,.20f+p*.45f);
            }
            for(int i=0;i<3;i++)
            {
                var trail=trails[i];trail.enabled=attack&&state.EnemyAge<Battle.EnemyHitSeconds;
                if(trail.enabled)
                {
                    Vector3 p=monster+Vector3.up*(.5f+i*.48f)+camera.transform.right*(i-1)*.16f;
                    trail.SetPosition(0,p-forward*(1.6f+i*.18f));trail.SetPosition(1,p);
                    trail.startColor=new Color(1,.4f,.1f,0);trail.endColor=new Color(1,.65f,.22f,.48f);
                }
                var claw=claws[i];float age=state.EnemyAge;
                claw.enabled=attack&&age>=.23f&&age<.68f;
                if(claw.enabled)
                {
                    float sweep=Mathf.Clamp01((age-.23f)/.17f);
                    // Follow the approaching claw; it reaches the hero only at the contact keyframe.
                    Vector3 swipeCenter=contact-forward*Mathf.Max(0,2.25f-AnimatedActor.MonsterAdvance(state));
                    for(int j=0;j<claw.positionCount;j++)
                    {
                        float t=j/(float)(claw.positionCount-1)*sweep;
                        Vector3 p=swipeCenter+camera.transform.right*((i-1)*.22f+Mathf.Lerp(.6f,-.5f,t)+Mathf.Sin(t*Mathf.PI)*.16f)
                            +camera.transform.up*(Mathf.Lerp(.55f,-.55f,t)+(i-1)*.1f);
                        claw.SetPosition(j,p);
                    }
                    ColorLine(claw,new Color(1,.73f,.34f),Mathf.Clamp01((.68f-age)/.23f)*.85f);
                }
            }
            shock.enabled=active&&hitAge<.38f;
            if(shock.enabled)
            {Circle(shock,contact,camera,Mathf.Lerp(.13f,1.05f,hitAge/.38f));ColorLine(shock,hitColor,(1-hitAge/.38f)*.85f);}
        }
    }
}
