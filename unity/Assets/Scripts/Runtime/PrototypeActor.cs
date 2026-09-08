using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Camera-driven rig with reference-based, locally authored articulated meshes.
    public sealed class PrototypeActor
    {
        public readonly Transform Root;
        readonly Transform torso,head;
        readonly Transform[] arms=new Transform[4],legs=new Transform[4],fists=new Transform[2],feet=new Transform[2],elbows=new Transform[2];
        readonly bool monster;
        readonly Vector3 pivot=new Vector3(0,1.24f,0);
        readonly Vector3[] shoulder={new Vector3(-.43f,2.08f,0),new Vector3(.43f,2.08f,0)};
        readonly Vector3[] wrist={new Vector3(-.65f,1.35f,.15f),new Vector3(.65f,1.35f,.15f)};
        readonly Vector3[] elbow={new Vector3(-.72f,1.72f,.05f),new Vector3(.72f,1.72f,.05f)};
        readonly Vector3[] lastWrist=new Vector3[2],lastElbow=new Vector3[2];
        readonly float[] wristSeen={-10,-10},elbowSeen={-10,-10};
        float bodyScale=.25f,roll,bodyTurn;
        readonly CharacterModel model;
        public PrototypeActor(string name,Vector3 position,bool monster=false)
        {
            this.monster=monster;
            Root=new GameObject(name).transform;Root.position=position;
            torso=new GameObject("Hip and chest rig").transform;torso.SetParent(Root,false);torso.localPosition=pivot;
            model=new CharacterModel(Root,torso,monster);head=model.Head;
            arms=model.Arms;legs=model.Legs;fists=model.Hands;feet=model.Feet;elbows=model.Elbows;
            if(monster) {shoulder[0].x=-.56f;shoulder[1].x=.56f;}
        }
        public static Material Material(Color color,float metallic=0,bool emission=false)
        {
            var template=Resources.Load<Material>("PrototypeSurface");
            var material=template?new Material(template):new Material(Shader.Find("Standard"));material.color=color;
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",.6f);
            if(emission) { material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*1.5f); }
            return material;
        }
        static void Bone(Transform bone,Vector3 a,Vector3 b)
        { bone.localPosition=(a+b)*.5f;Vector3 up=(b-a).normalized;Vector3 forward=Vector3.ProjectOnPlane(Vector3.forward,up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(Vector3.up,up);
            bone.localRotation=Quaternion.LookRotation(forward,up);bone.localScale=new Vector3(1,(b-a).magnitude,1); }
        static void Solve(Vector3 origin,ref Vector3 middle,ref Vector3 end,float upper,float lower)
        {
            Vector3 direction=end-origin;float distance=Mathf.Clamp(direction.magnitude,.09f,upper+lower-.005f);
            direction=direction.sqrMagnitude<.001f?Vector3.down:direction.normalized;end=origin+direction*distance;
            Vector3 bend=middle-origin-direction*Vector3.Dot(middle-origin,direction);
            if(bend.sqrMagnitude<.001f) bend=Vector3.Cross(direction,Vector3.forward);
            if(bend.sqrMagnitude<.001f) bend=Vector3.right;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            middle=origin+direction*along+bend.normalized*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
        }
        Vector3 Map(PosePoint p,PoseFrame frame)
        {
            var a=frame.points[11];var b=frame.points[12];
            return new Vector3(Mathf.Clamp(-(p.x-(a.x+b.x)*.5f)/bodyScale*.86f,-1.6f,1.6f),
                Mathf.Clamp(2.08f-(p.y-(a.y+b.y)*.5f)/bodyScale*.86f,.9f,3.35f),.12f);
        }
        public static float Strike(float age)
        {
            float t=Mathf.Clamp01(age/Battle.PunchSeconds);
            return t<.28f?Mathf.SmoothStep(0,1,t/.28f):Mathf.Pow(1-(t-.28f)/.72f,1.6f);
        }
        public void Update(PoseFrame pose,Battle game,float dt,float time)
        {
            bool punch=!monster&&game.Phase==GamePhase.Battle&&(game.Action==HeroAction.LeftPunch||game.Action==HeroAction.RightPunch);
            bool celebration=game.Phase==GamePhase.Victory;
            float strike=punch?Strike(game.ActionAge):0;
            float breathe=Mathf.Sin(time*2.5f+(monster?1:0));
            if(!monster && pose!=null)
            {
                float span=Mathf.Clamp(PoseQuality.Distance(pose.points[11],pose.points[12]),.08f,.8f);
                bodyScale=Mathf.Lerp(bodyScale,span,1-Mathf.Exp(-dt*6));
                roll=Mathf.Lerp(roll,Mathf.Clamp((pose.points[11].y-pose.points[12].y)/bodyScale*20,-10,10),1-Mathf.Exp(-dt*12));
            }
            else roll=Mathf.Lerp(roll,0,dt*5);
            float turn=punch?(game.Action==HeroAction.LeftPunch?18:-18)*strike:0;
            bodyTurn=Mathf.Lerp(bodyTurn,turn,1-Mathf.Exp(-dt*26));
            torso.localPosition=pivot+new Vector3(Mathf.Sin(time*1.4f)*.015f,breathe*.018f-strike*.045f,0);
            torso.localRotation=Quaternion.Euler(strike*8+(game.Action==HeroAction.Hurt&&!monster?-10:0),bodyTurn,roll*.45f);
            head.localRotation=Quaternion.Euler(breathe*1.2f,-bodyTurn*.45f,roll*-.3f);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                Vector3 te=new Vector3(sign*.70f,1.73f,.12f),tw=new Vector3(sign*.55f,1.43f,.36f);
                if(!monster && pose!=null)
                {
                    if(PoseQuality.Reliable(pose.points[13+side])) { lastElbow[side]=Map(pose.points[13+side],pose);elbowSeen[side]=time; }
                    if(PoseQuality.Reliable(pose.points[15+side])) { lastWrist[side]=Map(pose.points[15+side],pose);wristSeen[side]=time; }
                }
                if(!monster)
                {
                    if(time-elbowSeen[side]<.22f)te=lastElbow[side];
                    if(time-wristSeen[side]<.22f)tw=lastWrist[side];
                }
                if(!monster&&(game.Phase==GamePhase.Transforming||celebration))
                { te=new Vector3(sign*.86f,2.60f,0);tw=new Vector3(sign*.57f,3.15f,.12f); }
                if(!monster&&game.Phase==GamePhase.Battle&&game.Shield)
                { te=new Vector3(sign*.65f,1.83f,.3f);tw=new Vector3(sign*.09f,2.15f,.65f); }
                if(!monster&&game.Phase==GamePhase.Battle&&game.Action==HeroAction.Beam)
                { te=new Vector3(sign*.46f,1.97f,.37f);tw=side==0?new Vector3(-.1f,2.56f,.65f):new Vector3(-.10f,2.11f,.68f); }
                if(punch && side==(game.Action==HeroAction.LeftPunch?0:1))
                { te=new Vector3(sign*.39f,1.98f,.42f);tw=new Vector3(sign*.20f,2.04f,.5f+strike*.60f); }
                if(monster)
                {
                    tw+=new Vector3(0,breathe*.07f,0);
                    if(game.Enemy==EnemyPhase.Windup) { te=new Vector3(sign*.77f,2.47f,0);tw=new Vector3(sign*.53f,3.05f,.16f); }
                }
                float response=(tw-wrist[side]).magnitude>.15f?30:18;
                float alpha=1-Mathf.Exp(-dt*response);
                wrist[side]=Vector3.Lerp(wrist[side],tw,alpha);elbow[side]=Vector3.Lerp(elbow[side],te,alpha);
                Vector3 solvedEnd=wrist[side],solvedElbow=elbow[side];Solve(shoulder[side],ref solvedElbow,ref solvedEnd,.49f,.51f);
                Bone(arms[side*2],shoulder[side]-pivot,solvedElbow-pivot);
                Bone(arms[side*2+1],solvedElbow-pivot,solvedEnd-pivot);
                elbows[side].localPosition=solvedElbow-pivot;fists[side].localPosition=solvedEnd-pivot;
                fists[side].localRotation=Quaternion.FromToRotation(Vector3.forward,solvedEnd-solvedElbow);
                Vector3 hip=Root.InverseTransformPoint(torso.TransformPoint(new Vector3(sign*(monster?.29f:.19f),0,0)));
                float step=punch && side==(game.Action==HeroAction.LeftPunch?1:0)?strike:0;
                Vector3 ankle=new Vector3(sign*(monster?.43f:.26f),.19f,side==0?.16f:-.16f);ankle+=new Vector3(0,step*.055f,step*.35f);
                Vector3 knee=new Vector3(sign*.25f,.7f,.12f+step*.2f);Solve(hip,ref knee,ref ankle,.54f,.56f);
                Bone(legs[side*2],hip,knee);Bone(legs[side*2+1],knee,ankle);
                model.Knees[side].localPosition=knee;
                feet[side].localPosition=ankle+new Vector3(0,-.055f,.11f);
            }
            if(monster)
            {
                model.AnimateTail(time);
                Root.localScale=Vector3.one*(celebration?Mathf.Max(.04f,Root.localScale.x-dt*.5f):1.15f);
            }
        }
    }
}
