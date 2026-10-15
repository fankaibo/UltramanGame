using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Project the frozen hero's actual mesh onto the ground in the Fuji photo.
    // This is a small, one-time silhouette bake, not a disk beneath the actor.
    // All retained GPU resources belong to the photo root; retakes reuse them.
    public static class PhotoGroundShadow
    {
        public static void Create(Transform parent,Transform hero)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            float front=float.PositiveInfinity;
            foreach(var filter in hero.GetComponentsInChildren<MeshFilter>())
            {
                var surface=filter.GetComponent<MeshRenderer>();
                if(!surface||!surface.enabled||!filter.sharedMesh)continue;
                int offset=vertices.Count;
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var p=parent.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    vertices.Add(p);if(p.y< -3.35f)front=Mathf.Min(front,p.z);
                }
                foreach(int index in filter.sharedMesh.triangles)indices.Add(offset+index);
            }
            if(vertices.Count==0||float.IsInfinity(front))throw new InvalidOperationException("Photo shadow requires frozen boots");
            var colors=new Color[vertices.Count];Bounds bounds=default;
            for(int i=0;i<vertices.Count;i++)
            {
                var p=vertices[i];float h=Mathf.Max(0,p.y+3.9f);
                // Moonlight arrives from the photograph's upper left. Floor
                // depth compresses toward the horizon; contact stays at soles.
                vertices[i]=new Vector3(p.x+h*.34f,-3.9f+h*.12f+(p.z-front)*.14f,0);
                colors[i]=new Color(1-Mathf.SmoothStep(0,1,h/.35f),Mathf.Exp(-h*.6f),0,1);
                if(i==0)bounds=new Bounds(vertices[i],Vector3.zero);else bounds.Encapsulate(vertices[i]);
            }
            bounds.Expand(new Vector3(.32f,.32f,0));
            var mesh=new Mesh{name="Projected frozen photo hero",indexFormat=IndexFormat.UInt32};
            var material=new Material(Resources.Load<Shader>("PhotoGroundMask"));GameObject temporary=null;
            var previous=RenderTexture.active;
            try
            {
                mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.colors=colors;mesh.RecalculateBounds();
                temporary=new GameObject("Photo shadow bake");temporary.transform.SetParent(parent,false);
                var caster=new GameObject("Projected silhouette",typeof(MeshFilter),typeof(MeshRenderer));
                caster.layer=29;caster.transform.SetParent(temporary.transform,false);
                caster.GetComponent<MeshFilter>().sharedMesh=mesh;caster.GetComponent<MeshRenderer>().sharedMaterial=material;
                var capture=new GameObject("Photo ground mask camera").AddComponent<Camera>();capture.transform.SetParent(temporary.transform,false);
                capture.transform.localPosition=new Vector3(bounds.center.x,bounds.center.y,-5);
                capture.enabled=false;capture.orthographic=true;capture.orthographicSize=bounds.size.y*.5f;
                capture.aspect=bounds.size.x/bounds.size.y;capture.nearClipPlane=.1f;capture.farClipPlane=10;
                capture.cullingMask=1<<29;capture.clearFlags=CameraClearFlags.SolidColor;capture.backgroundColor=Color.black;
                capture.allowHDR=false;capture.allowMSAA=false;
                int height=Mathf.Clamp(Mathf.RoundToInt(512/capture.aspect),64,512);
                var mask=RuntimeResources.Own(parent,new RenderTexture(512,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                    {name="Photo hero ground mask",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear});
                mask.Create();capture.targetTexture=mask;capture.Render();capture.targetTexture=null;
                var receiver=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("PhotoGroundShadow"))
                    {name="Photo hero ground shadow",mainTexture=mask,renderQueue=1500});
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Photo hero ground shadow";quad.layer=31;
                quad.transform.SetParent(parent,false);quad.transform.localPosition=new Vector3(bounds.center.x,bounds.center.y,1.5f);
                quad.transform.localScale=new Vector3(bounds.size.x,bounds.size.y,1);
                var renderer=quad.GetComponent<MeshRenderer>();renderer.sharedMaterial=receiver;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;Release(quad.GetComponent<Collider>());
                Debug.Log($"[PhotoGroundShadow] vertices={vertices.Count} mask=512x{height} baked=once");
            }
            finally
            {
                RenderTexture.active=previous;
                // Destroy is deferred in a player, so disable the bake objects
                // immediately before any composition or clean-plate render.
                if(temporary)temporary.SetActive(false);
                Release(temporary);Release(material);Release(mesh);
            }
        }
        static void Release(UnityEngine.Object value)
        {if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
