using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Authored action illustrations plus timing, recoil and follow-through; camera gestures drive Battle.
    public sealed class AnimatedActor
    {
        public readonly Transform Root;
        readonly Transform picture;
        readonly Material material;
        readonly bool monster;
        readonly float cellHeight;
        readonly float[] baseline=new float[8];
        readonly Vector3 home,forwardAxis;
        float lastHealth,hitAge=10,phaseAge;
        bool heavyHit;
        GamePhase previous;
        int displayed=-1;
        public int Frame => displayed;
        public void SetPresentationOpacity(float opacity)
        { var tint=material.GetColor("_Tint");tint.a*=Mathf.Clamp01(opacity);material.SetColor("_Tint",tint); }
        public static readonly string[] HeroPoses={"战斗准备","收拳蓄力","挥拳出击","光之护盾","哉佩利敖光线","受击恢复","举手变身","胜利欢呼"};
        public static readonly string[] MonsterPoses={"准备","蓄力","反击","预警","恢复","受击","光线命中","挥手退场"};
        public AnimatedActor(string name,Vector3 position,Vector3 opponentPosition,bool isMonster=false)
        {
            monster=isMonster;home=position;
            forwardAxis=Vector3.ProjectOnPlane(opponentPosition-position,Vector3.up).normalized;
            Root=new GameObject(name).transform;Root.position=home;
            var texture=Resources.Load<Texture2D>(monster?"Art/GolzaActions":"Art/TigaRear45Actions");
            if(!texture)throw new System.InvalidOperationException("Missing character action atlas: "+name);
            material=new Material(Resources.Load<Shader>("CharacterSprite"));material.mainTexture=texture;
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name="Animated illustration";
            picture=obj.transform;picture.SetParent(Root,false);obj.GetComponent<Renderer>().sharedMaterial=material;
            obj.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            obj.GetComponent<Renderer>().receiveShadows=false;
            if(Application.isPlaying)Object.Destroy(obj.GetComponent<Collider>());else Object.DestroyImmediate(obj.GetComponent<Collider>());
            cellHeight=4.85f;
            // Determine the feet baseline once; generated poses can sit at different heights in their cells.
            // This only positions the original atlas, it does not modify its pixels.
            var pixels=texture.GetPixels32();int width=texture.width/4,height=texture.height/2;
            for(int frame=0;frame<8;frame++)
            {
                int x=(frame%4)*width,y=frame<4?height:0;bool found=false;
                for(int row=0;row<height&&!found;row++)
                {
                    int solid=0;
                    for(int column=0;column<width;column++)
                    {var c=pixels[(y+row)*texture.width+x+column];if(c.g-Mathf.Max(c.r,c.b)<32&&c.a>128)solid++;}
                    if(solid>=4) {baseline[frame]=row/(float)height;found=true;}
                }
                if(!found)throw new System.InvalidOperationException("Empty character frame "+frame+": "+name);
            }
            float aspect=(texture.width/4f)/(texture.height/2f);
            picture.localScale=new Vector3(cellHeight*aspect,cellHeight,1);
            SetFrame(0);
            Debug.Log($"[AnimatedActor] name={name} atlas={texture.width}x{texture.height} frames=8");
        }
        public static float Strike(float age)
        {
            if(age<Battle.PunchHitSeconds)return Mathf.SmoothStep(0,1,age/Battle.PunchHitSeconds);
            return 1-Mathf.SmoothStep(0,1,(age-Battle.PunchHitSeconds)/(Battle.PunchSeconds-Battle.PunchHitSeconds));
        }
        public static float MonsterAdvance(Battle state)
        {
            if(state.Phase!=GamePhase.Battle)return 0;
            if(state.Enemy==EnemyPhase.Windup)
                return -.16f*Mathf.SmoothStep(0,1,state.EnemyAge/state.WarningDuration);
            if(state.Enemy!=EnemyPhase.Attack)return 0;
            float age=state.EnemyAge;
            if(age<Battle.EnemyHitSeconds)return Mathf.Lerp(-.16f,2.25f,Mathf.SmoothStep(0,1,age/Battle.EnemyHitSeconds));
            if(age<Battle.EnemyHitSeconds+.12f)return 2.25f;
            return 2.25f*(1-Mathf.SmoothStep(0,1,(age-Battle.EnemyHitSeconds-.12f)/(Battle.EnemyAttackSeconds-Battle.EnemyHitSeconds-.12f)));
        }
        void SetFrame(int frame)
        {
            if(frame==displayed)return;
            displayed=frame;material.SetVector("_Frame",new Vector4((frame%4)*.25f,frame<4?.5f:0,.25f,.5f));
        }
        public void Update(Battle state,Camera camera,float dt,float time,int preview=-1)
        {
            if(previous!=state.Phase) {previous=state.Phase;phaseAge=0;}
            phaseAge+=dt;hitAge+=dt;
            if(state.EnemyHealth<lastHealth) {hitAge=0;heavyHit=lastHealth-state.EnemyHealth>1;}
            lastHealth=state.EnemyHealth;
            bool fighting=state.Phase==GamePhase.Battle;
            bool punch=fighting&&(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch);
            int frame=0;float forward=0,tilt=0,jump=0,scale=1,opacity=1;
            float breath=Mathf.Sin(time*(monster?2.3f:2.9f))*.006f;
            if(monster)
            {
                if(state.Phase==GamePhase.Victory)
                {frame=7;forward=-Mathf.SmoothStep(0,1,phaseAge/2)*1.8f;opacity=1-Mathf.SmoothStep(0,1,(phaseAge-1)/2);}
                else if(fighting&&state.Enemy==EnemyPhase.Attack)
                {
                    float age=state.EnemyAge;forward=MonsterAdvance(state);
                    frame=age<.13f?1:age<Battle.EnemyHitSeconds+.15f?2:4;
                    tilt=forward*2;breath=0;
                    jump=age<Battle.EnemyHitSeconds?Mathf.Sin(age/Battle.EnemyHitSeconds*Mathf.PI)*.08f:0;
                }
                else if(fighting&&hitAge<.35f)
                {frame=heavyHit?6:5;forward=-Mathf.Sin(hitAge/.35f*Mathf.PI)*.22f;tilt=-Mathf.Sin(hitAge/.35f*Mathf.PI)*5;}
                else if(fighting&&state.Enemy==EnemyPhase.Windup)
                {frame=state.EnemyAge<.3f?1:3;forward=MonsterAdvance(state);tilt=-3*Mathf.Clamp01(state.EnemyAge/state.WarningDuration);breath=Mathf.Sin(time*8)*.012f;}
                else if(fighting&&state.Enemy==EnemyPhase.Recover)
                {frame=state.EnemyAge<.55f?4:0;}
            }
            else
            {
                if(state.Phase==GamePhase.Transforming)
                {frame=6;scale=1+Mathf.Sin(phaseAge*5)*.012f;}
                else if(state.Phase==GamePhase.Victory)
                {frame=7;jump=Mathf.Abs(Mathf.Sin(Mathf.Min(phaseAge,2)*Mathf.PI))*.12f;}
                else if(fighting&&state.Action==HeroAction.Hurt)
                {frame=5;forward=-Mathf.Sin(Mathf.Clamp01(state.ActionAge/.55f)*Mathf.PI)*.18f;tilt=3;}
                else if(fighting&&state.Action==HeroAction.Beam)
                {frame=4;forward=.1f;}
                else if(fighting&&state.Shield)frame=3;
                else if(punch)
                {
                    frame=state.ActionAge<.065f?1:state.ActionAge<.25f?2:1;
                    forward=Strike(state.ActionAge)*1.45f;tilt=-Strike(state.ActionAge)*2;
                }
            }
            if(preview>=0) {frame=preview%8;forward=tilt=jump=0;opacity=scale=1;breath=0;}
            SetFrame(frame);
            Root.position=home+forwardAxis*forward+Vector3.up*jump;
            // The rear/front angle is authored into the atlas; the quad faces the camera for readability.
            Root.rotation=camera.transform.rotation*Quaternion.Euler(0,0,tilt);
            Root.localScale=new Vector3(scale,scale*(1+breath),1);
            picture.localPosition=new Vector3(0,cellHeight*(.5f-baseline[frame]),0);
            material.SetColor("_Tint",new Color(1,1,1,opacity));
        }
    }
}
