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
        readonly bool retargetedPunch;
        readonly Vector3 home, forward,opponentHome;
        AnimatedActor opponent;
        readonly Dictionary<string, AnimationClip> clips=new Dictionary<string, AnimationClip>();
        readonly Transform[] joints;
        readonly Renderer[] surfaces;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;
        readonly Quaternion[] comboExitStart,comboExitBase;
        readonly bool[] comboExitMask;
        bool comboWasActive,comboExitApplied;
        float comboExitAge=1;
        readonly List<Material> materials=new List<Material>();
        readonly List<Material> eyeMaterials=new List<Material>();
        readonly List<Material> coreMaterials=new List<Material>();
        readonly List<Material> atlasLightMaterials=new List<Material>();
        readonly List<Material> impactMaterials=new List<Material>();
        string playing;
        float clipAge, phaseAge, blendLeft, hitAge=10, lastHealth, poseOpacity=1;
        float heroRecoveryAge=10;
        float chaseAdvance,observedPunchAge;
        float windupSample=float.NaN;
        readonly MonsterStaggerMotion stagger=new MonsterStaggerMotion();
        readonly MonsterLaunchMotion launch=new MonsterLaunchMotion();
        readonly MonsterBeamMotion beamRecoil=new MonsterBeamMotion();
        readonly HeroPunchLink punchLink=new HeroPunchLink();
        public float LinkedPunchWeight=>punchLink.Weight;
        Vector3 beamTravel;
        public float BeamRecoilAge=>beamRecoil.Active?beamRecoil.Age:10;
        public bool BeamRecoilLeft=>beamRecoil.Left;
        public int BeamLandings=>beamRecoil.Landings;
        public float BeamChaseAdvance=>beamRecoil.Travel+.24f*beamRecoil.Weight;
        bool launchPoseApplied;
        Vector3 launchRootBase;
        Quaternion launchRotationBase;
        public float LaunchAge=>launch.Active?launch.Age:10;
        public int LaunchLandings=>launch.Landings;
        public float LaunchCamera=>launch.Camera;
        Vector3 staggerTravel;
        public float StaggerAge=>stagger.Active?stagger.Age:10;
        public bool StaggerLeft=>stagger.Left;
        public int StaggerLandings=>stagger.Landings;
        HeroAction observedAction=HeroAction.None;
        int lastPunchSide;
        GamePhase previous;
        bool heavyHit,accentHit;
        Transform hand,leftHand,forearm,leftForearm,leftUpperArm,upperArm,rightFoot,leftFoot;
        Transform head,upperSpine,pelvis,jaw;
        Quaternion jawBase;
        bool jawLayerApplied;
        float jawCorrection,recoveryWeight;
        public float RecoveryWeight=>recoveryWeight;
        Vector3 beamContactLocal;
        readonly SkinnedSurfaceAnchor beamSurface,foreheadSurface;
        Vector3 surfaceContactLocal;
        Transform leftThigh,rightThigh,leftShin,rightShin;
        Quaternion leftFootRest,rightFootRest;
        Vector3 leftFootLocal,rightFootLocal;
        Vector3 pelvisLocal;
        bool knockdownApplied;
        Vector3 knockdownRoot;
        Quaternion knockdownFacing;
        readonly Quaternion[] knockdownRotations=new Quaternion[9];
        float leftFootClearance,rightFootClearance;
        Quaternion headBase,spineBase;
        bool contactLayerApplied;
        bool breathArmsApplied;
        Quaternion breathLeftArm,breathRightArm;
        float contactSide;
        Vector3 recoilStart;
        float recoilStartYaw;
        Battle observedBattle;
        int observedBlocks;
        float guardAge=10;
        Vector3 guardContact;
        Vector3 guardTravel;
        bool guardContactPending;
        float stepDrop;
        bool stepArmsApplied;
        readonly Quaternion[] stepArmRotations=new Quaternion[6];
        bool stepLegsApplied;
        readonly Quaternion[] stepLegRotations=new Quaternion[6];
        readonly Dictionary<string,Transform> clawBones=new Dictionary<string,Transform>();
        readonly Transform[,,] clawFingers=new Transform[4,2,2];
        readonly Transform[,] clawThumbs=new Transform[2,2];
        readonly Quaternion[] clawRollBase=new Quaternion[4];
        bool clawRollApplied;
        bool clawReactionApplied;
        readonly Quaternion[] clawReactionBase=new Quaternion[6];
        Vector3 clawLeft,clawRight,clawCarryLeft,clawCarryRight;
        public float ClawReactionAmount=>Mathf.Max(clawLeft.magnitude,clawRight.magnitude);
        bool attackPoseApplied;
        float slamPrepare,rayPrepare;
        Vector3 foreheadLocal;
        public Vector3 RayOrigin=>foreheadSurface!=null?foreheadSurface.Position+forward*.035f:head?head.TransformPoint(foreheadLocal):Root.position+Vector3.up*3.4f;
        Vector3 attackRootBefore;
        Quaternion attackRotationBefore;
        readonly Quaternion[] attackRotations;
        readonly Vector3[] attackPositions;
        readonly Quaternion[] attackExitRotations;
        readonly Vector3[] attackExitPositions;
        readonly MonsterDissolve dissolve;
        public int DissolveStarts=>dissolve?.Starts??0;
        public int DissolveMotes=>dissolve?.ActiveMotes??0;
        readonly Vector3[] palmForwardLocal=new Vector3[2],palmUpLocal=new Vector3[2];
        static readonly string[] ClawFingerNames={"index","middle","ring","pinky"};
        Transform[] tailJoints;Quaternion[] tailRest;Vector3[] tailPositions;Quaternion tailRootRotation;float tailHeight;
        public Vector3 StrikeOrigin(HeroAction action) => action==HeroAction.LeftPunch&&leftHand?leftHand.position:HandPosition;
        public Vector3 HandPosition => hand?hand.position:Root.position+Vector3.up*2.2f;
        public Vector3 EnemyStrikeOrigin(int attackCount) => attackCount%2==0&&leftHand?leftHand.position:HandPosition;
        public Vector3 BeamOrigin => hand&&forearm?Vector3.Lerp(forearm.position,hand.position,.6f):HandPosition;
        public Vector3 BeamContact => upperSpine?upperSpine.TransformPoint(beamContactLocal):Root.position+Vector3.up*2.48f;
        public Vector3 BeamSurfaceContact => beamSurface!=null?beamSurface.Position:BeamContact;
        public Vector3 FootPosition(bool left) => (left?leftFoot:rightFoot)?(left?leftFoot:rightFoot).position:Root.position;
        public Vector3 GroundContactPosition => pelvis?pelvis.position:Root.position;
        public void SetOpponent(AnimatedActor actor){opponent=actor;}
        public void BindGuardImpact(Vector3 worldPosition)
        {if(!monster){guardContact=worldPosition;guardContactPending=true;}}
        public void BindSurfaceImpact(Vector3 worldPosition)
        {
            if(!monster||!upperSpine)return;
            // Called after the contact pose is sampled. Store the actual fist
            // or beam contact in chest space so recoil carries the light.
            surfaceContactLocal=upperSpine.InverseTransformPoint(worldPosition);
            UpdateSurfaceImpact();
        }

        public static RiggedActor CreateIfAvailable(string name,Vector3 position,Vector3 opponent,bool monster)
        {
            string path="Characters/"+name+"/"+name;
            var prefab=Resources.Load<GameObject>(path);
            return prefab?new RiggedActor(name,path,prefab,position,opponent,monster):null;
        }
        RiggedActor(string name,string path,GameObject prefab,Vector3 position,Vector3 opponent,bool isMonster)
        {
            monster=isMonster;home=position;opponentHome=opponent;retargetedPunch=!monster&&name!="Tiga";forward=Vector3.ProjectOnPlane(opponent-position,Vector3.up).normalized;
            if(retargetedPunch)StrikeAdvance=1.10f;
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
            int modelVertices=0;
            foreach(var renderer in renderers)
            {
                if(renderer is SkinnedMeshRenderer skin) {skin.updateWhenOffscreen=true;BoneCount=Mathf.Max(BoneCount,skin.bones.Length);modelVertices+=skin.sharedMesh.vertexCount;}
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                var mapped=renderer.sharedMaterials;
                for(int i=0;i<mapped.Length;i++)
                {
                    string key=mapped[i]?mapped[i].name:"Surface";
                    if(!materialCache.TryGetValue(key,out var mat))
                    {
                        mat=RuntimeResources.Own(Root,Surface(key,texture,eyes,name));materialCache[key]=mat;materials.Add(mat);
                        if(HeroAtlasLights.Configure(mat,name))atlasLightMaterials.Add(mat);
                    }
                    if(key.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0&&key.IndexOf("EyeRim",StringComparison.OrdinalIgnoreCase)<0)
                        if(!eyeMaterials.Contains(mat))eyeMaterials.Add(mat);
                    if(key.IndexOf("Crystal",StringComparison.OrdinalIgnoreCase)>=0||key.IndexOf("Timer",StringComparison.OrdinalIgnoreCase)>=0)
                        if(!coreMaterials.Contains(mat))coreMaterials.Add(mat);
                    if((monster&&key.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)<0&&key.IndexOf("EyesGlow",StringComparison.OrdinalIgnoreCase)<0)||
                       (!monster&&mat.HasProperty("_GuardPoint")))
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
                if(joint.name=="jaw")jaw=joint;
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
            attackRotations=new Quaternion[joints.Length];attackPositions=new Vector3[joints.Length];
            attackExitRotations=new Quaternion[joints.Length];attackExitPositions=new Vector3[joints.Length];
            comboExitStart=new Quaternion[joints.Length];comboExitBase=new Quaternion[joints.Length];comboExitMask=new bool[joints.Length];
            if(!monster&&upperSpine)for(int i=0;i<joints.Length;i++)comboExitMask[i]=joints[i]==upperSpine||joints[i].IsChildOf(upperSpine);
            Root.position=home;Root.rotation=Quaternion.LookRotation(forward,Vector3.up);
            // A point on the front of the resting chest, carried by its sampled
            // bone through recoil. Root/home coordinates drift off the skin.
            if(upperSpine)beamContactLocal=upperSpine.InverseTransformPoint(home+Vector3.up*2.48f+forward*.33f);
            if(monster)beamSurface=SkinnedSurfaceAnchor.Torso(surfaces,new Ray(home+Vector3.up*2.48f+forward*3,-forward));
            if(leftFoot)leftFootRest=Quaternion.Inverse(Root.rotation)*leftFoot.rotation;
            if(rightFoot)rightFootRest=Quaternion.Inverse(Root.rotation)*rightFoot.rotation;
            if(leftFoot)leftFootLocal=Root.InverseTransformPoint(leftFoot.position);
            if(rightFoot)rightFootLocal=Root.InverseTransformPoint(rightFoot.position);
            if(pelvis)pelvisLocal=Root.InverseTransformPoint(pelvis.position);
            leftFootClearance=leftFoot?Mathf.Max(.16f,leftFoot.position.y-home.y+.025f):.16f;
            rightFootClearance=rightFoot?Mathf.Max(.16f,rightFoot.position.y-home.y+.025f):.16f;
            if(monster)
            {
                dissolve=new MonsterDissolve(Root,surfaces);
                foreheadLocal=head.InverseTransformPoint(home+Vector3.up*3.48f+forward*.60f);
                foreheadSurface=SkinnedSurfaceAnchor.Head(surfaces,new Ray(home+Vector3.up*3.48f+forward*3,-forward));
            }
            Debug.Log($"[RiggedActor] name={name} clips={clips.Count} bones={BoneCount} renderers={renderers.Length} height={bounds.size.y*size:F2} vertices={modelVertices}");
        }
        static Material Surface(string name,Texture2D texture,Texture2D eyes,string character)
        {
            bool kaiju=name.StartsWith("Golza",StringComparison.Ordinal);
            bool emitter=(name.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0&&name.IndexOf("EyeRim",StringComparison.OrdinalIgnoreCase)<0)
                ||name.IndexOf("Timer",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Crystal",StringComparison.OrdinalIgnoreCase)>=0;
            bool heroSurface=character!="Golza"&&!emitter;
            var mat=kaiju?new Material(Resources.Load<Shader>("KaijuSurface")):
                heroSurface?new Material(Resources.Load<Shader>("HeroSurface")):new Material(Resources.Load<Material>("PrototypeSurface"));mat.name=name;
            mat.color=new Color(.72f,.77f,.85f);mat.SetFloat("_Metallic",heroSurface?.48f:.65f);mat.SetFloat("_Glossiness",heroSurface?.46f:.55f);
            if(name.StartsWith("Golza",StringComparison.Ordinal))
            {
                bool eye=name.Contains("Eyes");mat.mainTexture=eye?eyes:texture;mat.color=Color.white;
                mat.SetFloat("_SkinDetail",eye?0:1);
                mat.SetFloat("_Metallic",.03f);mat.SetFloat("_Glossiness",.22f);
                if(!eye)
                {
                    var enhanced=Resources.Load<Texture2D>("Characters/Golza/GolzaBodyHD");
                    if(enhanced)
                    {
                        // Generated restoration retains the source atlas inside
                        // horizontal padding. Map UVs into that region; preserve
                        // the original file and use it when the HD asset is absent.
                        mat.mainTexture=enhanced;
                        mat.mainTextureScale=new Vector2(.75f,1);
                        mat.mainTextureOffset=new Vector2(.125f,0);
                        mat.SetFloat("_Metallic",0);mat.SetFloat("_Glossiness",.16f);
                    }
                }
                if(eye){mat.EnableKeyword("_EMISSION");mat.SetTexture("_EmissionMap",eyes);mat.SetColor("_EmissionColor",new Color(.55f,.35f,.15f));}
            }
            else if(name.Contains("Suit")) {mat.mainTexture=texture;mat.color=Color.white;mat.SetFloat("_Metallic",.03f);mat.SetFloat("_Glossiness",.28f);}
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
                mat.mainTexture=rosterTexture;mat.color=Color.white;mat.SetFloat("_Metallic",heroSurface?.06f:.2f);mat.SetFloat("_Glossiness",heroSurface?.30f:.42f);
                if(name.ToLowerInvariant().Contains("eye")||name.ToLowerInvariant().Contains("timer"))
                {mat.EnableKeyword("_EMISSION");mat.SetTexture("_EmissionMap",rosterTexture);mat.SetColor("_EmissionColor",Color.white*.5f);}
            }
            if(heroSurface&&mat.mainTexture)mat.SetFloat("_TextureArmor",1);
            if(character=="Tiga"&&name=="TigaSuit")
            {
                mat.SetFloat("_CostumeFinish",1);
                var cavity=Resources.Load<Texture2D>("Characters/Tiga/TigaBodyOcclusion");
                if(cavity)mat.SetTexture("_CostumeOcclusion",cavity);
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
            if(knockdownApplied)
            {
                Root.SetPositionAndRotation(knockdownRoot,knockdownFacing);
                leftThigh.localRotation=knockdownRotations[0];leftShin.localRotation=knockdownRotations[1];leftFoot.localRotation=knockdownRotations[2];
                rightThigh.localRotation=knockdownRotations[3];rightShin.localRotation=knockdownRotations[4];rightFoot.localRotation=knockdownRotations[5];
                leftUpperArm.localRotation=knockdownRotations[6];leftForearm.localRotation=knockdownRotations[7];leftHand.localRotation=knockdownRotations[8];
                knockdownApplied=false;
            }
            if(jawLayerApplied){jaw.localRotation=jawBase;jawLayerApplied=false;}
            if(clawReactionApplied)
            {
                leftUpperArm.localRotation=clawReactionBase[0];leftForearm.localRotation=clawReactionBase[1];leftHand.localRotation=clawReactionBase[2];
                upperArm.localRotation=clawReactionBase[3];forearm.localRotation=clawReactionBase[4];hand.localRotation=clawReactionBase[5];
                clawReactionApplied=false;
            }
            bool attackInterrupted=attackPoseApplied&&state.Phase==GamePhase.Battle&&state.Enemy!=EnemyPhase.Attack&&state.EnemyHealth<lastHealth;
            if(attackInterrupted)for(int i=0;i<joints.Length;i++)
            {attackExitRotations[i]=joints[i].localRotation;attackExitPositions[i]=joints[i].localPosition;}
            if(attackPoseApplied)
            {
                Root.SetPositionAndRotation(attackRootBefore,attackRotationBefore);
                for(int i=0;i<joints.Length;i++){joints[i].localRotation=attackRotations[i];joints[i].localPosition=attackPositions[i];}
                attackPoseApplied=false;
            }
            if(clawRollApplied)
            {
                leftForearm.localRotation=clawRollBase[0];leftHand.localRotation=clawRollBase[1];
                forearm.localRotation=clawRollBase[2];hand.localRotation=clawRollBase[3];
                clawRollApplied=false;
            }
            bool comboStrike=!monster&&preview<0&&ComboStrikeMotion.Active(state);
            if(comboWasActive&&!comboStrike&&ReferenceEquals(observedBattle,state)&&state.Phase==GamePhase.Battle&&state.Shield)
            {
                // Shield rules take over immediately; fold the visible upper
                // body out of the hook, not from the hidden straight-punch clip.
                comboExitAge=0;for(int i=0;i<joints.Length;i++)if(comboExitMask[i])comboExitStart[i]=joints[i].localRotation;
            }
            else if(comboStrike||preview>=0||!ReferenceEquals(observedBattle,state)||state.Phase!=GamePhase.Battle)comboExitAge=1;
            if(comboExitApplied)for(int i=0;i<joints.Length;i++)if(comboExitMask[i])joints[i].localRotation=comboExitBase[i];
            comboExitApplied=false;comboWasActive=comboStrike;
            bool retargetArms=!monster&&preview<0&&state.Phase==GamePhase.Battle&&
                (state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch||
                 (state.Action==HeroAction.None&&!state.Shield));
            // The leg reach correction is a presentation layer; remove it
            // before arm targeting and clip blending on the following frame.
            if(launchPoseApplied){Root.position=launchRootBase;Root.rotation=launchRotationBase;launchPoseApplied=false;}
            Root.position+=Vector3.up*stepDrop;stepDrop=0;
            Root.position-=guardTravel;guardTravel=Vector3.zero;
            if(stepLegsApplied)
            {
                leftThigh.localRotation=stepLegRotations[0];leftShin.localRotation=stepLegRotations[1];leftFoot.localRotation=stepLegRotations[2];
                rightThigh.localRotation=stepLegRotations[3];rightShin.localRotation=stepLegRotations[4];rightFoot.localRotation=stepLegRotations[5];stepLegsApplied=false;
            }
            if(stepArmsApplied)
            {
                // When leaving a retargeted pose, let the destination clip
                // blend from the hands the player actually saw. Restoring the
                // source clip first would pop the guard outward for one frame.
                if(monster||retargetArms)
                {
                    leftUpperArm.localRotation=stepArmRotations[0];leftForearm.localRotation=stepArmRotations[1];leftHand.localRotation=stepArmRotations[2];
                    upperArm.localRotation=stepArmRotations[3];forearm.localRotation=stepArmRotations[4];hand.localRotation=stepArmRotations[5];
                }
                stepArmsApplied=false;
            }
            // Arm IK caches the breathed pose. Remove the shoulder offset
            // after that restore so a paused/repeated idle sample cannot add it
            // again. A new non-retargeted clip still blends from visible hands.
            if(breathArmsApplied)
            {
                if(retargetArms){leftUpperArm.localRotation=breathLeftArm;upperArm.localRotation=breathRightArm;}
                breathArmsApplied=false;
            }
            // Blends start from the sampled clip, never from last frame's
            // additive impact. Otherwise the same impulse feeds back into itself.
            if(contactLayerApplied)
            {
                if(upperSpine)upperSpine.localRotation=spineBase;
                if(head)head.localRotation=headBase;
                contactLayerApplied=false;
            }
            if(previous!=state.Phase) {previous=state.Phase;phaseAge=0;}
            punchLink.Tick(state,dt,!monster&&preview<0);
            if(!monster)
            {
                if(state.Phase!=GamePhase.Battle)
                {heroRecoveryAge=10;observedAction=state.Action;chaseAdvance=observedPunchAge=0;}
                else
                {
                    bool wasPunch=observedAction==HeroAction.LeftPunch||observedAction==HeroAction.RightPunch;
                    if(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)
                    {
                        if(observedAction!=state.Action||state.ActionAge<observedPunchAge)
                            chaseAdvance=opponent==null?0:opponent.LaunchAge<MonsterLaunchMotion.Landing?.72f:opponent.BeamChaseAdvance;
                        lastPunchSide=state.Action==HeroAction.LeftPunch?-1:1;
                        observedPunchAge=state.ActionAge;
                    }
                    if(state.Action==HeroAction.None&&wasPunch)heroRecoveryAge=0;
                    else heroRecoveryAge+=dt;
                    observedAction=state.Action;
                }
            }
            // A block is a contact event, not the held guard input. Observe it
            // once per round so a held shield, pause or photo restart cannot
            // replay the recoil. Chest yields first; planted knees take the
            // weight a little later and settle through the next input.
            if(!ReferenceEquals(observedBattle,state))
            {observedBattle=state;observedBlocks=state.Blocks;guardAge=hitAge=10;guardContactPending=false;recoilStart=Vector3.zero;recoilStartYaw=0;accentHit=false;windupSample=float.NaN;slamPrepare=rayPrepare=jawCorrection=0;stagger.Clear();launch.Clear();beamRecoil.Clear();beamTravel=staggerTravel=Vector3.zero;}
            if(monster)
            {
                if(preview>=0||state.Phase!=GamePhase.Battle||state.Enemy==EnemyPhase.Attack)beamRecoil.Clear();
                else beamRecoil.Tick(dt);
                if(preview>=0||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam||state.Enemy==EnemyPhase.Attack)launch.Clear();
                else launch.Tick(dt);
                if(preview>=0||state.Phase==GamePhase.Waiting||state.Phase==GamePhase.Transforming||state.Phase==GamePhase.Paused)stagger.Clear();
                else stagger.Tick(dt,state.Phase==GamePhase.Battle&&state.Enemy!=EnemyPhase.Attack&&state.Action!=HeroAction.Beam);
            }
            if(state.Phase!=GamePhase.Battle){guardAge=10;guardContactPending=false;}
            else if(state.Blocks>observedBlocks)
            {
                guardAge=0;
                // Actual play supplies the shield contact; older pose previews
                // can still use the midpoint of the braced hands.
                if(!guardContactPending)guardContact=(HandPosition+StrikeOrigin(HeroAction.LeftPunch))*.5f;
                guardContactPending=false;
            }
            else guardAge+=dt;
            observedBlocks=state.Blocks;
            float guardRecoil=ContactPulse(guardAge,0,.09f,.54f);
            phaseAge+=dt;hitAge+=dt;
            if(state.Phase!=GamePhase.Battle){hitAge=10;recoilStart=Vector3.zero;recoilStartYaw=0;}
            else if(state.EnemyHealth<lastHealth)
            {
                // Preserve only a previous ordinary impulse. A sustained beam
                // or airborne reaction already owns its continuing arm motion.
                clawCarryLeft=beamRecoil.Active||launch.Active?Vector3.zero:clawLeft;
                clawCarryRight=beamRecoil.Active||launch.Active?Vector3.zero:clawRight;
                // Continue from a still-settling hit when punches arrive quickly.
                // Do not carry a lunge displacement into a different hit pose.
                bool carryPose=playing=="Hurt"||playing=="Idle";
                recoilStart=carryPose?Vector3.ProjectOnPlane(Root.position-home-staggerTravel-beamTravel,Vector3.up):Vector3.zero;
                recoilStartYaw=carryPose?Vector3.SignedAngle(forward,Root.forward,Vector3.up):0;
                hitAge=0;heavyHit=lastHealth-state.EnemyHealth>1;contactSide=state.Action==HeroAction.LeftPunch?-1:state.Action==HeroAction.RightPunch?1:0;
                accentHit=!heavyHit&&ComboStrikeMotion.Active(state);
                if(monster&&preview<0&&state.Action==HeroAction.Beam)
                {stagger.Clear();launch.Clear();beamRecoil.Begin((state.Punches/Battle.MaxEnergy)%2==1);}
                // Leave the final warning second and the attacking claw alone.
                // The step never delays an enemy hit or the child's next input.
                if(monster&&accentHit&&!beamRecoil.Active&&state.Enemy!=EnemyPhase.Attack&&
                    (state.Enemy!=EnemyPhase.Windup||state.WarningDuration-state.EnemyAge>1.1f))
                {
                    if(MonsterLaunchMotion.Uppercut(state)&&
                        (state.Enemy!=EnemyPhase.Windup||state.WarningDuration-state.EnemyAge>1.6f))
                    {stagger.Clear();launch.Begin(contactSide<0);}
                    else if(!launch.Active)stagger.Begin(contactSide<0);
                }
                surfaceContactLocal=beamContactLocal;
            }
            lastHealth=state.EnemyHealth;
            recoveryWeight=monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Recover&&!beamRecoil.Active&&!launch.Active
                ?MonsterRecoveryMotion.Weight(state.EnemyAge)*Mathf.SmoothStep(0,1,(hitAge-MonsterRecoilMotion.Duration)/.18f):0;
            string next="Idle";float sample=time%clips["Idle"].length,travel=0,opacity=1,fallTilt=0,fallSide=0,fallDrop=0;
            Frame=0;
            if(monster)
            {
                if(state.Phase==GamePhase.Transforming&&clips.ContainsKey("Walk"))
                {next="Walk";sample=state.TransformationAge%clips["Walk"].length;travel=-.45f*(1-Mathf.SmoothStep(0,1,state.TransformationAge/Battle.TransformationSeconds));}
                else if(state.Phase==GamePhase.Victory)
                {
                    next="Defeat";sample=Mathf.Min(1.5f,phaseAge);Frame=7;
                    float collapse=VictoryMotion.Collapse(phaseAge);
                    float stagger=Mathf.Sin(Mathf.Clamp01(phaseAge/.5f)*Mathf.PI);
                    fallTilt=-12f*stagger+20f*collapse;fallSide=-6f*collapse;
                    opacity=VictoryMotion.Opacity(phaseAge);
                }
                else if(state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack) {next=state.EnemyAttackCount%2==0?"AttackAlt":"Attack";sample=state.EnemyAge;Frame=2;travel=AnimatedActor.MonsterAdvance(state);}
                else if(state.Phase==GamePhase.Battle&&(beamRecoil.Active||hitAge<(heavyHit?.9f:MonsterRecoilMotion.Duration)))
                {
                    next="Hurt";sample=beamRecoil.Active?beamRecoil.Clip:heavyHit?(hitAge>.14f?Mathf.Lerp(.14f,.4f,(hitAge-.14f)/.76f):hitAge):hitAge*.4f/MonsterRecoilMotion.Duration;Frame=heavyHit?6:5;
                }
                else if(state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Windup)
                {
                    next=(state.EnemyAttackCount+1)%2==0?"WindupAlt":"Windup";
                    float desired=MonsterWindupMotion.Clip(state.EnemyAge,state.WarningDuration,clips[next].length);
                    // A queued guide can extend even the final warning second.
                    // Return to the listening hold smoothly instead of jumping
                    // backwards through the authored shoulder/jaw animation.
                    windupSample=playing==next&&!float.IsNaN(windupSample)?Mathf.MoveTowards(windupSample,desired,dt*3.5f):desired;
                    sample=windupSample;
                    Frame=3;travel=AnimatedActor.MonsterAdvance(state);
                }
                // The two-handed slam has its own planted stance. Sampling
                // the lunge underneath it leaves one shin folded sideways.
                if(preview<0&&(MonsterSlamMotion.Active(state)||MonsterRayMotion.Active(state))&&next!="Hurt"){next="Idle";sample=0;}
            }
            else if(state.Phase==GamePhase.Transforming) {next="Transform";sample=state.TransformationAge;Frame=6;}
            else if(state.Phase==GamePhase.Victory) {next="Victory";sample=Mathf.Max(0,phaseAge-VictoryMotion.TurnStartSeconds);Frame=7;}
            else if(state.Phase==GamePhase.Battle)
            {
                if(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)
                {next=state.Action==HeroAction.LeftPunch?"LeftPunch":"RightPunch";sample=state.ActionAge;Frame=sample<.07f?1:2;travel=AnimatedActor.Strike(sample)*PunchTravel(state);}
                else if(state.Action==HeroAction.Beam) {next="Beam";sample=Mathf.Min(1.9f,playing==next?clipAge+dt:0);Frame=4;}
                else if(state.Action==HeroAction.Hurt)
                {
                    next="Hurt";sample=KnockdownMotion.ClipSeconds(state.ActionAge);Frame=5;
                    float p=KnockdownMotion.Weight(state.ActionAge);
                    fallTilt=state.ActionAge<KnockdownMotion.RiseSeconds?-62f*p:
                        Mathf.Lerp(-62,16,RiseStep(state.ActionAge,.68f,1.02f))*(1-RiseStep(state.ActionAge,1.02f,KnockdownMotion.Duration));
                    fallSide=20f*p;
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
            bool changedClip=playing!=next;
            if(changedClip)
            {
                playing=next;clipAge=0;
                // Punch clips already start at the combat stance. A long blend
                // delays their baked foot compensation while the actor root
                // advances, sliding the support foot during the first step.
                blendLeft=!monster&&(next=="LeftPunch"||next=="RightPunch")?.025f:.055f;
            }
            else clipAge+=dt;
            for(int i=0;i<joints.Length;i++) {positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;scales[i]=joints[i].localScale;}
            if(attackInterrupted&&next=="Hurt")
            {
                // Blend out of the raised claws the player saw, not the idle
                // source pose underneath the additive ground-strike layer.
                for(int i=0;i<joints.Length;i++){positions[i]=attackExitPositions[i];rotations[i]=attackExitRotations[i];}
                blendLeft=.14f;
            }
            // Solve the claws in this frame's actor basis. Last frame's breathing
            // yaw or recoil otherwise feeds back into the world-space arm solve,
            // even when the caller samples the same instant again.
            if(monster)
            {
                Root.position=home+forward*travel+Vector3.down*fallDrop;
                Root.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(fallTilt,0,fallSide);
            }
            clips[next].SampleAnimation(model,Mathf.Clamp(sample,0,clips[next].length));
            if(monster&&preview<0)CorrectRestingArms(state);
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack&&!MonsterSlamMotion.Active(state)&&!MonsterRayMotion.Active(state))
                CorrectAttackArms(state);
            if(monster)ApplyClawPose(state,preview,time);
            // Re-rendering a held transition must keep its blend progress. A
            // first explicit sample or pose preview still evaluates immediately.
            float mix=preview>=0||blendLeft<=0||dt<=0&&changedClip?1:dt<=0?0:Mathf.Clamp01(dt/blendLeft);
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
                PoseKnockdown(state.ActionAge);
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&next=="Hurt"&&!launch.Active)
            {
                // Hips yield over anchored feet instead of sliding the entire
                // actor backwards. Chest, head and knees settle at different
                // times; the next punch keeps the remaining root momentum.
                float recoil=heavyHit?(beamRecoil.Active?0:ContactPulse(hitAge,0,.10f,.78f)):MonsterRecoilMotion.Weight(hitAge);
                float carry=MonsterRecoilMotion.Carry(hitAge);
                Root.position+=recoilStart*carry-forward*(heavyHit?.14f:accentHit?.34f:.23f)*recoil;
                Root.position+=Vector3.Cross(Vector3.up,forward)*(contactSide*(heavyHit?.045f:accentHit?.11f:.065f)*recoil);
                Root.rotation*=Quaternion.AngleAxis(recoilStartYaw*carry+contactSide*(heavyHit?4.5f:accentHit?10:4f)*recoil,Vector3.up);
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
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack&&!MonsterSlamMotion.Active(state)&&!MonsterRayMotion.Active(state))
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
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&(next=="Windup"||next=="WindupAlt"))
            {
                float coil=MonsterWindupMotion.Coil(state.EnemyAge);
                float breath=MonsterWindupMotion.Breath(state.EnemyAge,state.WarningDuration);
                float side=(state.EnemyAttackCount+1)%2==0?-1:1;
                var right=Vector3.Cross(Vector3.up,forward);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(-side*11*coil,Vector3.up)
                        *Quaternion.AngleAxis(-1.8f*breath,right)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(side*7*coil,Vector3.up)
                        *Quaternion.AngleAxis(1.2f*breath,right)*head.rotation;
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
                    bool uppercut=MonsterLaunchMotion.Uppercut(state);
                    upperSpine.rotation=Quaternion.AngleAxis(side*(comboStrike?(uppercut?-10:-24):7)*punch,Vector3.up)
                        *Quaternion.AngleAxis((uppercut?7:-6)*punch,right)*upperSpine.rotation;
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
                state.Enemy==EnemyPhase.Rest&&next=="Idle";
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
                if(monsterBreath)wave*=Mathf.SmoothStep(0,1,(hitAge-MonsterRecoilMotion.Duration)/.25f);
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
                    breathLeftArm=leftUpperArm.localRotation;breathRightArm=upperArm.localRotation;breathArmsApplied=true;
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
            if(recoveryWeight>0)
            {
                var right=Vector3.Cross(Vector3.up,forward);
                float envelope=MonsterRecoveryMotion.Weight(state.EnemyAge);
                float blend=envelope>0?recoveryWeight/envelope:0;
                if(!contactLayerApplied)
                {if(upperSpine)spineBase=upperSpine.localRotation;if(head)headBase=head.localRotation;}
                if(upperSpine)upperSpine.rotation=Quaternion.AngleAxis(-MonsterRecoveryMotion.Chest(state.EnemyAge)*blend,right)*upperSpine.rotation;
                if(head)head.rotation=Quaternion.AngleAxis(MonsterRecoveryMotion.HeadTurn(state.EnemyAge)*blend,Vector3.up)
                    *Quaternion.AngleAxis(3*recoveryWeight,right)*head.rotation;
                contactLayerApplied=true;
            }
            if(!monster&&punchLink.Weight>0)
            {
                // Wind the accepted next shoulder while the current fist
                // retracts. The loading pose survives the brief Idle handoff,
                // and is entirely gone at the unchanged punch contact time.
                var right=Vector3.Cross(Vector3.up,forward);
                if(!contactLayerApplied)
                {if(upperSpine)spineBase=upperSpine.localRotation;if(head)headBase=head.localRotation;}
                if(upperSpine)upperSpine.rotation=Quaternion.AngleAxis(-punchLink.Sign*10*punchLink.Weight,Vector3.up)
                    *Quaternion.AngleAxis(1.5f*punchLink.Weight,right)*upperSpine.rotation;
                if(head)head.rotation=Quaternion.AngleAxis(punchLink.Sign*5*punchLink.Weight,Vector3.up)*head.rotation;
                contactLayerApplied=true;
            }
            if(monster&&preview<0&&next=="Hurt"&&state.Phase==GamePhase.Battle)
            {
                // World axes are deliberate: the mirrored Source bones do not
                // share Euler axes. Chest yields first, head follows 35 ms later,
                // then both settle. Left/right punches twist opposite shoulders.
                // A beam sustains the recoil while it is striking the chest,
                // rather than returning to Idle during the visible blast.
                float chest=heavyHit&&beamRecoil.Active?0:ContactPulse(hitAge,0,heavyHit?.12f:.075f,heavyHit?.84f:.46f);
                float follow=heavyHit&&beamRecoil.Active?0:ContactPulse(hitAge,.035f,heavyHit?.20f:.14f,heavyHit?.9f:MonsterRecoilMotion.Duration);
                var right=Vector3.Cross(Vector3.up,forward);
                if(upperSpine)
                {
                    spineBase=upperSpine.localRotation;
                    upperSpine.rotation=Quaternion.AngleAxis(-(heavyHit?20:accentHit?22:13)*chest-18*beamRecoil.Chest,right)
                        *Quaternion.AngleAxis(contactSide*(accentHit?30:18)*chest,Vector3.up)
                        *Quaternion.AngleAxis(contactSide*4*chest,forward)*upperSpine.rotation;
                }
                if(head)
                {
                    headBase=head.localRotation;
                    head.rotation=Quaternion.AngleAxis(-(heavyHit?13:accentHit?12:7)*follow-10*beamRecoil.Weight,right)
                        *Quaternion.AngleAxis(contactSide*7*follow,Vector3.up)*head.rotation;
                }
                contactLayerApplied=true;
                if(!launch.Active)AnchorMonsterFeet();
            }
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&next=="Idle"&&state.Enemy==EnemyPhase.Rest&&!launch.Active)
                AnchorMonsterFeet();
            if((retargetedPunch||chaseAdvance>0)&&preview<0&&state.Phase==GamePhase.Battle&&
                (state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch||
                 (state.Action==HeroAction.None&&heroRecoveryAge<.26f&&!state.Shield)))
                PoseRetargetedFootwork(state);
            if(!monster&&preview<0&&state.Phase==GamePhase.Battle&&state.Action!=HeroAction.Hurt)
                PoseGuardBrace(ContactPulse(guardAge,0,.14f,.64f));
            if(retargetArms)
            {
                if(retargetedPunch)PoseRetargetedArms(state);
                else PoseTigaArms(state,comboStrike);
            }
            if(comboStrike)PoseComboStrike(state);
            if(comboExitAge<.14f)
            {
                comboExitAge+=dt;float weight=1-Mathf.SmoothStep(0,1,comboExitAge/.14f);
                for(int i=0;i<joints.Length;i++)if(comboExitMask[i])
                {comboExitBase[i]=joints[i].localRotation;joints[i].localRotation=Quaternion.Slerp(comboExitBase[i],comboExitStart[i],weight);}
                comboExitApplied=true;
            }
            if(monster&&preview<0&&state.Phase==GamePhase.Battle&&!MonsterSlamMotion.Active(state)&&!MonsterRayMotion.Active(state)&&
                (next=="Windup"||next=="WindupAlt"||next=="Attack"||next=="AttackAlt"||(next=="Idle"&&state.Enemy==EnemyPhase.Recover)))
                PoseMonsterStep(state);
            staggerTravel=Vector3.zero;
            if(monster&&preview<0&&stagger.Active)PoseStaggerStep();
            if(monster&&preview<0&&launch.Active)PoseMonsterLaunch();
            beamTravel=Vector3.zero;
            if(monster&&preview<0&&beamRecoil.Active)PoseBeamRecoveryStep();
            if(monster)PoseClawRoll(state,preview);
            // Keep the tail planted while the torso recoils. It follows heading
            // and travel, but not the pelvis or whole-actor backward hit pitch.
            if(tailJoints!=null&&tailJoints.Length>0)
            {
                for(int i=1;i<tailJoints.Length;i++)
                {tailJoints[i].localRotation=tailRest[i];tailJoints[i].localPosition=tailPositions[i];}
                tailJoints[0].rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(Mathf.Sin(time*1.8f)*5,Vector3.up)*tailRootRotation;
                var anchor=tailJoints[0].position;
                if(state.Phase!=GamePhase.Victory||preview>=0)anchor.y=home.y+tailHeight+launch.Lift;
                else anchor.y=Mathf.Max(home.y+.70f,anchor.y);
                tailJoints[0].position=anchor;
                if(launch.Active)
                {
                    // The tail lags the hip turn and curls upward through its
                    // own joints. Translating a rigid tail with both feet made
                    // the airborne creature look like a lifted standing model.
                    var side=Vector3.Cross(Vector3.up,forward);
                    float follow=launch.TailFollow,sign=launch.Left?-1:1;
                    tailJoints[0].rotation=Quaternion.AngleAxis(sign*12*follow,Vector3.up)
                        *Quaternion.AngleAxis(9*follow,side)*tailJoints[0].rotation;
                    for(int i=1;i<tailJoints.Length;i++)
                    {
                        float along=(float)i/(tailJoints.Length-1);
                        float curl=Mathf.Sin(along*Mathf.PI)*8*follow;
                        tailJoints[i].rotation=Quaternion.AngleAxis(curl,side)*tailJoints[i].rotation;
                    }
                }
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
            if(monster&&preview<0&&MonsterSlamMotion.Active(state)&&next!="Hurt")PoseGroundSlam(state,dt);
            else slamPrepare=0;
            if(monster&&preview<0&&MonsterRayMotion.Active(state)&&next!="Hurt")PoseHeadRay(state,dt);
            else rayPrepare=0;
            if(monster)PoseClawReaction(state,preview);
            if(monster&&jaw)
            {
                // Keep a small breathing gap at rest. The authored windup,
                // attack and hurt clips retain their full opening range.
                bool rest=preview<0&&(state.Phase==GamePhase.Waiting||state.Phase==GamePhase.Battle&&
                    (state.Enemy==EnemyPhase.Rest||state.Enemy==EnemyPhase.Recover)&&next!="Hurt");
                float target=rest?-8-4*recoveryWeight+Mathf.Sin(time*1.7f)*.8f:0;
                jawCorrection=Mathf.MoveTowards(jawCorrection,target,Mathf.Max(0,dt)*120);
                jawBase=jaw.localRotation;jawLayerApplied=true;
                jaw.rotation=Quaternion.AngleAxis(jawCorrection,Root.right)*jaw.rotation;
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
                // Preserve the source lens color. Tiga's eyes must not also
                // receive the blue timer pulse through the core collection.
                Color c=monster?new Color(1,.24f,.055f):mat.GetColor("_Color");
                mat.SetColor("_EmissionColor",c*(monster?.25f+eyeGlow*1.7f:1.05f));
            }
            float coreGlow=!monster&&state.Action==HeroAction.Beam
                ?.55f+.95f*Mathf.Sin(Mathf.Clamp01(state.ActionAge/1.9f)*Mathf.PI):
                !monster&&state.Phase==GamePhase.Transforming?.55f+.35f*Mathf.Sin(phaseAge*8):.08f;
            foreach(var mat in impactMaterials)
                mat.SetColor("_EmissionColor",Color.black);
            if(monster)UpdateSurfaceImpact();else UpdateGuardLight();
            foreach(var mat in coreMaterials)
            {
                Color c=new Color(.10f,.68f,1);
                mat.SetColor("_EmissionColor",c*(.35f+coreGlow*1.6f));
            }
            foreach(var mat in atlasLightMaterials)HeroAtlasLights.SetCharge(mat,coreGlow);
            dissolve?.Tick(state.Phase==GamePhase.Victory&&preview<0?phaseAge:-1);
            // Departure removes actual surface fragments, retaining opaque
            // depth and matching shadows until each fragment disappears.
            poseOpacity=monster&&state.Phase==GamePhase.Victory&&preview<0?(opacity>0?1:0):opacity;SetPresentationOpacity(1);
        }
        void UpdateGuardLight()
        {
            float strength=ContactPulse(guardAge,0,.075f,.42f);
            foreach(var mat in impactMaterials)
            {
                mat.SetVector("_GuardPoint",new Vector4(guardContact.x,guardContact.y,guardContact.z,1.12f));
                mat.SetColor("_GuardColor",new Color(.18f,.58f,1,strength*.62f));
            }
        }
        void UpdateSurfaceImpact()
        {
            float strength=heavyHit
                ?(1-Mathf.SmoothStep(0,1,(hitAge-.74f)/.28f))*(.85f+.15f*Mathf.Sin(hitAge*28)*Mathf.Sin(hitAge*28))
                :1-Mathf.SmoothStep(0,1,(hitAge-.02f)/.22f);
            Vector3 contact=upperSpine?upperSpine.TransformPoint(surfaceContactLocal):BeamContact;
            Color color=heavyHit?new Color(.15f,.60f,1.30f,strength):new Color(1.25f,.65f,.26f,strength);
            foreach(var mat in impactMaterials)
            {
                if(!mat.HasProperty("_ImpactPoint"))continue;
                mat.SetVector("_ImpactPoint",new Vector4(contact.x,contact.y,contact.z,heavyHit?1.05f:.80f));
                mat.SetVector("_ImpactDirection",Root.forward);mat.SetColor("_ImpactColor",color);
            }
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
        void PoseMonsterLaunch()
        {
            if(!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            var facing=Quaternion.LookRotation(forward);var side=Vector3.Cross(Vector3.up,forward);
            float air=launch.Air,sign=launch.Left?-1:1;
            Vector3 travel=-forward*(.18f*launch.Travel);
            launchRootBase=Root.position;launchRotationBase=Root.rotation;launchPoseApplied=true;
            Root.position=home+travel+Vector3.up*(launch.Lift-.14f*launch.Compression);
            // Rotate around the hips, not the feet. The chest is thrown back
            // and sideways while the pelvis stays on the ballistic arc, so the
            // limbs can fold instead of dragging an upright statue upwards.
            Root.rotation=facing;
            Vector3 pivot=pelvis?pelvis.position:Root.position+Vector3.up*1.6f;
            float tumble=launch.Tumble;
            Root.rotation=Quaternion.AngleAxis(-38*tumble,side)*Quaternion.AngleAxis(sign*16*tumble,Vector3.up)
                *Quaternion.AngleAxis(sign*11*tumble,forward)*facing;
            if(pelvis)Root.position+=pivot-pelvis.position;
            if(!stepLegsApplied)
            {
                stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
                stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            }
            // Unequal folded knees and a lifted tail make the whole creature
            // airborne. On landing the soles stay still while the hips compress.
            float leftTuck=launch.Left?.38f:.16f,rightTuck=launch.Left?.16f:.38f;
            Vector3 left=home+facing*leftFootLocal-forward*(.18f*launch.FootTravel(true))+Vector3.up*(launch.Lift+leftTuck*tumble+launch.FootLift(true))+forward*(launch.Left?-.24f:.10f)*tumble;
            Vector3 right=home+facing*rightFootLocal-forward*(.18f*launch.FootTravel(false))+Vector3.up*(launch.Lift+rightTuck*tumble+launch.FootLift(false))+forward*(launch.Left?.10f:-.24f)*tumble;
            float drop=Mathf.Max(LegDrop(leftThigh,leftShin,leftFoot,left),LegDrop(rightThigh,rightShin,rightFoot,right));
            // launchRootBase restores this drop together with the launch pose.
            Root.position-=Vector3.up*drop;
            PoseLimb(leftThigh,leftShin,leftFoot,left,1,forward-side*.20f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,1,forward+side*.20f,rightFootLocal.y);
            leftFoot.rotation=Quaternion.AngleAxis(-16*air,side)*facing*leftFootRest;
            rightFoot.rotation=Quaternion.AngleAxis(-25*air,side)*facing*rightFootRest;
        }
        void PoseGuardBrace(float weight)
        {
            if(weight<=0||!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            // Preserve the current clip's foot placement, including a new
            // counterpunch step. A block may bend the knees but cannot drag
            // either sole or delay a punch/beam until the recoil has finished.
            Vector3 left=leftFoot.position,right=rightFoot.position;
            Quaternion l=leftFoot.rotation,r=rightFoot.rotation;
            Vector3 leftPole=Vector3.ProjectOnPlane(leftShin.position-leftThigh.position,left-leftThigh.position).normalized;
            Vector3 rightPole=Vector3.ProjectOnPlane(rightShin.position-rightThigh.position,right-rightThigh.position).normalized;
            if(leftPole.sqrMagnitude<.01f)leftPole=forward;
            if(rightPole.sqrMagnitude<.01f)rightPole=forward;
            if(!stepLegsApplied)
            {
                stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
                stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            }
            guardTravel=(-forward*.15f+Vector3.down*.14f)*weight;Root.position+=guardTravel;
            PoseLimb(leftThigh,leftShin,leftFoot,left,1,leftPole,left.y-home.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,1,rightPole,right.y-home.y);
            leftFoot.rotation=l;rightFoot.rotation=r;
        }
        void PoseMonsterStep(Battle state)
        {
            if(!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            var facing=Quaternion.LookRotation(forward);var side=Vector3.Cross(Vector3.up,forward);
            Vector3 left=home+facing*leftFootLocal,right=home+facing*rightFootLocal;
            float weight=1;
            if(state.Enemy==EnemyPhase.Windup)weight=Mathf.SmoothStep(0,1,state.EnemyAge/.20f);
            else if(state.Enemy==EnemyPhase.Attack)
            {
                // The opposite leg supports the claw lunge. Plant the moving
                // foot before contact, hold its landing, then lift it to return.
                float age=state.EnemyAge,advance,lift;
                if(age<MonsterStepMotion.LandingSeconds)
                {float t=Mathf.Clamp01(age/MonsterStepMotion.LandingSeconds);advance=AnimatedActor.EnemyAdvance*Mathf.SmoothStep(0,1,t);lift=.20f*Mathf.Sin(t*Mathf.PI);}
                else if(age<MonsterStepMotion.ReturnStartSeconds){advance=AnimatedActor.EnemyAdvance;lift=0;}
                else
                {float t=Mathf.Clamp01((age-MonsterStepMotion.ReturnStartSeconds)/(MonsterStepMotion.ReturnLandingSeconds-MonsterStepMotion.ReturnStartSeconds));advance=AnimatedActor.EnemyAdvance*(1-Mathf.SmoothStep(0,1,t));lift=.16f*Mathf.Sin(t*Mathf.PI);}
                Vector3 step=forward*advance+Vector3.up*lift;
                if(MonsterStepMotion.LeadLeft(state.EnemyAttackCount))left+=step;else right+=step;
            }
            // Lower the hips only as far as the leg lengths need. Keep the
            // authored claw contact in world space while the knees take weight.
            Vector3 clawLeft=leftHand.position,clawRight=hand.position;
            Quaternion rotationLeft=leftHand.rotation,rotationRight=hand.rotation;
            Quaternion footLeft=leftFoot.rotation,footRight=rightFoot.rotation;
            stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
            stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            float drop=Mathf.Max(LegDrop(leftThigh,leftShin,leftFoot,left),LegDrop(rightThigh,rightShin,rightFoot,right));
            stepDrop=drop*weight;Root.position-=Vector3.up*stepDrop;
            PoseLimb(leftThigh,leftShin,leftFoot,left,weight,forward-side*.20f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,weight,forward+side*.20f,rightFootLocal.y);
            leftFoot.rotation=Quaternion.Slerp(footLeft,facing*leftFootRest,weight);
            rightFoot.rotation=Quaternion.Slerp(footRight,facing*rightFootRest,weight);
            if(drop>0)
            {
                stepArmRotations[0]=leftUpperArm.localRotation;stepArmRotations[1]=leftForearm.localRotation;stepArmRotations[2]=leftHand.localRotation;
                stepArmRotations[3]=upperArm.localRotation;stepArmRotations[4]=forearm.localRotation;stepArmRotations[5]=hand.localRotation;stepArmsApplied=true;
                PoseLimb(leftUpperArm,leftForearm,leftHand,clawLeft,1,-side,.30f);
                PoseLimb(upperArm,forearm,hand,clawRight,1,side,.30f);
                leftHand.rotation=rotationLeft;hand.rotation=rotationRight;AlignClawWrists();
            }
        }
        void PoseHeadRay(Battle state,float dt)
        {
            if(!upperSpine||!head||!leftHand||!hand||!leftFoot||!rightFoot)return;
            rayPrepare=Mathf.MoveTowards(rayPrepare,MonsterRayMotion.Prepare(state),dt*3.5f);
            for(int i=0;i<joints.Length;i++){attackRotations[i]=joints[i].localRotation;attackPositions[i]=joints[i].localPosition;}
            attackRootBefore=Root.position;attackRotationBefore=Root.rotation;attackPoseApplied=true;
            var right=Vector3.Cross(Vector3.up,forward);var facing=Quaternion.LookRotation(forward);
            float recoil=state.Enemy==EnemyPhase.Attack?MonsterRayMotion.Recoil(state.EnemyAge):0;
            Vector3 tailAnchor=tailJoints!=null&&tailJoints.Length>0?tailJoints[0].position:Vector3.zero;
            Root.position+=(-forward*.08f-Vector3.up*.045f)*recoil;
            upperSpine.rotation=Quaternion.AngleAxis((-5-6*recoil)*rayPrepare,right)*upperSpine.rotation;
            head.rotation=Quaternion.AngleAxis((9+4*recoil)*rayPrepare,right)*head.rotation;
            // Open the chest and hold the claws beside the ribs. Both feet stay
            // planted; a distant ray must not inherit the forward claw lunge.
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;var wrist=side==0?leftHand:hand;
                Vector3 target=home+right*(sign*.72f)+forward*.70f+Vector3.up*2.40f;
                PoseLimb(side==0?leftUpperArm:upperArm,side==0?leftForearm:forearm,wrist,target,rayPrepare,right*sign+forward*.18f,.22f);
            }
            PoseLimb(leftThigh,leftShin,leftFoot,home+facing*leftFootLocal,1,forward-right*.3f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,home+facing*rightFootLocal,1,forward+right*.3f,rightFootLocal.y);
            leftFoot.rotation=facing*leftFootRest;rightFoot.rotation=facing*rightFootRest;AlignClawWrists();
            if(tailJoints!=null&&tailJoints.Length>0)tailJoints[0].position=tailAnchor;
        }
        void PoseGroundSlam(Battle state,float dt)
        {
            if(!upperSpine||!head||!leftHand||!hand||!leftFoot||!rightFoot)return;
            float desired=MonsterSlamMotion.Prepare(state);
            // A hit can interrupt the last part of the tell. Re-enter the slam
            // gradually instead of snapping both claws overhead on Attack/0.
            slamPrepare=Mathf.MoveTowards(slamPrepare,desired,dt*3.5f);
            for(int i=0;i<joints.Length;i++){attackRotations[i]=joints[i].localRotation;attackPositions[i]=joints[i].localPosition;}
            attackRootBefore=Root.position;attackRotationBefore=Root.rotation;attackPoseApplied=true;
            var right=Vector3.Cross(Vector3.up,forward);var facing=Quaternion.LookRotation(forward);
            Vector3 baseLeft=leftHand.position,baseRight=hand.position;
            Vector3 tailAnchor=tailJoints!=null&&tailJoints.Length>0?tailJoints[0].position:Vector3.zero;
            float down=state.Enemy==EnemyPhase.Attack?MonsterSlamMotion.Down(state.EnemyAge):0;
            Root.position-=Vector3.up*(.80f*down*slamPrepare);
            upperSpine.rotation=Quaternion.AngleAxis(65*down*slamPrepare,right)*upperSpine.rotation;
            head.rotation=Quaternion.AngleAxis(-25*down*slamPrepare,right)*head.rotation;
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                Vector3 raised=home+right*(sign*.55f)+forward*.02f+Vector3.up*3.22f;
                Vector3 ground=home+right*(sign*.50f)+forward*1.05f+Vector3.up*.28f;
                Vector3 neutral=side==0?baseLeft:baseRight;
                bool returning=state.Enemy==EnemyPhase.Recover||state.Enemy==EnemyPhase.Attack&&state.EnemyAge>.50f;
                Vector3 target=returning?Vector3.Lerp(neutral,ground,down):Vector3.Lerp(raised,ground,down);
                var wrist=side==0?leftHand:hand;
                PoseLimb(side==0?leftUpperArm:upperArm,side==0?leftForearm:forearm,wrist,target,slamPrepare,right*sign+forward*.35f,.20f);
                Vector3 aim=Vector3.Lerp(wrist.TransformDirection(palmForwardLocal[side]),Vector3.down+forward*.15f,down*slamPrepare);
                wrist.rotation=Quaternion.FromToRotation(wrist.TransformDirection(palmForwardLocal[side]),aim)*wrist.rotation;
            }
            PoseLimb(leftThigh,leftShin,leftFoot,home+facing*leftFootLocal,1,forward-right*.3f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,home+facing*rightFootLocal,1,forward+right*.3f,rightFootLocal.y);
            leftFoot.rotation=facing*leftFootRest;rightFoot.rotation=facing*rightFootRest;AlignClawWrists();
            if(tailJoints!=null&&tailJoints.Length>0)tailJoints[0].position=tailAnchor;
        }
        void PoseStaggerStep()
        {
            if(!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            var side=Vector3.Cross(Vector3.up,forward);
            Vector3 left=leftFoot.position,right=rightFoot.position;
            Vector3 step=(-forward*.58f+side*(stagger.Left?-.12f:.12f))*stagger.Reach+Vector3.up*stagger.Lift;
            if(stagger.Left)left+=step;else right+=step;
            Quaternion l=leftFoot.rotation,r=rightFoot.rotation;
            if(!stepLegsApplied)
            {
                stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
                stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            }
            staggerTravel=-forward*(.24f*stagger.Root);Root.position+=staggerTravel;
            float drop=Mathf.Max(LegDrop(leftThigh,leftShin,leftFoot,left),LegDrop(rightThigh,rightShin,rightFoot,right));
            stepDrop+=drop;Root.position-=Vector3.up*drop;
            PoseLimb(leftThigh,leftShin,leftFoot,left,1,forward-side*.20f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,1,forward+side*.20f,rightFootLocal.y);
            leftFoot.rotation=(stagger.Left?Quaternion.AngleAxis(stagger.Pitch,side):Quaternion.identity)*l;
            rightFoot.rotation=(!stagger.Left?Quaternion.AngleAxis(stagger.Pitch,side):Quaternion.identity)*r;
        }
        void AnchorMonsterFeet()
        {
            if(!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            var facing=Quaternion.LookRotation(forward);var side=Vector3.Cross(Vector3.up,forward);
            Vector3 left=home+facing*leftFootLocal,right=home+facing*rightFootLocal;
            stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
            stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            stepDrop=Mathf.Max(LegDrop(leftThigh,leftShin,leftFoot,left),LegDrop(rightThigh,rightShin,rightFoot,right));
            Root.position-=Vector3.up*stepDrop;
            PoseLimb(leftThigh,leftShin,leftFoot,left,1,forward-side*.20f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,1,forward+side*.20f,rightFootLocal.y);
            leftFoot.rotation=facing*leftFootRest;rightFoot.rotation=facing*rightFootRest;
        }
        void PoseBeamRecoveryStep()
        {
            if(!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            var facing=Quaternion.LookRotation(forward);var side=Vector3.Cross(Vector3.up,forward);
            Vector3 left=home+facing*leftFootLocal,right=home+facing*rightFootLocal;
            Vector3 step=(-forward*.60f+side*(beamRecoil.Left?-.10f:.10f))*beamRecoil.Reach+Vector3.up*beamRecoil.Lift;
            if(beamRecoil.Left)left+=step;else right+=step;
            if(!stepLegsApplied)
            {
                stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
                stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            }
            beamTravel=-forward*beamRecoil.Travel;Root.position+=beamTravel;
            float drop=Mathf.Max(LegDrop(leftThigh,leftShin,leftFoot,left),LegDrop(rightThigh,rightShin,rightFoot,right));
            stepDrop+=drop;Root.position-=Vector3.up*drop;
            PoseLimb(leftThigh,leftShin,leftFoot,left,1,forward-side*.20f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,1,forward+side*.20f,rightFootLocal.y);
            leftFoot.rotation=(beamRecoil.Left?Quaternion.AngleAxis(beamRecoil.Pitch,side):Quaternion.identity)*facing*leftFootRest;
            rightFoot.rotation=(!beamRecoil.Left?Quaternion.AngleAxis(beamRecoil.Pitch,side):Quaternion.identity)*facing*rightFootRest;
        }
        float PunchTravel(Battle state)=>StrikeAdvance+chaseAdvance;
        void PoseRetargetedFootwork(Battle state)
        {
            if(!leftFoot||!rightFoot||!leftThigh||!rightThigh||!leftShin||!rightShin)return;
            var facing=Quaternion.LookRotation(forward);var side=Vector3.Cross(Vector3.up,forward);
            Vector3 left=home+facing*leftFootLocal,right=home+facing*rightFootLocal;
            float lift=0,advance=0,weight=1,stride=PunchTravel(state);
            if(state.Action==HeroAction.None)weight=1-Mathf.SmoothStep(0,1,heroRecoveryAge/.26f);
            else
            {
                float age=state.ActionAge;
                if(age<Battle.PunchHitSeconds)
                {
                    float t=Mathf.Clamp01(age/Battle.PunchHitSeconds);
                    advance=stride*Mathf.SmoothStep(0,1,t);lift=.12f*Mathf.Sin(t*Mathf.PI);
                }
                else if(age<.18f)advance=stride;
                else
                {
                    float t=Mathf.Clamp01((age-.18f)/(Battle.PunchSeconds-.18f));
                    advance=stride*(1-Mathf.SmoothStep(0,1,t));lift=.09f*Mathf.Sin(t*Mathf.PI);
                }
                Vector3 step=forward*advance+Vector3.up*lift;
                if(state.Action==HeroAction.LeftPunch)left+=step;else right+=step;
                if(chaseAdvance>0)
                {
                    // A pursuit is a short travelling step: the rear heel
                    // releases too, instead of stretching a planted rear leg.
                    Vector3 follow=forward*(chaseAdvance*.55f*AnimatedActor.Strike(age))+
                        Vector3.up*(.075f*Mathf.Sin(Mathf.Clamp01(age/Battle.PunchSeconds)*Mathf.PI));
                    if(state.Action==HeroAction.LeftPunch)right+=follow;else left+=follow;
                }
            }
            // These imported clips lift both feet with the pelvis. Keep the
            // rear sole planted, land the leading foot before impact, and let
            // the knees take the travel instead of sliding the entire model.
            stepLegRotations[0]=leftThigh.localRotation;stepLegRotations[1]=leftShin.localRotation;stepLegRotations[2]=leftFoot.localRotation;
            stepLegRotations[3]=rightThigh.localRotation;stepLegRotations[4]=rightShin.localRotation;stepLegRotations[5]=rightFoot.localRotation;stepLegsApplied=true;
            Quaternion l=leftFoot.rotation,r=rightFoot.rotation;
            stepDrop=Mathf.Max(LegDrop(leftThigh,leftShin,leftFoot,left),LegDrop(rightThigh,rightShin,rightFoot,right))*weight;
            Root.position-=Vector3.up*stepDrop;
            PoseLimb(leftThigh,leftShin,leftFoot,left,weight,forward-side*.15f,leftFootLocal.y);
            PoseLimb(rightThigh,rightShin,rightFoot,right,weight,forward+side*.15f,rightFootLocal.y);
            leftFoot.rotation=Quaternion.Slerp(l,facing*leftFootRest,weight);
            rightFoot.rotation=Quaternion.Slerp(r,facing*rightFootRest,weight);
        }
        Vector3 PunchContact=>opponent!=null?opponent.BeamSurfaceContact-
            Vector3.up*(opponent.LaunchAge<MonsterLaunchMotion.Landing?.36f:0):opponentHome-forward*.33f+Vector3.up*2.48f;
        static float PunchReach(float age)=>age<Battle.PunchHitSeconds?Mathf.SmoothStep(0,1,age/Battle.PunchHitSeconds):
            1-Mathf.SmoothStep(0,1,(age-.15f)/(Battle.PunchSeconds-.15f));
        Vector3 LinkedGuard(Vector3 guard,bool left,Vector3 side)
        {
            if(punchLink.Side!=(left?HeroAction.LeftPunch:HeroAction.RightPunch))return guard;
            return guard+(-forward*.10f-side*((left?-1:1)*.03f)-Vector3.up*.15f)*punchLink.Weight;
        }
        void PoseRetargetedArms(Battle state)
        {
            if(!leftUpperArm||!leftForearm||!leftHand||!upperArm||!forearm||!hand)return;
            bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
            float age=state.ActionAge,reach=0;
            if(punch)reach=PunchReach(age);
            // The same guard is the start and end of both punches. The source
            // clips stretch the other arm out too, obscuring which fist struck.
            // Keep that hand tucked while the striking shoulder transfers weight.
            var side=Vector3.Cross(Vector3.up,forward);
            if(upperSpine&&punch)upperSpine.rotation=Quaternion.AngleAxis(16*reach,side)*upperSpine.rotation;
            if(head&&punch)head.rotation=Quaternion.AngleAxis(-8*reach,side)*head.rotation;
            stepArmRotations[0]=leftUpperArm.localRotation;stepArmRotations[1]=leftForearm.localRotation;stepArmRotations[2]=leftHand.localRotation;
            stepArmRotations[3]=upperArm.localRotation;stepArmRotations[4]=forearm.localRotation;stepArmRotations[5]=hand.localRotation;stepArmsApplied=true;
            Vector3 contact=PunchContact;
            float blend=punch||heroRecoveryAge<.26f?1:Mathf.SmoothStep(0,1,clipAge/.16f);
            for(int i=0;i<2;i++)
            {
                bool left=i==0,active=punch&&(left==(state.Action==HeroAction.LeftPunch));
                var upper=left?leftUpperArm:upperArm;var lower=left?leftForearm:forearm;var wrist=left?leftHand:hand;
                float sign=left?-1:1;
                Vector3 guard=LinkedGuard(upper.position+forward*.28f-side*(sign*.10f)-Vector3.up*.15f,left,side);
                Vector3 target=guard;
                if(active)
                {
                    Vector3 finish=contact-forward*.12f+side*(sign*.10f);
                    target=Vector3.Lerp(guard,finish,reach);
                    // A small outward arc separates the two silhouettes. It
                    // disappears at contact and on return to the shared guard.
                    target+=side*(sign*.10f*Mathf.Sin(reach*Mathf.PI));
                }
                var palm=wrist.rotation;var previousForearm=wrist.position-lower.position;
                PoseLimb(upper,lower,wrist,target,blend,side*(sign*.45f)+Vector3.down,.30f);
                wrist.rotation=Quaternion.FromToRotation(previousForearm,wrist.position-lower.position)*palm;
            }
        }
        void PoseTigaArms(Battle state,bool combo)
        {
            if(!leftUpperArm||!leftForearm||!leftHand||!upperArm||!forearm||!hand)return;
            // The clip owns the torso and stepping. Use one timed fist path:
            // easing toward an already-moving baked hand compounded its early
            // acceleration, especially on the foreground right punch.
            bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
            float age=state.ActionAge;
            float motion=punch&&!combo?PunchReach(age):0;
            float blend=punch||heroRecoveryAge<.26f?1:Mathf.SmoothStep(0,1,clipAge/.16f);
            stepArmRotations[0]=leftUpperArm.localRotation;stepArmRotations[1]=leftForearm.localRotation;stepArmRotations[2]=leftHand.localRotation;
            stepArmRotations[3]=upperArm.localRotation;stepArmRotations[4]=forearm.localRotation;stepArmRotations[5]=hand.localRotation;stepArmsApplied=true;
            var side=Vector3.Cross(Vector3.up,forward);
            for(int i=0;i<2;i++)
            {
                bool left=i==0,active=punch&&!combo&&left==(state.Action==HeroAction.LeftPunch);float sign=left?-1:1;
                var upper=left?leftUpperArm:upperArm;var lower=left?leftForearm:forearm;var wrist=left?leftHand:hand;
                Vector3 guard=LinkedGuard(upper.position+forward*.28f-side*(sign*.10f)-Vector3.up*.15f,left,side);
                Vector3 target=guard;
                if(active)
                {
                    Vector3 finish=PunchContact-forward*.10f+side*(sign*.06f);
                    target=Vector3.Lerp(guard,finish,motion)+side*(sign*.08f*Mathf.Sin(motion*Mathf.PI));
                }
                var palm=wrist.rotation;var span=wrist.position-lower.position;
                Vector3 pole=Vector3.Lerp(side*(sign*.45f)+Vector3.down,side*(sign*.65f)+Vector3.down*.5f,active?motion:0);
                PoseLimb(upper,lower,wrist,target,blend,pole,.30f);
                wrist.rotation=Quaternion.FromToRotation(span,wrist.position-lower.position)*palm;
            }
        }
        void PoseComboStrike(Battle state)
        {
            if(!leftUpperArm||!leftForearm||!leftHand||!upperArm||!forearm||!hand)return;
            // The lead fist sweeps in from outside the shoulder, then folds
            // back to guard. The other hand stays in; feet retain their baked
            // or retargeted support positions and the hit still occurs at .12 s.
            if(!stepArmsApplied)
            {
                stepArmRotations[0]=leftUpperArm.localRotation;stepArmRotations[1]=leftForearm.localRotation;stepArmRotations[2]=leftHand.localRotation;
                stepArmRotations[3]=upperArm.localRotation;stepArmRotations[4]=forearm.localRotation;stepArmRotations[5]=hand.localRotation;stepArmsApplied=true;
            }
            float reach=ComboStrikeMotion.Reach(state.ActionAge),blend=ComboStrikeMotion.Blend(state.ActionAge);
            bool uppercut=MonsterLaunchMotion.Uppercut(state);
            var side=Vector3.Cross(Vector3.up,forward);
            Vector3 contact=PunchContact;
            for(int i=0;i<2;i++)
            {
                bool left=i==0,active=left==(state.Action==HeroAction.LeftPunch);float sign=left?-1:1;
                var upper=left?leftUpperArm:upperArm;var lower=left?leftForearm:forearm;var wrist=left?leftHand:hand;
                Vector3 guard=LinkedGuard(upper.position+forward*.28f-side*(sign*.10f)-Vector3.up*.15f,left,side);
                Vector3 target=guard;
                if(active)
                {
                    target=Vector3.Lerp(guard,contact-forward*.10f+side*(sign*.06f),reach);
                    float arc=state.ActionAge<Battle.PunchHitSeconds?.55f:.24f;
                    if(uppercut)
                    {
                        target+=side*(sign*.15f*Mathf.Sin(reach*Mathf.PI));
                        target+=Vector3.up*(state.ActionAge<Battle.PunchHitSeconds?-.25f*(1-reach):.52f*Mathf.Sin(reach*Mathf.PI));
                    }
                    else target+=side*(sign*arc*Mathf.Sin(reach*Mathf.PI));
                }
                var palm=wrist.rotation;var previousForearm=wrist.position-lower.position;
                PoseLimb(upper,lower,wrist,target,blend,side*(sign*(active?.9f:.45f))+Vector3.down*(active?.2f:1),.30f);
                wrist.rotation=Quaternion.FromToRotation(previousForearm,wrist.position-lower.position)*palm;
            }
        }
        static float LegDrop(Transform hip,Transform knee,Transform ankle,Vector3 target)
        {
            float reach=Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,ankle.position)-.015f;
            float horizontal=Vector3.ProjectOnPlane(hip.position-target,Vector3.up).sqrMagnitude;
            float vertical=Mathf.Sqrt(Mathf.Max(0,reach*reach-horizontal));
            return Mathf.Clamp(hip.position.y-target.y-vertical,0,.28f);
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
        static float RiseStep(float age,float start,float end)=>Mathf.SmoothStep(0,1,(age-start)/(end-start));
        void PoseKnockdown(float age)
        {
            float weight=KnockdownMotion.Weight(age);
            if(!pelvis||!leftThigh||!leftShin||!leftFoot||!rightThigh||!rightShin||!rightFoot||
                !leftUpperArm||!leftForearm||!leftHand||weight<=0)return;
            knockdownRoot=Root.position;knockdownFacing=Root.rotation;
            knockdownRotations[0]=leftThigh.localRotation;knockdownRotations[1]=leftShin.localRotation;knockdownRotations[2]=leftFoot.localRotation;
            knockdownRotations[3]=rightThigh.localRotation;knockdownRotations[4]=rightShin.localRotation;knockdownRotations[5]=rightFoot.localRotation;
            knockdownRotations[6]=leftUpperArm.localRotation;knockdownRotations[7]=leftForearm.localRotation;knockdownRotations[8]=leftHand.localRotation;knockdownApplied=true;
            var side=Vector3.Cross(Vector3.up,forward);
            var facing=Quaternion.LookRotation(forward);
            Vector3 seated=home-forward*.35f+Vector3.up*.43f;
            Vector3 upright=home+facing*pelvisLocal;
            Vector3 crouch=upright+side*.12f;crouch.y=home.y+pelvisLocal.y*.52f;
            Vector3 hips=Vector3.Lerp(seated,crouch,RiseStep(age,.80f,1.12f));
            hips=Vector3.Lerp(hips,upright,RiseStep(age,1.12f,1.62f));
            float landed=age<KnockdownMotion.LandingSeconds?weight:1;
            Root.position+=Vector3.Lerp(pelvis.position,hips,landed)-pelvis.position;
            Vector3 feet=seated+forward*1.12f;
            Vector3 left=feet-side*.32f,right=feet+side*.30f-forward*.20f;
            left.y=home.y+leftFootClearance;right.y=home.y+rightFootClearance;
            // First bring the right foot beneath the hips while the left boot
            // and hand carry the body. Then step the left boot back as the
            // right leg pushes up. Never slide both supports with a fade weight.
            float rightStep=RiseStep(age,.58f,.86f),leftStep=RiseStep(age,.90f,1.30f);
            Vector3 standingLeft=home+facing*leftFootLocal,standingRight=home+facing*rightFootLocal;
            left=Vector3.Lerp(left,standingLeft,leftStep)+Vector3.up*(.14f*Mathf.Sin(leftStep*Mathf.PI));
            right=Vector3.Lerp(right,standingRight,rightStep)+Vector3.up*(.10f*Mathf.Sin(rightStep*Mathf.PI));
            float floorBlend=RiseStep(age,1.38f,1.62f);
            float lc=Mathf.Lerp(leftFootClearance,leftFootLocal.y,floorBlend),rc=Mathf.Lerp(rightFootClearance,rightFootLocal.y,floorBlend);
            PoseLimb(leftThigh,leftShin,leftFoot,left,landed,Vector3.Lerp(Vector3.up,forward,RiseStep(age,.68f,1.02f)),lc);
            PoseLimb(rightThigh,rightShin,rightFoot,right,landed,Vector3.Lerp(Vector3.up,forward,RiseStep(age,.68f,1.02f)),rc);
            leftFoot.rotation=Quaternion.Slerp(leftFoot.rotation,facing*leftFootRest,landed);
            rightFoot.rotation=Quaternion.Slerp(rightFoot.rotation,facing*rightFootRest,landed);
            Vector3 support=seated-side*.68f-forward*.32f;support.y=home.y+.23f;
            Quaternion palm=leftHand.rotation;
            PoseLimb(leftUpperArm,leftForearm,leftHand,support,landed*(1-RiseStep(age,.84f,1.22f)),-side,.23f);
            leftHand.rotation=palm;
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
            float curl=.23f,spread=.8f;
            bool attack=false;
            int attackSide=1;
            if(preview>=0)
            {
                curl=preview==1||preview==3?.12f:preview==2?.37f:preview==5?.27f:preview==7?.18f:.23f;
                attack=preview==2;attackSide=1;
            }
            else if(state.Phase==GamePhase.Battle)
            {
                attack=state.Enemy==EnemyPhase.Attack;attackSide=state.EnemyAttackCount%2==0?-1:1;
                if(state.Enemy==EnemyPhase.Attack)
                {
                    float reach=Mathf.Sin(Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI);
                    curl=.25f+.16f*reach;
                }
                else if(state.Enemy==EnemyPhase.Windup)
                {
                    float wind=Mathf.Clamp01(state.EnemyAge/state.WarningDuration);
                    curl=.10f+.17f*wind+.08f*MonsterWindupMotion.Coil(state.EnemyAge);
                }
                else if(state.Enemy==EnemyPhase.Rest)curl=.23f+Mathf.Sin(time*2.05f+.8f)*.025f;
                else if(state.Enemy==EnemyPhase.Recover)curl=.24f;
                else if(state.Action==HeroAction.Hurt)curl=.27f;
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
                        float fan=sideSpread*(i-1.5f)*5f;
                        first.rotation=Quaternion.AngleAxis(fan,palmUp)*Quaternion.AngleAxis(amount*(52+i*5),curlAxis)*first.rotation;
                    }
                    var tip=clawFingers[i,side,1];
                    if(tip)
                        tip.rotation=Quaternion.AngleAxis(amount*(68+i*7),curlAxis)*tip.rotation;
                }
                var thumb=clawThumbs[side,0];
                var thumbTip=clawThumbs[side,1];
                if(thumb&&thumbTip)
                {
                    // The thumb opposes the fingers; sharing their curl axis
                    // bends its diagonal bone sideways instead of closing a claw.
                    Vector3 palmCenter=(clawFingers[1,side,0].position+clawFingers[2,side,0].position)*.5f;
                    Vector3 opposition=Vector3.Cross(thumbTip.position-thumb.position,palmCenter-thumb.position).normalized;
                    if(opposition.sqrMagnitude>.5f)
                    {
                        thumb.rotation=Quaternion.AngleAxis(amount*42,opposition)*thumb.rotation;
                        thumbTip.rotation=Quaternion.AngleAxis(amount*58,opposition)*thumbTip.rotation;
                    }
                }
            }
        }
        void PoseClawReaction(Battle state,int preview)
        {
            if(preview>=0||state.Phase!=GamePhase.Battle||state.Enemy==EnemyPhase.Attack||
                (MonsterSlamMotion.Active(state)||MonsterRayMotion.Active(state))&&playing!="Hurt"&&state.Enemy!=EnemyPhase.Recover)
            {clawLeft=clawRight=clawCarryLeft=clawCarryRight=Vector3.zero;return;}
            if(!upperArm||!forearm||!hand||!leftUpperArm||!leftForearm||!leftHand)return;
            var side=Vector3.Cross(Vector3.up,forward);
            for(int i=0;i<2;i++)
            {
                bool left=i==0;float sign=left?-1:1;
                var pose=MonsterClawMotion.Sample(left,hitAge,contactSide,accentHit,launch.Age,beamRecoil.Age);
                Vector3 offset=side*pose.X+Vector3.up*pose.Y+forward*pose.Z;
                offset+=(left?clawCarryLeft:clawCarryRight)*MonsterClawMotion.Carry(hitAge);
                offset+=(-forward*.12f+Vector3.down*.18f+side*(sign*.06f))*recoveryWeight;
                if(left)clawLeft=offset;else clawRight=offset;
                var upper=left?leftUpperArm:upperArm;var lower=left?leftForearm:forearm;var wrist=left?leftHand:hand;
                int at=i*3;clawReactionBase[at]=upper.localRotation;clawReactionBase[at+1]=lower.localRotation;clawReactionBase[at+2]=wrist.localRotation;
                if(offset.sqrMagnitude<.0000001f)continue;
                var palm=wrist.rotation;var arm=wrist.position-lower.position;
                Vector3 axis=wrist.position-upper.position;
                Vector3 oldPole=Vector3.ProjectOnPlane(lower.position-upper.position,axis).normalized;
                Vector3 pole=Vector3.Slerp(oldPole,side*sign+Vector3.down*.45f,Mathf.Clamp01(offset.magnitude*1.3f));
                PoseLimb(upper,lower,wrist,wrist.position+offset,1,pole,.35f);
                wrist.rotation=Quaternion.FromToRotation(arm,wrist.position-lower.position)*palm;
            }
            // Both sides must be restorable even when the lagging side is still
            // at zero during the first few milliseconds of a contact.
            clawReactionApplied=ClawReactionAmount>.0003f;
            if(clawReactionApplied)AlignClawWrists();
        }
        void PoseClawRoll(Battle state,int preview)
        {
            // Turn the palms partly toward one another to expose the curved
            // fingers. A palm-down pose on both sides reads as two flat paddles.
            // Share axial roll with the forearm so the wrist skin does not take
            // the whole twist. Rotating about elbow -> wrist preserves contact.
            clawRollBase[0]=leftForearm.localRotation;clawRollBase[1]=leftHand.localRotation;
            clawRollBase[2]=forearm.localRotation;clawRollBase[3]=hand.localRotation;
            clawRollApplied=true;
            bool attack=preview==2||preview<0&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Attack;
            bool leadLeft=preview<0&&state.EnemyAttackCount%2==0;
            float reach=attack?Mathf.Sin(Mathf.Clamp01((preview==2?.4f:state.EnemyAge)/Battle.EnemyAttackSeconds)*Mathf.PI):0;
            for(int side=0;side<2;side++)
            {
                var lower=side==0?leftForearm:forearm;var wrist=side==0?leftHand:hand;
                Vector3 axis=(wrist.position-lower.position).normalized;
                bool lead=attack&&(side==0)==leadLeft;
                float outward=Mathf.Lerp(.95f,lead?.35f:.80f,reach);
                Vector3 desired=Root.up*.30f+Root.right*((side==0?-1:1)*outward);
                Vector3 source=Vector3.ProjectOnPlane(wrist.TransformDirection(palmUpLocal[side]),axis);
                desired=Vector3.ProjectOnPlane(desired,axis);
                if(source.sqrMagnitude<.001f||desired.sqrMagnitude<.001f)continue;
                float roll=Mathf.Clamp(Vector3.SignedAngle(source,desired,axis),-55,55);
                Quaternion palm=wrist.rotation;
                lower.rotation=Quaternion.AngleAxis(roll*.60f,axis)*lower.rotation;
                wrist.rotation=Quaternion.AngleAxis(roll,axis)*palm;
            }
        }
        static float ContactPulse(float age,float start,float peak,float end)
        {return age<=start||age>=end?0:age<peak?Mathf.SmoothStep(0,1,(age-start)/(peak-start)):1-Mathf.SmoothStep(0,1,(age-peak)/(end-peak));}
    }
}
