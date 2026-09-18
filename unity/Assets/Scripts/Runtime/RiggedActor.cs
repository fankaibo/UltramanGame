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
        readonly GameObject model;
        readonly bool monster;
        readonly Vector3 home, forward;
        readonly Dictionary<string, AnimationClip> clips=new Dictionary<string, AnimationClip>();
        readonly Transform[] joints;
        readonly Renderer[] surfaces;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;
        readonly List<Material> materials=new List<Material>();
        string playing;
        float clipAge, phaseAge, blendLeft, hitAge=10, lastHealth, poseOpacity=1;
        GamePhase previous;
        bool heavyHit;
        Transform hand,leftHand,forearm,leftFoot,rightFoot;
        Transform head,upperSpine;
        Quaternion headBase,spineBase;
        bool contactLayerApplied;
        float contactSide;
        Transform[] tailJoints;Quaternion[] tailRest;Vector3[] tailPositions;Quaternion tailRootRotation;float tailHeight;
        public Vector3 StrikeOrigin(HeroAction action) => action==HeroAction.LeftPunch&&leftHand?leftHand.position:HandPosition;
        public Vector3 HandPosition => hand?hand.position:Root.position+Vector3.up*2.2f;
        public Vector3 EnemyStrikeOrigin(int attackCount) => attackCount%2==0&&leftHand?leftHand.position:HandPosition;
        public Vector3 BeamOrigin => hand&&forearm?Vector3.Lerp(forearm.position,hand.position,.6f):HandPosition;
        public Vector3 FootPosition(bool left) => (left?leftFoot:rightFoot)?(left?leftFoot:rightFoot).position:Root.position;

        public static RiggedActor CreateIfAvailable(string name,Vector3 position,Vector3 opponent,bool monster)
        {
            string path="Characters/"+name+"/"+name;
            var prefab=Resources.Load<GameObject>(path);
            return prefab?new RiggedActor(name,path,prefab,position,opponent,monster):null;
        }
        RiggedActor(string name,string path,GameObject prefab,Vector3 position,Vector3 opponent,bool isMonster)
        {
            monster=isMonster;home=position;forward=Vector3.ProjectOnPlane(opponent-position,Vector3.up).normalized;
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
                if(joint.name=="Foot_L"||joint.name=="bip_foot_L")leftFoot=joint;
                if(joint.name=="Foot_R"||joint.name=="bip_foot_R")rightFoot=joint;
                if(monster&&joint.name=="bip_head")head=joint;
                if(monster&&joint.name=="bip_spine_2")upperSpine=joint;
            }
            if(!hand||!leftHand)throw new InvalidOperationException(name+" is missing a left or right strike bone");
            if(monster)
            {
                var tails=new List<Transform>();foreach(var joint in joints)if(joint.name.StartsWith("tail_",StringComparison.Ordinal))tails.Add(joint);
                tails.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));tailJoints=tails.ToArray();tailRest=new Quaternion[tailJoints.Length];tailPositions=new Vector3[tailJoints.Length];
                for(int i=0;i<tailJoints.Length;i++){tailRest[i]=tailJoints[i].localRotation;tailPositions[i]=tailJoints[i].localPosition;}
                if(tailJoints.Length>0){tailHeight=Root.InverseTransformPoint(tailJoints[0].position).y;tailRootRotation=Quaternion.Inverse(Root.rotation)*tailJoints[0].rotation;}
            }
            positions=new Vector3[joints.Length];scales=new Vector3[joints.Length];rotations=new Quaternion[joints.Length];
            Root.position=home;Root.rotation=Quaternion.LookRotation(forward,Vector3.up);
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
                else if(state.Phase==GamePhase.Victory) {next="Defeat";sample=phaseAge;Frame=7;opacity=1-Mathf.SmoothStep(0,1,(phaseAge-1.5f)/1.5f);}
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
            else if(state.Phase==GamePhase.Victory) {next="Victory";sample=phaseAge;Frame=7;}
            else if(state.Phase==GamePhase.Battle)
            {
                if(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)
                {next=state.Action==HeroAction.LeftPunch?"LeftPunch":"RightPunch";sample=state.ActionAge;Frame=sample<.07f?1:2;travel=AnimatedActor.Strike(sample)*AnimatedActor.PunchAdvance;}
                else if(state.Action==HeroAction.Beam) {next="Beam";sample=Mathf.Min(1.9f,playing==next?clipAge+dt:0);Frame=4;}
                else if(state.Action==HeroAction.Hurt)
                {
                    next="Hurt";sample=state.ActionAge;Frame=5;
                    float p=Mathf.Sin(Mathf.Clamp01(state.ActionAge/.55f)*Mathf.PI);
                    fallTilt=-28f*p;fallDrop=.32f*p;
                    // Roll toward the camera-facing side so the fall reads in the
                    // fixed 45-degree battle composition instead of looking like a lean.
                    fallSide=54f*p;
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
            if(playing!=next) {playing=next;clipAge=0;blendLeft=.055f;}
            else clipAge+=dt;
            for(int i=0;i<joints.Length;i++) {positions[i]=joints[i].localPosition;rotations[i]=joints[i].localRotation;scales[i]=joints[i].localScale;}
            clips[next].SampleAnimation(model,Mathf.Clamp(sample,0,clips[next].length));
            float mix=preview>=0||dt<=0||blendLeft<=0?1:Mathf.Clamp01(dt/blendLeft);
            blendLeft=Mathf.Max(0,blendLeft-dt);
            for(int i=1;i<joints.Length&&mix<1;i++)
            {joints[i].localPosition=Vector3.Lerp(positions[i],joints[i].localPosition,mix);joints[i].localRotation=Quaternion.Slerp(rotations[i],joints[i].localRotation,mix);joints[i].localScale=Vector3.Lerp(scales[i],joints[i].localScale,mix);}
            Root.position=home+forward*travel+Vector3.down*fallDrop;
            Root.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(fallTilt,0,fallSide);
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
                var anchor=tailJoints[0].position;anchor.y=home.y+tailHeight;tailJoints[0].position=anchor;
            }
            poseOpacity=opacity;SetPresentationOpacity(1);
        }
        static float ContactPulse(float age,float start,float peak,float end)
        {return age<=start||age>=end?0:age<peak?Mathf.SmoothStep(0,1,(age-start)/(peak-start)):1-Mathf.SmoothStep(0,1,(age-peak)/(end-peak));}
    }
}
