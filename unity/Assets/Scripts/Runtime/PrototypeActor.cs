using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Locally authored articulated character. Presentation is independent of hit/gesture rules.
    public sealed class PrototypeActor
    {
        public readonly Transform Root;
        readonly Transform torso,head;
        readonly Transform[] arms=new Transform[4],legs=new Transform[4],fists=new Transform[2],feet=new Transform[2],elbows=new Transform[2];
        readonly Transform[] tail=new Transform[4];
        readonly bool monster;
        readonly Vector3 pivot=new Vector3(0,1.24f,0);
        readonly Vector3[] shoulder={new Vector3(-.43f,2.08f,0),new Vector3(.43f,2.08f,0)};
        readonly Vector3[] wrist={new Vector3(-.65f,1.35f,.15f),new Vector3(.65f,1.35f,.15f)};
        readonly Vector3[] elbow={new Vector3(-.72f,1.72f,.05f),new Vector3(.72f,1.72f,.05f)};
        readonly Vector3[] lastWrist=new Vector3[2],lastElbow=new Vector3[2];
        readonly float[] wristSeen={-10,-10},elbowSeen={-10,-10};
        float bodyScale=.25f,roll,bodyTurn;
        public PrototypeActor(string name,Vector3 position,bool monster=false)
        {
            this.monster=monster;
            Root=new GameObject(name).transform;Root.position=position;
            torso=new GameObject("Hip and chest rig").transform;torso.SetParent(Root,false);torso.localPosition=pivot;
            var silver=Material(new Color(.73f,.81f,.92f),.72f);
            var red=Material(new Color(.66f,.025f,.095f),.30f);
            var purple=Material(new Color(.27f,.075f,.55f),.4f);
            var dark=Material(new Color(.10f,.24f,.29f),.3f);
            var armor=Material(new Color(.27f,.39f,.42f),.45f);
            var gold=Material(new Color(.95f,.58f,.14f),.65f);
            var eyes=Material(new Color(1,.90f,.55f),.1f,true);
            var cyan=Material(new Color(.1f,.78f,1),.3f,true);
            var main=monster?dark:silver;
            Upper(PrimitiveType.Sphere,new Vector3(0,1.78f,0),new Vector3(.85f,.85f,.47f),main);
            Upper(PrimitiveType.Capsule,new Vector3(0,1.4f,0),new Vector3(.58f,.32f,.40f),monster?armor:purple);
            Upper(PrimitiveType.Capsule,new Vector3(0,2.22f,0),new Vector3(.22f,.18f,.24f),main);
            head=new GameObject("Head rig").transform;head.SetParent(torso,false);head.localPosition=new Vector3(0,2.58f,0)-pivot;
            Part(head,PrimitiveType.Sphere,Vector3.zero,new Vector3(.56f,.68f,.50f),main);
            if(!monster)
            {
                // Symmetric silver mask, central fin, forehead crystal and red/purple chest panels.
                var crest=Part(head,PrimitiveType.Cube,new Vector3(0,.28f,-.01f),new Vector3(.065f,.37f,.34f),silver);
                crest.localRotation=Quaternion.Euler(-12,0,0);
                Part(head,PrimitiveType.Sphere,new Vector3(0,.24f,.226f),new Vector3(.11f,.16f,.055f),purple);
                for(int side=0;side<2;side++)
                {
                    float sign=side==0?-1:1;
                    var eye=Part(head,PrimitiveType.Sphere,new Vector3(sign*.138f,.055f,.225f),new Vector3(.20f,.105f,.05f),eyes);
                    eye.localRotation=Quaternion.Euler(0,sign*8,sign*15);
                    var cheek=Part(head,PrimitiveType.Cube,new Vector3(sign*.13f,-.13f,.19f),new Vector3(.12f,.15f,.07f),silver);
                    cheek.localRotation=Quaternion.Euler(0,sign*20,sign*18);
                    var chest=Upper(PrimitiveType.Sphere,new Vector3(sign*.205f,1.92f,.12f),new Vector3(.39f,.43f,.30f),side==0?red:purple);
                    chest.localRotation=Quaternion.Euler(0,0,sign*14);
                    var trim=Upper(PrimitiveType.Cube,new Vector3(sign*.19f,2.055f,.248f),new Vector3(.46f,.085f,.065f),gold);
                    trim.localRotation=Quaternion.Euler(0,0,sign*24);
                    Upper(PrimitiveType.Sphere,shoulder[side],new Vector3(.32f,.28f,.34f),silver);
                    var back=Upper(PrimitiveType.Cube,new Vector3(sign*.19f,2.035f,-.225f),new Vector3(.43f,.08f,.05f),gold);
                    back.localRotation=Quaternion.Euler(0,0,sign*25);
                }
                Part(head,PrimitiveType.Cube,new Vector3(0,-.16f,.24f),new Vector3(.14f,.018f,.02f),purple);
                Upper(PrimitiveType.Sphere,new Vector3(0,1.94f,.304f),new Vector3(.18f,.22f,.075f),gold);
                Upper(PrimitiveType.Sphere,new Vector3(0,1.94f,.35f),new Vector3(.115f,.15f,.045f),cyan);
            }
            else
            {
                for(int side=0;side<2;side++)
                {
                    float sign=side==0?-1:1;
                    Part(head,PrimitiveType.Sphere,new Vector3(sign*.145f,.04f,.235f),new Vector3(.17f,.11f,.055f),eyes);
                    var horn=Part(head,PrimitiveType.Capsule,new Vector3(sign*.22f,.33f,-.03f),new Vector3(.11f,.21f,.12f),gold);
                    horn.localRotation=Quaternion.Euler(-18,0,sign*-28);
                    Upper(PrimitiveType.Sphere,shoulder[side],new Vector3(.43f,.38f,.42f),armor);
                }
                Part(head,PrimitiveType.Sphere,new Vector3(0,-.15f,.18f),new Vector3(.45f,.21f,.3f),armor);
                for(int i=0;i<4;i++) Upper(PrimitiveType.Sphere,new Vector3(0,1.48f+i*.16f,.25f),new Vector3(.55f-i*.035f,.12f,.12f),gold);
                for(int i=0;i<4;i++)
                {
                    var spine=Upper(PrimitiveType.Cube,new Vector3(0,1.45f+i*.22f,-.28f),new Vector3(.14f,.18f,.24f),armor);
                    spine.localRotation=Quaternion.Euler(32,0,0);
                    tail[i]=Part(Root,PrimitiveType.Sphere,new Vector3(0,.6f-i*.10f,-.28f-i*.23f),Vector3.one*(.32f-i*.045f),dark);
                }
            }
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                for(int section=0;section<2;section++)
                {
                    arms[side*2+section]=Part(torso,PrimitiveType.Capsule,Vector3.zero,Vector3.one,monster?armor:section==0?(side==0?red:purple):silver);
                    legs[side*2+section]=Part(Root,PrimitiveType.Capsule,Vector3.zero,Vector3.one,monster?dark:section==0?(side==0?red:purple):silver);
                }
                elbows[side]=Part(torso,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.19f,monster?gold:silver);
                fists[side]=Part(torso,PrimitiveType.Sphere,Vector3.zero,new Vector3(.23f,.24f,.30f),monster?gold:silver);
                feet[side]=Part(Root,PrimitiveType.Sphere,new Vector3(sign*.23f,.13f,.08f),new Vector3(.30f,.22f,.48f),main);
            }
        }
        public static Material Material(Color color,float metallic=0,bool emission=false)
        {
            var template=Resources.Load<Material>("PrototypeSurface");
            var material=template?new Material(template):new Material(Shader.Find("Standard"));material.color=color;
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",.6f);
            if(emission) { material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*1.5f); }
            return material;
        }
        Transform Upper(PrimitiveType type,Vector3 position,Vector3 scale,Material material) => Part(torso,type,position-pivot,scale,material);
        static Transform Part(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type);obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
            Object.Destroy(obj.GetComponent<Collider>());return obj.transform;
        }
        static void Bone(Transform bone,Vector3 a,Vector3 b,float radius)
        { bone.localPosition=(a+b)*.5f;bone.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);bone.localScale=new Vector3(radius,(b-a).magnitude*.5f,radius); }
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
                Bone(arms[side*2],shoulder[side]-pivot,solvedElbow-pivot,.19f);
                Bone(arms[side*2+1],solvedElbow-pivot,solvedEnd-pivot,.17f);
                elbows[side].localPosition=solvedElbow-pivot;fists[side].localPosition=solvedEnd-pivot;
                fists[side].localRotation=Quaternion.FromToRotation(Vector3.forward,solvedEnd-solvedElbow);
                Vector3 hip=Root.InverseTransformPoint(torso.TransformPoint(new Vector3(sign*.19f,0,0)));
                float step=punch && side==(game.Action==HeroAction.LeftPunch?1:0)?strike:0;
                Vector3 ankle=new Vector3(sign*.26f,.19f,side==0?.16f:-.16f);ankle+=new Vector3(0,step*.055f,step*.35f);
                Vector3 knee=new Vector3(sign*.25f,.7f,.12f+step*.2f);Solve(hip,ref knee,ref ankle,.54f,.56f);
                Bone(legs[side*2],hip,knee,.26f);Bone(legs[side*2+1],knee,ankle,.22f);
                feet[side].localPosition=ankle+new Vector3(0,-.055f,.11f);
            }
            if(monster)
            {
                for(int i=0;i<tail.Length;i++) tail[i].localPosition=new Vector3(Mathf.Sin(time*2-i*.45f)*.1f*(i+1),.6f-i*.1f,-.28f-i*.23f);
                Root.localScale=Vector3.one*(celebration?Mathf.Max(.04f,Root.localScale.x-dt*.5f):1.15f);
            }
        }
    }
}
