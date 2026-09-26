using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Samples baked skeletal clips against the same clocks that resolve combat damage.
    // No Blender constraints, drivers, scripts or camera data are needed at runtime.
    public sealed class RiggedActor
    {
        public readonly Transform Root;
        public int Frame { get; private set; }
        public int BoneCount { get; private set; }
        public float StrikeAdvance {get;private set;}=AnimatedActor.PunchAdvance;
        [Serializable] sealed class MotionTuning {public float punchAdvance=AnimatedActor.PunchAdvance;}
        readonly GameObject model;
        readonly bool monster;
        readonly Vector3 home, forward;
        readonly Dictionary<string, AnimationClip> clips=new Dictionary<string, AnimationClip>();
        readonly Transform[] joints;
        readonly Renderer[] surfaces;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;
        readonly List<Material> materials=new List<Material>();
        readonly List<Material> eyeMaterials=new List<Material>();
        readonly List<Material> coreMaterials=new List<Material>();
        readonly List<Material> impactMaterials=new List<Material>();
        string playing;
        float clipAge, phaseAge, blendLeft, hitAge=10, lastHealth, poseOpacity=1;
        float heroRecoveryAge=10;
        HeroAction observedAction=HeroAction.None;
        int lastPunchSide;
        GamePhase previous;
        bool heavyHit;
        Transform hand,leftHand,forearm,leftForearm,leftUpperArm,upperArm,rightFoot,leftFoot;
        Transform head,upperSpine,pelvis;
        Vector3 beamContactLocal;
        Transform leftThigh,rightThigh,leftShin,rightShin;
        Quaternion leftFootRest,rightFootRest;
        Vector3 leftFootLocal,rightFootLocal;
        float leftFootClearance,rightFootClearance;
        Quaternion headBase,spineBase;
        bool contactLayerApplied;
        float contactSide;
        Battle observedBattle;
        int observedBlocks;
        float guardAge=10;
        readonly Dictionary<string,Transform> clawBones=new Dictionary<string,Transform>();
        readonly Transform[,,] clawFingers=new Transform[4,2,2];
        readonly Transform[,] clawThumbs=new Transform[2,2];
        readonly Vector3[] palmForwardLocal=new Vector3[2],palmUpLocal=new Vector3[2];
        static readonly string[] ClawFingerNames={"index","middle","ring","pinky"};
        Transform[] tailJoints;Quaternion[] tailRest;Vector3[] tailPositions;Quaternion tailRootRotation;float tailHeight;
        public Vector3 StrikeOrigin(HeroAction action) => action==HeroAction.LeftPunch&&leftHand?leftHand.position:HandPosition;
        public Vector3 HandPosition => hand?hand.position:Root.position+Vector3.up*2.2f;
        public Vector3 EnemyStrikeOrigin(int attackCount) => attackCount%2==0&&leftHand?leftHand.position:HandPosition;
        public Vector3 BeamOrigin => hand&&forearm?Vector3.Lerp(forearm.position,hand.position,.6f):HandPosition;
        public Vector3 BeamContact => upperSpine?upperSpine.TransformPoint(beamContactLocal):Root.position+Vector3.up*2.48f;
        public Vector3 FootPosition(bool left) => (left?leftFoot:rightFoot)?(left?leftFoot:rightFoot).position:Root.position;
        public Vector3 GroundContactPosition => pelvis?pelvis.position:Root.position;

        public static RiggedActor CreateIfAvailable(string name,Vector3 position,Vector3 opponent,bool monster)
        {
            string path="Characters/"+name+"/"+name;
            var prefab=Resources.Load<GameObject>(path);
            return prefab?new RiggedActor(name,path,prefab,position,opponent,monster):null;
        }
        RiggedActor(string name,string path,GameObject prefab,Vector3 position,Vector3 opponent,bool isMonster)
        {
            monster=isMonster;home=position;forward=Vector3.ProjectOnPlane(opponent-position,Vector3.up).normalized;
            var motion=Resources.Load<TextAsset>("Characters/"+name+"/motion");
            if(motion)
            {
                var tuning=JsonUtility.FromJson<MotionTuning>(motion.text);
                if(tuning==null||float.IsNaN(tuning.punchAdvance)||tuning.punchAdvance<.25f||tuning.punchAdvance>1.5f)
                    throw new InvalidOperationException(name+" has invalid authored punch travel");
                StrikeAdvance=tuning.punchAdvance;
            }
            Root=new GameObject(name+" skeletal actor").transform;
            var placement=new GameObject("Model placement").transform;placement.SetParent(Root,false);
            model=UnityEngine.Object.Instantiate(prefab,placement,false);model.name="Character";
            foreach(var animation in model.GetComponentsInChildren<Animation>())animation.enabled=false;
            foreach(var animator in model.GetComponentsInChildren<Animator>())animator.enabled=false;
            foreach(var clip in Resources.LoadAll<AnimationClip>(path))
            {
                if(clip.name.StartsWith("__preview__",StringComparison.Ordinal))continue;
                string key=clip.name.Substring(clip.name.LastIndexOf('|')+1);
                clips[key]=clip;
            }
            foreach(string required in monster?new[]{"Idle","Windup","WindupAlt","Attack","AttackAlt","Hurt","Defeat"}:
                new[]{"Idle","LeftPunch","RightPunch","Guard","Beam","Hurt","Transform","Victory"})
                if(!clips.ContainsKey(required))throw new InvalidOperationException(name+" is missing animation "+required);
            clips["Idle"].SampleAnimation(model,0);
            var renderers=model.GetComponentsInChildren<Renderer>();surfaces=renderers;
            if(renderers.Length==0)throw new InvalidOperationException(name+" has no character mesh");
            // Imported skin bounds include every clip and can be much larger than the body.
            // useScale=true compensates for the skin scale, producing mesh-local vertices.
            Bounds bounds=default;bool first=true;
            foreach(var renderer in renderers)
            {
                if(renderer is SkinnedMeshRenderer skin)
                {
                    var baked=new Mesh();skin.BakeMesh(baked,true);
                    foreach(var vertex in baked.vertices)
                    {
                        var point=skin.transform.TransformPoint(vertex);
                        if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                    }
                    if(Application.isPlaying)UnityEngine.Object.Destroy(baked);else UnityEngine.Object.DestroyImmediate(baked);
                }
                else {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            }
            float size=(monster?3.6f:3.45f)/Mathf.Max(.01f,bounds.size.y);
            Vector3 center=bounds.center;
            if(monster)
                foreach(var bone in model.GetComponentsInChildren<Transform>())
                    if(bone.name=="bip_pelvis"){center=bone.position;break;}
            placement.localScale=Vector3.one*size;
            placement.localPosition=-new Vector3(center.x,bounds.min.y,center.z)*size;
            var texture=Resources.Load<Texture2D>("Characters/"+name+"/"+name+"Body");
            var eyes=Resources.Load<Texture2D>("Characters/"+name+"/"+name+"Eyes");
            var materialCache=new Dictionary<string,Material>();
            foreach(var renderer in renderers)
            {
                if(renderer is SkinnedMeshRenderer skin) {skin.updateWhenOffscreen=true;BoneCount=Mathf.Max(BoneCount,skin.bones.Length);}
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                var mapped=renderer.sharedMaterials;
                for(int i=0;i<mapped.Length;i++)
                {
                    string key=mapped[i]?mapped[i].name:"Surface";
                    if(!materialCache.TryGetValue(key,out var mat))
                    {mat=RuntimeResources.Own(Root,Surface(key,texture,eyes,name));materialCache[key]=mat;materials.Add(mat);}
                    if(key.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0)
                        if(!eyeMaterials.Contains(mat))eyeMaterials.Add(mat);
                    if(key.IndexOf("Crystal",StringComparison.OrdinalIgnoreCase)>=0||key.IndexOf("Timer",StringComparison.OrdinalIgnoreCase)>=0||key.IndexOf("EyesGlow",StringComparison.OrdinalIgnoreCase)>=0)
                        if(!coreMaterials.Contains(mat))coreMaterials.Add(mat);
                    if((monster&&key.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)<0&&key.IndexOf("EyesGlow",StringComparison.OrdinalIgnoreCase)<0)||
                       (!monster&&(key.IndexOf("Suit",StringComparison.OrdinalIgnoreCase)>=0||key.IndexOf("Gold",StringComparison.OrdinalIgnoreCase)>=0)))
                    {
                        if(mat.HasProperty("_EmissionColor")){mat.EnableKeyword("_EMISSION");if(!impactMaterials.Contains(mat))impactMaterials.Add(mat);}
                    }
                    mapped[i]=mat;
                }
                renderer.sharedMaterials=mapped;
            }
            joints=model.GetComponentsInChildren<Transform>();
            foreach(var joint in joints)
            {
                joint.gameObject.layer=ContactShadows.ActorLayer;
                if(joint.name=="HandBase_R"||joint.name=="bip_hand_R")hand=joint;
                if(joint.name=="ForearmBase_R"||joint.name=="bip_lowerArm_R")forearm=joint;
                if(joint.name=="HandBase_L"||joint.name=="bip_hand_L")leftHand=joint;
                if(joint.name=="bip_upperArm_L"||joint.name=="armBase_L")leftUpperArm=joint;
                if(joint.name=="bip_upperArm_R"||joint.name=="armBase_R")upperArm=joint;
                if(joint.name=="bip_lowerArm_L"||joint.name=="ForearmBase_L")leftForearm=joint;
                if(joint.name=="bip_lowerArm_R"||joint.name=="ForearmBase_R")forearm=joint;
                if(joint.name=="Foot_L"||joint.name=="bip_foot_L")leftFoot=joint;
                if(joint.name=="Foot_R"||joint.name=="bip_foot_R")rightFoot=joint;
                if(joint.name=="hip"||joint.name=="bip_pelvis")pelvis=joint;
                if(joint.name=="ThighBase_L"||joint.name=="bip_hip_L")leftThigh=joint;
                if(joint.name=="ThighBase_R"||joint.name=="bip_hip_R")rightThigh=joint;
                if(joint.name=="Shin_L"||joint.name=="bip_knee_L")leftShin=joint;
                if(joint.name=="Shin_R"||joint.name=="bip_knee_R")rightShin=joint;
                if(monster&&joint.name=="bip_head")head=joint;
                if(monster&&joint.name=="bip_spine_2")upperSpine=joint;
                if(!monster&&(joint.name=="head"||joint.name=="bip_head"))head=joint;
                if(!monster&&(joint.name=="spineLower"||joint.name=="bip_spine_0"))upperSpine=joint;
                if(monster&&(joint.name.StartsWith("bip_index_",StringComparison.Ordinal)||
                    joint.name.StartsWith("bip_middle_",StringComparison.Ordinal)||
                    joint.name.StartsWith("bip_ring_",StringComparison.Ordinal)||
                    joint.name.StartsWith("bip_pinky_",StringComparison.Ordinal)||
                    joint.name.StartsWith("bip_thumb_",StringComparison.Ordinal)))
                    clawBones[joint.name]=joint;
            }
            if(!hand||!leftHand)throw new InvalidOperationException(name+" is missing a left or right strike bone");
            if(monster)
            {
                for(int side=0;side<2;side++)
                {
                    string suffix=side==0?"L":"R";
                    for(int finger=0;finger<ClawFingerNames.Length;finger++)
                        for(int segment=0;segment<2;segment++)
                            clawBones.TryGetValue($"bip_{ClawFingerNames[finger]}_{segment}_{suffix}",out clawFingers[finger,side,segment]);
                    for(int segment=0;segment<2;segment++)
                        clawBones.TryGetValue($"bip_thumb_{segment}_{suffix}",out clawThumbs[side,segment]);
                    var wrist=side==0?leftHand:hand;
                    Vector3 palmCenter=Vector3.zero;
                    for(int finger=0;finger<4;finger++)palmCenter+=clawFingers[finger,side,0].position;
                    Vector3 direction=(palmCenter/4-wrist.position).normalized;
                    Vector3 across=clawFingers[0,side,0].position-clawFingers[3,side,0].position;
                    Vector3 normal=Vector3.Cross(direction,across).normalized*(side==0?1:-1);
                    palmForwardLocal[side]=wrist.InverseTransformDirection(direction);
                    palmUpLocal[side]=wrist.InverseTransformDirection(normal);
                }
                var tails=new List<Transform>();foreach(var joint in joints)if(joint.name.StartsWith("tail_",StringComparison.Ordinal))tails.Add(joint);
                tails.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));tailJoints=tails.ToArray();tailRest=new Quaternion[tailJoints.Length];tailPositions=new Vector3[tailJoints.Length];
                for(int i=0;i<tailJoints.Length;i++){tailRest[i]=tailJoints[i].localRotation;tailPositions[i]=tailJoints[i].localPosition;}
                if(tailJoints.Length>0){tailHeight=Root.InverseTransformPoint(tailJoints[0].position).y;tailRootRotation=Quaternion.Inverse(Root.rotation)*tailJoints[0].rotation;}
            }
            positions=new Vector3[joints.Length];scales=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];
            Root.position=home;Root.rotation=Quaternion.LookRotation(forward,Vector3.up);
            // A point on the front of the resting chest, carried by its sampled
            // bone through recoil. Root/home coordinates drift off the skin.
            if(upperSpine)beamContactLocal=upperSpine.InverseTransformPoint(home+Vector3.up*2.48f+forward*.33f);
            if(leftFoot)leftFootRest=Quaternion.Inverse(Root.rotation)*leftFoot.rotation;
            if(rightFoot)rightFootRest=Quaternion.Inverse(Root.rotation)*rightFoot.rotation;
            if(leftFoot)leftFootLocal=Root.InverseTransformPoint(leftFoot.position);
            if(rightFoot)rightFootLocal=Root.InverseTransformPoint(rightFoot.position);
            leftFootClearance=leftFoot?Mathf.Max(.16f,leftFoot.position.y-home.y+.025f):.16f;
            rightFootClearance=rightFoot?Mathf.Max(.16f,rightFoot.position.y-home.y+.025f):.16f;
            Debug.Log($"[RiggedActor] name={name} clips={clips.Count} bones={BoneCount} renderers={renderers.Length} height={bounds.size.y*size:F2}");
        }
        static Material Surface(string name,Texture2D texture,Texture2D eyes,string character)
        {
            bool kaiju=name.StartsWith("Golza",StringComparison.Ordinal)&&!name.Contains("Eyes");
            var mat=kaiju?new Material(Resources.Load<Shader>("KaijuSurface")):new Material(Resources.Load<Material>("PrototypeSurface"));mat.name=name;
            mat.color=new Color(.72f,.77f,.85f);mat.SetFloat("_Metallic",.65f);mat.SetFloat("_Glossiness",.55f);
            if(name.StartsWith("Golza",StringComparison.Ordinal))
            {
                bool eye=name.Contains("Eyes");mat.mainTexture=eye?eyes:texture;mat.color=Color.white;
                mat.SetFloat("_Metallic",.03f);mat.SetFloat("_Glossiness",.22f);
                if(eye){mat.EnableKeyword("_EMISSION");mat.SetTexture("_EmissionMap",eyes);mat.SetColor("_EmissionColor",new Color(.55f,.35f,.15f));}
            }
            else if(name.Contains("Suit")) {mat.mainTexture=texture;mat.color=Color.white;mat.SetFloat("_Metallic",.12f);mat.SetFloat("_Glossiness",.32f);}
            else if(name.Contains("Gold")) {mat.color=new Color(.78f,.55f,.18f);mat.SetFloat("_Metallic",.7f);}
            else if(name.Contains("EyeRim")) {mat.color=new Color(.03f,.04f,.05f);mat.SetFloat("_Metallic",.3f);}
            else if(name.Contains("EyesGlow")||name.Contains("Timer")||name.Contains("Crystal"))
            {
                var color=name.Contains("EyesGlow")?new Color(1,.85f,.48f):new Color(.12f,.65f,1);
                mat.color=color;mat.SetFloat("_Metallic",.1f);mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*1.5f);
            }
            var rosterTexture=Resources.Load<Texture2D>("Characters/"+character+"/Textures/"+name);
            if(rosterTexture)
            {
                mat.mainTexture=rosterTexture;mat.color=Color.white;mat.SetFloat("_Metallic",.2f);mat.SetFloat("_Glossiness",.42f);
                if(name.ToLowerInvariant().Contains("eye")||name.ToLowerInvariant().Contains("timer"))
                {mat.EnableKeyword("_EMISSION");mat.SetTexture("_EmissionMap",rosterTexture);mat.SetColor("_EmissionColor",Color.white*.5f);}
            }
            return mat;
        }
        public void SetPresentationOpacity(float opacity)
        {
            float alpha=poseOpacity*Mathf.Clamp01(opacity);
            foreach(var surface in surfaces)surface.enabled=alpha>.001f;
            foreach(var mat in materials)
            {
                var color=mat.color;color.a=alpha;mat.color=color;
                bool fade=alpha<.999f;
                mat.SetInt("_SrcBlend",(int)(fade?BlendMode.SrcAlpha:BlendMode.One));
                mat.SetInt("_DstBlend",(int)(fade?BlendMode.OneMinusSrcAlpha:BlendMode.Zero));
                mat.SetInt("_ZWrite",fade?0:1);
                if(fade)mat.EnableKeyword("_ALPHABLEND_ON");else mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue=fade?(int)RenderQueue.Transparent:-1;
            }
        }
        public void Update(Battle state,float dt,float time,int preview=-1)
        {
            // Blends start from the sampled clip, never from last frame's
            // additive impact. Otherwise the same impulse feeds back into itself.
            if(contactLayerApplied)
            {
                if(upperSpine)upperSpine.localRotation=spineBase;
                if(head)head.localRotation=headBase;
                contactLayerApplied=false;
            }
            if(previous!=state.Phase) {previous=state.Phase;phaseAge=0;}
            if(!monster)
            {
                if(state.Phase!=GamePhase.Battle)
                {heroRecoveryAge=10;observedAction=state.Action;}
                else
                {
                    bool wasPunch=observedAction==HeroAction.LeftPunch||observedAction==HeroAction.RightPunch;
                    if(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)
                        lastPunchSide=state.Action==HeroAction.LeftPunch?-1:1;
                    if(state.Action==HeroAction.None&&wasPunch)heroRecoveryAge=0;
                    else heroRecoveryAge+=dt;
                    observedAction=state.Action;
                }
            }
            // A block is a contact event, not the held guard input. Observe it
            // once per round so a held shield, pause or photo restart cannot
            // replay the recoil. The layer affects only the torso above the hips.
            if(!ReferenceEquals(observedBattle,state))
            {observedBattle=state;observedBlocks=state.Blocks;guardAge=10;}
            if(state.Phase!=GamePhase.Battle)guardAge=10;
            else if(state.Blocks>observedBlocks)guardAge=0;
            else guardAge+=dt;
            observedBlocks=state.Blocks;
            float guardRecoil=ContactPulse(guardAge,0,.09f,.54f);
            phaseAge+=dt;hitAge+=dt;
            if(state.EnemyHealth<lastHealth)
            {hitAge=0;heavyHit=lastHealth-state.EnemyHealth>1;contactSide=state.Action==HeroAction.LeftPunch?-1:state.Action==HeroAction.RightPunch?1:0;}
            lastHealth=state.EnemyHealth;
            string next="Idle";float sample=time%clips["Idle"].length,travel=0,opacity=1,fallTilt=0,fallSide=0,fallDrop=0;
            Frame=0;
            if(monster)
            {
                if(state.Phase==GamePhase.Transforming&&clips.ContainsKey("Walk"))
                {next="Walk";sample=phaseAge%clips["Walk"].length;travel=-.45f*(1-Mathf.SmoothStep(0,1,phaseAge/2.2f));}
                else if(state.Phase==GamePhase.Victory)
                {
                    next="Defeat";sample=Mathf.Min(1.5f,phaseAge);Frame=7;
                    float collapse=VictoryMotion.Collapse(phaseAge);
                    float stagger=Mathf.Sin(Mathf.Clamp01(phaseAge/.5f)*Mathf.PI);
                    fallTilt=-12f*stagger+20f*collapse;fallSide=-6f*collapse;
                    opacity=VictoryMotion.Opacity(phaseAge);
                }
                else if(state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack) {next=state.EnemyAttackCount%2==0?"AttackAlt":"Attack";sample=state.EnemyAge;Frame=2;travel=AnimatedActor.MonsterAdvance(state);}
                else if(state.Phase==GamePhase.Battle&&hitAge<(heavyHit?.9f:.4f))
                {
                    next="Hurt";sample=heavyHit&&hitAge>.14f?Mathf.Lerp(.14f,.4f,(hitAge-.14f)/.76f):hitAge;Frame=heavyHit?6:5;
                    // The baked pelvis/legs already absorb the hit. Keep the
                    // actor root upright so both feet retain their planted pose;
                    // the chest and head supply directional follow-through below.
                }
                else if(state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Windup)
                {
                    next=(state.EnemyAttackCount+1)%2==0?"WindupAlt":"Windup";
                    // Hold a readable warning pose while the child listens; complete the
                    // anticipation during the last second instead of stretching every key.
                    sample=state.EnemyAge<.4f?state.EnemyAge:Mathf.Lerp(.4f,clips[next].length,
                        Mathf.Clamp01((state.EnemyAge-state.WarningDuration+1)/1));
                    Frame=3;travel=AnimatedActor.MonsterAdvance(state);
                }
            }
            else if(state.Phase==GamePhase.Transforming) {next="Transform";sample=phaseAge;Frame=6;}
            else if(state.Phase==GamePhase.Victory) {next="Victory";sample=Mathf.Max(0,phaseAge-VictoryMotion.TurnStartSeconds);Frame=7;}
            else if(state.Phase==GamePhase.Battle)
            {
                if(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)
                {next=state.Action==HeroAction.LeftPunch?"LeftPunch":"RightPunch";sample=state.ActionAge;Frame=sample<.07f?1:2;travel=AnimatedActor.Strike(sample)*StrikeAdvance;}
                else if(state.Action==HeroAction.Beam) {next="Beam";sample=Mathf.Min(1.9f,playing==next?clipAge+dt:0);Frame=4;}
                else if(state.Action==HeroAction.Hurt)
                {
                    next="Hurt";sample=KnockdownMotion.ClipSeconds(state.ActionAge);Frame=5;
                    float p=KnockdownMotion.Weight(state.ActionAge);
                    fallTilt=-62f*p;fallSide=20f*p;
                }
                else if(state.Shield) {next="Guard";sample=playing==next?clipAge+dt:0;Frame=3;}
            }
            if(preview>=0)
            {
                Frame=preview%8;travel=0;opacity=1;
                string[] names=monster?new[]{"Idle","Windup","Attack","Windup","Idle","Hurt","Hurt","Defeat"}:
                    new[]{"Idle","RightPunch","RightPunch","Guard","Beam","Hurt","Transform","Victory"};
                float[] moments=monster?new[]{0,.15f,.4f,.8f,0,.15f,.3f,.5f}:new[]{0,.045f,.12f,.3f,.85f,.15f,1.2f,.8f};
                next=names[Frame];sample=moments[Frame];
            }
            if(playing!=next)
            {
                playing=next;clipAge=0;
                // Punch clips already start at the combat stance. A long blend
                // delays their baked foot compensation while the actor root
                // advances, sliding the support foot during the first step.
                blendLeft=!monster&&(next=="LeftPunch"||next=="RightPunch")?.025f:.055f;
            }
            else clipAge+=dt;
            for(int i=0;i<joints.Length;i++) {positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;scales[i]=joints[i].localScale;}
            clips[next].SampleAnimation(model,Mathf.Clamp(sample,0,clips[next].length));
            if(monster&&preview<0)CorrectRestingArms(state);
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack)
                CorrectAttackArms(state);
            if(monster)ApplyClawPose(state,preview,time);
            float mix=preview>=0||dt<=0||blendLeft<=0?1:Mathf.Clamp01(dt/blendLeft);
            blendLeft=Mathf.Max(0,blendLeft-dt);
            for(int i=1;i<joints.Length&&mix<1;i++)
            {joints[i].localPosition=Vector3.Lerp(positions[i],joints[i].localPosition,mix);joints[i].localRotation=Quaternion.Slerp(rotations[i],joints[i].localRotation,mix);joints[i].localScale=Vector3.Lerp(scales[i],joints[i].localScale,mix);}
            // Clip blending changes the elbow and palm together. Limit the
            // resulting pose, including the transition frames between clips.
            if(monster)AlignClawWrists();
            Root.position=home+forward*travel+Vector3.down*fallDrop;
            Root.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(fallTilt,0,fallSide);
            if(preview<0&&state.Phase==GamePhase.Victory)
            {
                if(monster)PoseDefeat(VictoryMotion.Collapse(phaseAge));
                else PoseVictoryTurn(VictoryMotion.Turn(phaseAge));
            }
            if(!monster&&preview<0&&state.Phase==GamePhase.Battle&&next=="Hurt")
                PoseKnockdown(KnockdownMotion.Weight(state.ActionAge));
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&next=="Hurt")
            {
                // The source Hurt clip supplies the chest recoil, but its root
                // stays fixed. A short, planted-foot backstep gives each punch
                // a readable weight transfer and lets the following recovery
                // settle back into the diagonal arena composition.
                float recoil=ContactPulse(hitAge,0,heavyHit?.10f:.065f,heavyHit?.78f:.36f);
                Root.position-=forward*(heavyHit?.14f:.085f)*recoil;
                Root.position+=Vector3.Cross(Vector3.up,forward)*(contactSide*(heavyHit?.045f:.028f)*recoil);
                Root.rotation*=Quaternion.AngleAxis(contactSide*(heavyHit?4.5f:2.5f)*recoil,Vector3.up);
            }
            if(!monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Action==HeroAction.None&&heroRecoveryAge<.26f)
            {
                // Keep a small follow-through after the authored punch clip ends.
                // The root returns to its home line first, then settles back from
                // contact so rapid left/right punches do not snap between poses.
                float settle=1-Mathf.SmoothStep(0,1,heroRecoveryAge/.26f);
                var right=Vector3.Cross(Vector3.up,forward);
                Root.position-=forward*(.045f*settle);
                Root.position+=right*(lastPunchSide*.018f*settle);
                Root.rotation*=Quaternion.AngleAxis(-lastPunchSide*3.2f*settle,Vector3.up);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(lastPunchSide*5.5f*settle,Vector3.up)
                        *Quaternion.AngleAxis(-2.5f*settle,right)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(-lastPunchSide*3.0f*settle,Vector3.up)*head.rotation;
                }
                contactLayerApplied=true;
            }
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack)
            {
                // The baked clip owns both hands and the planted-foot keyframes.
                // This small torso/head layer gives alternating lead claws a
                // different centre of mass, so a long exchange reads as two
                // deliberate lunges instead of one repeated pose.
                float reach=Mathf.Sin(Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI)*(1-guardRecoil*.65f);
                float side=state.EnemyAttackCount%2==0?-1:1;
                var right=Vector3.Cross(Vector3.up,forward);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(side*6*reach,Vector3.up)
                        *Quaternion.AngleAxis(-4*reach-17*guardRecoil,right)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(side*8*reach,Vector3.up)
                        *Quaternion.AngleAxis(-3*reach-6*guardRecoil,right)*head.rotation;
                }
                contactLayerApplied=true;
            }
            if(!monster&&preview<0&&state.Phase==GamePhase.Battle&&
                (state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch))
            {
                // The authored Tiga clip drives the hand and planted feet. Add a
                // restrained body lead so the strike reads as a weight transfer:
                // shoulder turns into the lane, chest drops toward contact and
                // the head follows a fraction behind. Hands remain untouched.
                float punch=AnimatedActor.Strike(state.ActionAge);
                float side=state.Action==HeroAction.LeftPunch?-1:1;
                var right=Vector3.Cross(Vector3.up,forward);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(side*7*punch,Vector3.up)
                        *Quaternion.AngleAxis(-6*punch,right)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(-side*4*punch,Vector3.up)
                        *Quaternion.AngleAxis(-2*punch,right)*head.rotation;
                }
                contactLayerApplied=true;
            }
            if(!monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Shield&&guardRecoil>0)
            {
                var right=Vector3.Cross(Vector3.up,forward);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(-13*guardRecoil,right)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    // Counter the chest tilt slightly to keep eyes on the claw.
                    head.rotation=Quaternion.AngleAxis(5*guardRecoil,right)*head.rotation;
                }
                contactLayerApplied=true;
            }
            bool heroBreath=!monster&&preview<0&&state.Phase==GamePhase.Battle&&
                state.Action==HeroAction.None&&!state.Shield&&guardRecoil<=0&&hitAge>.4f;
            bool monsterBreath=monster&&preview<0&&state.Phase==GamePhase.Battle&&
                state.Enemy==EnemyPhase.Rest&&hitAge>.4f;
            if(heroBreath||monsterBreath)
            {
                // Keep the pause between authored clips alive without moving the
                // feet or changing any combat clock. The two actors breathe out
                // of phase so a long exchange does not read as a frozen tableau.
                var right=Vector3.Cross(Vector3.up,forward);
                // Let each exchange hand off a slightly different centre of
                // mass. The authored Idle clip is short and otherwise returns
                // to the same silhouette after every hit; this small planted
                // weight shift makes a long arcade string feel continuous.
                float cadence=time*(monsterBreath?1.72f:1.95f)+state.Punches*.55f+state.EnemyAttackCount*.31f;
                float wave=Mathf.Sin(cadence+(monsterBreath?.8f:0));
                float footShift=wave*(monsterBreath?.045f:.032f);
                Root.position+=right*footShift;
                Root.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(wave*(monsterBreath?2.6f:1.9f),Vector3.up);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(wave*(monsterBreath?1.5f:1.0f),right)
                        *Quaternion.AngleAxis(wave*(monsterBreath?1.1f:.7f),Vector3.up)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(-wave*(monsterBreath?1.8f:1.25f),right)
                        *Quaternion.AngleAxis(wave*(monsterBreath?1.4f:1.0f),Vector3.up)*head.rotation;
                }
                if(heroBreath&&upperArm&&leftUpperArm)
                {
                    // Keep the hero alive during the short arcade pause. The
                    // authored idle clip leaves the shoulders almost frozen;
                    // a tiny opposing arm settle makes the stance read as a
                    // guarded fighter without changing a punch or shield pose.
                    float shoulder=wave*1.45f;
                    upperArm.rotation=Quaternion.AngleAxis(shoulder,forward)*upperArm.rotation;
                    leftUpperArm.rotation=Quaternion.AngleAxis(-shoulder,forward)*leftUpperArm.rotation;
                }
                contactLayerApplied=true;
            }
            if(monster&&preview<0&&next=="Hurt"&&state.Phase==GamePhase.Battle)
            {
                // World axes are deliberate: the mirrored Source bones do not
                // share Euler axes. Chest yields first, head follows 35 ms later,
                // then both settle. Left/right punches twist opposite shoulders.
                // A beam sustains the recoil while it is striking the chest,
                // rather than returning to Idle during the visible blast.
                float chest=ContactPulse(hitAge,0,heavyHit?.12f:.075f,heavyHit?.84f:.35f);
                float follow=ContactPulse(hitAge,.035f,heavyHit?.20f:.14f,heavyHit?.9f:.4f);
                var right=Vector3.Cross(Vector3.up,forward);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(-(heavyHit?20:9)*chest,right)
                        *Quaternion.AngleAxis(contactSide*18*chest,Vector3.up)
                        *Quaternion.AngleAxis(contactSide*4*chest,forward)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(-(heavyHit?13:7)*follow,right)
                        *Quaternion.AngleAxis(contactSide*7*follow,Vector3.up)*head.rotation;
                }
                contactLayerApplied=true;
            }
            // Keep the tail planted while the torso recoils. It follows heading
            // and travel, but not the pelvis or whole-actor backward hit pitch.
            if(tailJoints!=null&&tailJoints.Length>0)
            {
                for(int i=1;i<tailJoints.Length;i++)
                {tailJoints[i].localRotation=tailRest[i];tailJoints[i].localPosition=tailPositions[i];}
                tailJoints[0].rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(Mathf.Sin(time*1.8f)*5,Vector3.up)*tailRootRotation;
                var anchor=tailJoints[0].position;
                if(state.Phase!=GamePhase.Victory||preview>=0)anchor.y=home.y+tailHeight;
                else anchor.y=Mathf.Max(home.y+.70f,anchor.y);
                tailJoints[0].position=anchor;
                if(state.Phase==GamePhase.Victory&&preview<0&&tailJoints.Length>1)
                {
                    // Lowering the hips must not drag the distal tail through
                    // the floor. Rotate the chain at its base, preserving all
                    // segment lengths, so its tip settles just above the ash.
                    Vector3 span=tailJoints[tailJoints.Length-1].position-anchor;
                    float length=span.magnitude;
                    float dy=Mathf.Clamp(home.y+.32f-anchor.y,-length*.95f,length*.95f);
                    Vector3 desired=Vector3.ProjectOnPlane(span,Vector3.up).normalized*Mathf.Sqrt(Mathf.Max(0,length*length-dy*dy))+Vector3.up*dy;
                    tailJoints[0].rotation=Quaternion.FromToRotation(span,desired)*tailJoints[0].rotation;
                }
            }
            // Character-local emission carries the same readable signals as the
            // arcade VFX: Golza's eyes wake during warning/attack, while Tiga's
            // timer and crystal intensify during transformation and beam charge.
            float warningGlow=monster&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Windup
                ?.65f+.55f*Mathf.Sin(state.EnemyAge*9):0;
            float attackGlow=monster&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack
                ?.45f+.75f*Mathf.Sin(Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI):0;
            float victoryGlow=monster&&state.Phase==GamePhase.Victory?Mathf.Clamp01(1-phaseAge/2.5f):0;
            float eyeGlow=Mathf.Clamp01(.18f+warningGlow+attackGlow+victoryGlow);
            foreach(var mat in eyeMaterials)
            {
                Color c=monster?new Color(1,.24f,.055f):new Color(.72f,.92f,1);
                mat.SetColor("_EmissionColor",c*(.25f+eyeGlow*1.7f));
            }
            float coreGlow=!monster&&state.Action==HeroAction.Beam
                ?.55f+.95f*Mathf.Sin(Mathf.Clamp01(state.ActionAge/1.9f)*Mathf.PI):
                !monster&&state.Phase==GamePhase.Transforming?.55f+.35f*Mathf.Sin(phaseAge*8):.08f;
            float impactGlow=monster
                ?ContactPulse(hitAge,0,heavyHit?.10f:.055f,heavyHit?.72f:.38f)
                :ContactPulse(guardAge,0,.075f,.42f);
            Color impactColor=monster?(heavyHit?new Color(.055f,.18f,.36f):new Color(1,.16f,.035f)):new Color(.14f,.62f,1);
            foreach(var mat in impactMaterials)
                mat.SetColor("_EmissionColor",impactColor*(impactGlow*(monster?1.15f:.85f)));
            foreach(var mat in coreMaterials)
            {
                Color c=new Color(.10f,.68f,1);
                mat.SetColor("_EmissionColor",c*(.35f+coreGlow*1.6f));
            }
            poseOpacity=opacity;SetPresentationOpacity(1);
        }
        void PoseDefeat(float collapse)
        {
            if(!pelvis)return;
            var side=Vector3.Cross(Vector3.up,forward);var facing=Quaternion.LookRotation(forward);
            // Knees fold beneath the body while the claws drop to either side.
            // A feet-pivot whole-body flip used to bury the belly in the floor.
            Vector3 hips=home-forward*.18f+Vector3.up*.82f;
            Root.position+=Vector3.Lerp(pelvis.position,hips,collapse)-pelvis.position;
            PoseLimb(leftThigh,leftShin,leftFoot,home+facing*leftFootLocal,1,forward-side*.3f,leftFootClearance);
            PoseLimb(rightThigh,rightShin,rightFoot,home+facing*rightFootLocal,1,forward+side*.3f,rightFootClearance);
            leftFoot.rotation=facing*leftFootRest;rightFoot.rotation=facing*rightFootRest;
            float hands=Mathf.SmoothStep(0,1,(collapse-.15f)/.85f);
            Vector3 contact=home+forward*.85f+Vector3.up*.30f;
            PoseLimb(leftUpperArm,leftForearm,leftHand,contact-side*.64f,hands,-side,.30f);
            PoseLimb(upperArm,forearm,hand,contact+side*.64f-forward*.12f,hands,side,.30f);
            AlignClawWrists();
        }
        void PoseVictoryTurn(float progress)
        {
            if(progress<=0||!leftFoot||!rightFoot)return;
            Quaternion start=Quaternion.LookRotation(forward),finish=Quaternion.LookRotation(Vector3.back);
            Quaternion half=Quaternion.Slerp(start,finish,.5f);
            Vector3 plantedLeft=home+start*leftFootLocal;
            Vector3 halfRoot=plantedLeft-half*leftFootLocal,plantedRight=halfRoot+half*rightFootLocal;
            bool first=progress<.5f;float step=first?progress*2:(progress-.5f)*2;
            Root.rotation=Quaternion.Slerp(first?start:half,first?half:finish,Mathf.SmoothStep(0,1,step));
            Root.position=first?plantedLeft-Root.rotation*leftFootLocal:plantedRight-Root.rotation*rightFootLocal;
            float lift=.16f*Mathf.Sin(step*Mathf.PI);
            var swinging=first?rightFoot:leftFoot;
            Vector3 target=swinging.position+Vector3.up*lift;
            PoseLimb(first?rightThigh:leftThigh,first?rightShin:leftShin,swinging,target,1,Root.forward,
                home.y+(first?rightFootLocal.y:leftFootLocal.y));
            leftFoot.rotation=Root.rotation*leftFootRest;rightFoot.rotation=Root.rotation*rightFootRest;
        }
        void PoseKnockdown(float weight)
        {
            if(!pelvis||weight<=0)return;
            var side=Vector3.Cross(Vector3.up,forward);
            Vector3 seated=home-forward*.35f+Vector3.up*.43f;
            Root.position+=Vector3.Lerp(pelvis.position,seated,weight)-pelvis.position;
            Vector3 feet=seated+forward*1.12f;feet.y=home.y+.16f;
            PoseLimb(leftThigh,leftShin,leftFoot,feet-side*.32f,weight,Vector3.up,leftFootClearance);
            PoseLimb(rightThigh,rightShin,rightFoot,feet+side*.30f-forward*.20f,weight,Vector3.up,rightFootClearance);
            if(leftFoot)leftFoot.rotation=Quaternion.Slerp(leftFoot.rotation,Quaternion.LookRotation(forward)*leftFootRest,weight);
            if(rightFoot)rightFoot.rotation=Quaternion.Slerp(rightFoot.rotation,Quaternion.LookRotation(forward)*rightFootRest,weight);
            // One hand braces beside the hip; the other retains its authored
            // recoil. Unequal limbs read as a fall instead of a rotated statue.
            Vector3 support=seated-side*.68f-forward*.32f;support.y=home.y+.20f;
            Quaternion palm=leftHand?leftHand.rotation:Quaternion.identity;
            PoseLimb(leftUpperArm,leftForearm,leftHand,support,weight,-side,.20f);
            if(leftHand)leftHand.rotation=palm;
        }
        void PoseLimb(Transform thigh,Transform shin,Transform foot,Vector3 target,float weight,Vector3 pole,float clearance)
        {
            if(!thigh||!shin||!foot)return;
            // Interpolate the endpoint, then solve the full chain. Blending
            // rotations independently sweeps a foot/hand through the ground.
            target=Vector3.Lerp(foot.position,target,weight);target.y=Mathf.Max(home.y+clearance,target.y);
            Vector3 start=thigh.position,to=target-start;
            float a=Vector3.Distance(start,shin.position),b=Vector3.Distance(shin.position,foot.position);
            float d=Mathf.Clamp(to.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 axis=to.normalized,bend=Vector3.ProjectOnPlane(pole,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d);
            Vector3 knee=start+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            thigh.rotation=Quaternion.FromToRotation(shin.position-start,knee-start)*thigh.rotation;
            shin.rotation=Quaternion.FromToRotation(foot.position-shin.position,start+axis*d-shin.position)*shin.rotation;
        }
        void CorrectRestingArms(Battle state)
        {
            // The imported Golza idle curve leaves both elbows on the same
            // plane, which makes the hands read as a flat, mirrored prop on a
            // television. Gently solve only the resting/wind-up/recovery
            // poses toward a chest-level target; attack and hurt clips retain
            // their authored reach and contact timing.
            float blend=state.Enemy==EnemyPhase.Rest?.34f:state.Enemy==EnemyPhase.Windup?.20f:state.Enemy==EnemyPhase.Recover?.24f:0;
            if(state.Phase!=GamePhase.Battle||blend<=0||!upperArm||!leftUpperArm||!forearm||!leftForearm)return;
            var right=Vector3.Cross(Vector3.up,forward).normalized;
            Vector3 center=Root.position+forward*.48f+Vector3.up*2.38f;
            // A kaiju guard is asymmetrical: one claw owns the foreground while
            // the other stays closer to the ribs. Equal forward targets made both
            // hands flatten into one prop on a three-quarter TV shot.
            SolveArm(leftUpperArm,leftForearm,leftHand,
                center-right*.43f+forward*.00f+Vector3.up*.06f,
                center-right*.47f+forward*.22f+Vector3.up*.00f,blend);
            SolveArm(upperArm,forearm,hand,
                center+right*.43f+forward*.04f+Vector3.up*.12f,
                center+right*.50f+forward*.44f+Vector3.up*.08f,blend);
        }
        void CorrectAttackArms(Battle state)
        {
            if(!upperArm||!leftUpperArm||!forearm||!leftForearm||!hand||!leftHand)return;
            // Keep the imported attack clip's timing, but guide the active claw
            // toward the contact lane. This prevents the low-resolution source
            // animation from reading as two disconnected arms on a TV.
            float phase=Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds);
            float reach=Mathf.Sin(phase*Mathf.PI);
            bool leadLeft=state.EnemyAttackCount%2==0;
            float leadSide=leadLeft?-1:1;
            var right=Vector3.Cross(Vector3.up,forward).normalized;
            Vector3 center=Root.position+forward*.48f+Vector3.up*2.38f;
            Vector3 leadElbow=center+right*leadSide*.52f+forward*.22f+Vector3.up*(.24f+.08f*reach);
            Vector3 leadWrist=center+right*leadSide*.50f+forward*(.42f+.44f*reach)+Vector3.up*(.04f+.13f*reach);
            // Pull the non-leading claw back toward the chest. It still moves
            // with the attack, but never competes with the contact hand.
            Vector3 supportElbow=center-right*leadSide*.43f+forward*.01f+Vector3.up*.18f;
            Vector3 supportWrist=center-right*leadSide*.42f+forward*(.12f+.08f*reach)+Vector3.up*(.08f+.02f*reach);
            float leadBlend=.08f+.24f*reach,supportBlend=.10f+.10f*reach;
            SolveArm(leadLeft?leftUpperArm:upperArm,leadLeft?leftForearm:forearm,leadLeft?leftHand:hand,leadElbow,leadWrist,leadBlend);
            SolveArm(leadLeft?upperArm:leftUpperArm,leadLeft?forearm:leftForearm,leadLeft?hand:leftHand,supportElbow,supportWrist,supportBlend);
        }
        static void SolveArm(Transform upper,Transform lower,Transform wrist,Vector3 elbowTarget,Vector3 wristTarget,float blend)
        {
            if(!upper||!lower||!wrist)return;
            Vector3 upperVector=lower.position-upper.position,targetVector=elbowTarget-upper.position;
            if(upperVector.sqrMagnitude<.0001f||targetVector.sqrMagnitude<.0001f)return;
            // The clip already authors a forward-facing claw. Re-aiming upper
            // and lower arms must not roll that palm with the inherited elbow
            // rotation, which previously left the claws hanging or folded in.
            Quaternion palm=wrist.rotation;
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(upperVector,targetVector)*upper.rotation,blend);
            Vector3 forearmVector=wrist.position-lower.position;targetVector=wristTarget-lower.position;
            if(forearmVector.sqrMagnitude>=.0001f&&targetVector.sqrMagnitude>=.0001f)
            {
                Quaternion forearmAim=Quaternion.FromToRotation(forearmVector,targetVector);
                lower.rotation=Quaternion.Slerp(lower.rotation,forearmAim*lower.rotation,blend);
                // The old correction restored the wrist world rotation after
                // re-aiming the arm. On a close 45-degree shot that made the
                // palm hang sideways from a correctly placed forearm. Follow
                // the forearm only part way, preserving the authored claw roll
                // while keeping the fingers attached to the strike lane.
                wrist.rotation=Quaternion.Slerp(palm,forearmAim*palm,Mathf.Clamp01(blend*.72f));
            }
            else wrist.rotation=palm;
        }
        void AlignClawWrists()
        {
            // A world-facing palm can fold back over a raised forearm during
            // anticipation. Constrain the actual metacarpal axis, not the
            // mirrored bone's arbitrary local Euler angles. Only the wrist
            // rotates: elbow, contact point and attack timing stay authored.
            for(int side=0;side<2;side++)
            {
                Transform wrist=side==0?leftHand:hand,lower=side==0?leftForearm:forearm;
                if(!wrist||!lower||palmForwardLocal[side].sqrMagnitude<.5f)continue;
                Vector3 palm=wrist.TransformDirection(palmForwardLocal[side]);
                Vector3 arm=(wrist.position-lower.position).normalized;
                Vector3 limited=Vector3.RotateTowards(arm,palm,35*Mathf.Deg2Rad,0);
                wrist.rotation=Quaternion.FromToRotation(palm,limited)*wrist.rotation;
            }
        }
        void ApplyClawPose(Battle state,int preview,float time)
        {
            if(clawBones.Count==0)return;
            float curl=.14f,spread=.8f;
            bool attack=false;
            int attackSide=1;
            if(preview>=0)
            {
                curl=preview==1||preview==3?.06f:preview==2?.30f:preview==5?.22f:preview==7?.18f:.14f;
                attack=preview==2;attackSide=1;
            }
            else if(state.Phase==GamePhase.Battle)
            {
                attack=state.Enemy==EnemyPhase.Attack;attackSide=state.EnemyAttackCount%2==0?-1:1;
                if(state.Enemy==EnemyPhase.Attack)
                {
                    float reach=Mathf.Sin(Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI);
                    curl=.19f+.16f*reach;
                }
                else if(state.Enemy==EnemyPhase.Windup)
                {
                    float wind=Mathf.Clamp01(state.EnemyAge/state.WarningDuration);
                    curl=.04f+.13f*wind;
                }
                else if(state.Enemy==EnemyPhase.Rest)curl=.14f+Mathf.Sin(time*2.05f+.8f)*.025f;
                else if(state.Enemy==EnemyPhase.Recover)curl=.17f;
                else if(state.Action==HeroAction.Hurt)curl=.22f;
            }
            // The imported mesh has two phalanges per claw finger. A small,
            // camera-readable curl makes the hands read as claws instead of
            // five flat rods; the lead hand opens a little before curling at
            // contact while the support hand stays closer to the chest.
            for(int side=0;side<2;side++)
            {
                var wrist=side==0?leftHand:hand;
                Vector3 palmForward=wrist.TransformDirection(palmForwardLocal[side]);
                Vector3 palmUp=wrist.TransformDirection(palmUpLocal[side]);
                Vector3 curlAxis=Vector3.Cross(palmForward,-palmUp).normalized;
                bool lead=attack&&((side==0&&attackSide<0)||(side==1&&attackSide>0));
                float amount=curl*(lead?1.08f:.86f);
                float sideSpread=(side==0?-1:1)*spread;
                for(int i=0;i<ClawFingerNames.Length;i++)
                {
                    var first=clawFingers[i,side,0];
                    if(first)
                    {
                        float fan=sideSpread*(i-1.5f)*.7f;
                        first.rotation=Quaternion.AngleAxis(fan,palmUp)*Quaternion.AngleAxis(amount*58,curlAxis)*first.rotation;
                    }
                    var tip=clawFingers[i,side,1];
                    if(tip)
                        tip.rotation=Quaternion.AngleAxis(amount*78,curlAxis)*tip.rotation;
                }
                var thumb=clawThumbs[side,0];
                if(thumb)
                    thumb.rotation=Quaternion.AngleAxis(sideSpread*1.8f,palmForward)*Quaternion.AngleAxis(amount*42,curlAxis)*thumb.rotation;
                var thumbTip=clawThumbs[side,1];
                if(thumbTip)
                    thumbTip.rotation=Quaternion.AngleAxis(amount*58,curlAxis)*thumbTip.rotation;
            }
        }
        static float ContactPulse(float age,float start,float peak,float end)
        {return age<=start||age>=end?0:age<peak?Mathf.SmoothStep(0,1,(age-start)/(peak-start)):1-Mathf.SmoothStep(0,1,(age-peak)/(end-peak));}
    }
}
