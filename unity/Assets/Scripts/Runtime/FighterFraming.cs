using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Fit animated body landmarks, not the importer's static skinned bounds
    // (which include Golza's tail and the whole animation's travel).
    public sealed class FighterFraming
    {
        struct Landmark { public Transform Bone; public float Radius; }
        readonly List<Landmark> landmarks=new List<Landmark>();
        float weight,zoom;
        public float Weight=>weight;
        public void Clear(){weight=zoom=0;}
        public void Bind(AnimatedActor hero,AnimatedActor enemy)
        {
            landmarks.Clear();Add(hero,false);Add(enemy,true);Clear();
        }
        void Add(AnimatedActor actor,bool monster)
        {
            if(actor==null||!actor.IsRigged)return;
            foreach(var bone in actor.Root.GetComponentsInChildren<Transform>())
            {
                string n=bone.name;float radius=0;
                if(n=="head"||n=="bip_head")radius=monster?.44f:actor.Root.name.Contains("Zero")?.46f:.30f;
                else if(n=="HandBase_L"||n=="HandBase_R"||n=="bip_hand_L"||n=="bip_hand_R")radius=monster?.25f:.16f;
                else if(n=="Foot_L"||n=="Foot_R"||n=="bip_foot_L"||n=="bip_foot_R")radius=.16f;
                else if(n=="hip"||n=="bip_pelvis")radius=.30f;
                if(radius>0)landmarks.Add(new Landmark{Bone=bone,Radius=radius});
            }
        }
        public void Tick(float desired,float dt)
        {
            if(dt<=0)return;
            float tau=desired>weight?.38f:.16f;
            weight=Mathf.Lerp(desired,weight,Mathf.Exp(-dt/tau));
            if(weight<.0001f)weight=0;
        }
        public float RequiredFieldOfView(Camera camera)
        {
            float tangent=0;
            foreach(var point in landmarks)
            {
                if(!point.Bone)continue;
                var p=camera.transform.InverseTransformPoint(point.Bone.position);
                float z=Mathf.Max(.1f,p.z-point.Radius);
                // Six percent rail on each edge, with extra lower room for
                // the instruction and the contact dust. Radius covers skin.
                float vertical=(Mathf.Abs(p.y)+point.Radius)/(z*(p.y>=0?.80f:.86f));
                float horizontal=(Mathf.Abs(p.x)+point.Radius)/(z*.84f*camera.aspect);
                tangent=Mathf.Max(tangent,vertical,horizontal);
            }
            return 2*Mathf.Atan(tangent)*Mathf.Rad2Deg;
        }
        public void Apply(Camera camera,float dt,float dedicated)
        {
            float strength=weight*(1-Mathf.Clamp01(dedicated));
            if(strength<=0||landmarks.Count==0){zoom=0;return;}
            float available=Mathf.Clamp(camera.fieldOfView-RequiredFieldOfView(camera),0,4.4f);
            // Store a lens correction, not the absolute lens of a previous shot.
            // A dedicated camera must never inherit the ordinary shot's FOV.
            if(dt>0)zoom=available<zoom?available:Mathf.Lerp(available,zoom,Mathf.Exp(-dt/.55f));
            camera.fieldOfView-=Mathf.Min(available,zoom)*strength;
        }
    }
}
