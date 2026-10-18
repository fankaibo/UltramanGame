using System;
using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Zero's original head blades dock beside the timer for Twin Shoot. The
    // child still uses the shared finisher gesture; this owns presentation only.
    public sealed class ZeroTwinShoot
    {
        readonly Transform root,chest;
        readonly ZeroSluggerRig blades;
        readonly Transform[] arms;
        readonly Quaternion[] baseArms=new Quaternion[6];
        readonly Vector3[] entryHands=new Vector3[2];
        readonly Vector3 timerLocal;
        readonly Vector3 chestForward,chestUp;
        readonly Action<Transform,Transform,Transform,Vector3,float,Vector3,float> solve;
        Battle observed;
        bool wasBeam,applied;
        float age,returnAge=1;
        public bool Active=>wasBeam||returnAge<.55f;
        public float Dock=>wasBeam?Smooth(age,.30f,.85f):1-Smooth(returnAge,0,.38f);
        public float Age=>age;
        public int TimerVertices {get;}
        public Vector3 Muzzle=>chest.TransformPoint(timerLocal)+Forward*.07f;
        Vector3 Forward=>chest.TransformDirection(chestForward).normalized;
        Vector3 Up=>chest.TransformDirection(chestUp).normalized;
        public ZeroTwinShoot(Transform root,Transform chest,Renderer[] surfaces,ZeroSluggerRig blades,Transform[] arms,
            Action<Transform,Transform,Transform,Vector3,float,Vector3,float> solve)
        {
            this.root=root;this.chest=chest;this.blades=blades;this.arms=arms;this.solve=solve;
            Vector3 center=Vector3.zero;int count=0;var mesh=new Mesh();
            foreach(var renderer in surfaces)
            {
                if(!(renderer is SkinnedMeshRenderer skin))continue;
                skin.BakeMesh(mesh,true);var verts=mesh.vertices;var mats=skin.sharedMaterials;
                for(int sub=0;sub<mats.Length;sub++)
                {
                    if(mats[sub].name.IndexOf("colortimer",StringComparison.OrdinalIgnoreCase)<0)continue;
                    foreach(int v in new HashSet<int>(mesh.GetTriangles(sub))){center+=skin.transform.TransformPoint(verts[v]);count++;}
                }
            }
            if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);
            if(count==0)throw new InvalidOperationException("Zero timer surface is missing");
            TimerVertices=count;timerLocal=chest.InverseTransformPoint(center/count);
            chestForward=chest.InverseTransformDirection(root.forward);chestUp=chest.InverseTransformDirection(Vector3.up);
            Debug.Log($"[ZeroTwinShoot] timerVertices={count} originalSluggers=True");
        }
        static float Smooth(float value,float from,float to)=>Mathf.SmoothStep(0,1,(value-from)/(to-from));
        public void Clear(){Restore();wasBeam=false;returnAge=1;age=0;observed=null;}
        public void Restore()
        {
            if(!applied)return;
            for(int i=0;i<6;i++)arms[i].localRotation=baseArms[i];applied=false;
        }
        public void Observe(Battle state,float dt,int preview)
        {
            bool same=ReferenceEquals(observed,state);
            if(!same||preview>=0||(state.Phase!=GamePhase.Battle&&state.Phase!=GamePhase.Victory))
            {observed=state;wasBeam=false;returnAge=1;age=0;Restore();return;}
            bool beam=state.Phase==GamePhase.Battle&&state.Action==HeroAction.Beam;
            if(beam&&!wasBeam)
            {age=0;returnAge=1;for(int i=0;i<2;i++)entryHands[i]=root.InverseTransformPoint(arms[i*3+2].position);}
            else if(beam)age+=Mathf.Max(0,dt);
            else if(wasBeam)returnAge=0;
            else returnAge+=Mathf.Max(0,dt);
            wasBeam=beam;
            if(!beam&&state.Phase==GamePhase.Battle&&(state.IsPunch||state.Shield||state.Action==HeroAction.Hurt))returnAge=1;
            Restore();
        }
        Vector3 DockCenter(int i)=>Muzzle+Vector3.Cross(Up,Forward)*((i==0?-1:1)*.27f)-Up*.02f;
        public Vector3 BladeTarget(int i)
        {
            var start=blades.MountedCenter(i);var end=DockCenter(i);float t=Dock;
            float sign=i==0?-1:1;
            return Vector3.Lerp(start,end,t)+Vector3.Cross(Up,Forward)*(sign*.23f*Mathf.Sin(t*Mathf.PI))+Forward*(.22f*Mathf.Sin(t*Mathf.PI));
        }
        public void PoseWeapons()
        {
            if(!Active)return;
            for(int i=0;i<2;i++)
            {
                float sign=i==0?-1:1;
                var rest=blades.MountedRotation(i);
                var dock=Quaternion.AngleAxis(sign*26,Forward)*Quaternion.AngleAxis(sign*90,Up)*rest;
                blades.Pose(i,BladeTarget(i),Quaternion.Slerp(rest,dock,Dock));
            }
        }
        public void PoseArms()
        {
            if(!Active)return;
            for(int i=0;i<6;i++)baseArms[i]=arms[i].localRotation;applied=true;
            var side=Vector3.Cross(Up,Forward);
            for(int i=0;i<2;i++)
            {
                float sign=i==0?-1:1;int j=i*3;
                var wrist=arms[j+2];var target=BladeTarget(i)+side*(sign*.11f)-Up*.08f;
                if(wasBeam)target=Vector3.Lerp(root.TransformPoint(entryHands[i]),target,Smooth(age,0,.30f));
                else target=Vector3.Lerp(target,wrist.position,Smooth(returnAge,.38f,.55f));
                var palm=wrist.rotation;var span=wrist.position-arms[j+1].position;
                solve(arms[j],arms[j+1],wrist,target,1,side*sign+Vector3.down*.55f,.30f);
                wrist.rotation=Quaternion.FromToRotation(span,wrist.position-arms[j+1].position)*palm;
            }
        }
    }
}
