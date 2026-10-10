using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class KaijuSkinReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/kaiju-skin"));
        public static void Release()
        {
            const string asset="Assets/Resources/Characters/Golza/GolzaBodyHD.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(asset);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.alphaSource=TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            var shader=Resources.Load<Shader>("KaijuSurface");
            if(!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Kaiju shader failed");
            Render("original",true);Render("after",false);
            if(File.ReadAllText(Folder+"/original/poses.txt")!=File.ReadAllText(Folder+"/after/poses.txt"))
                throw new Exception("Material replacement changed the skeleton");
            Debug.Log("[KaijuSkinReview] passed bodyMaterials=3 originalEyes=True identicalPoses=True views=28 turntableFrames=240");
        }
        static void Render(string version,bool original)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(935);
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=new Battle();hero.Root.gameObject.SetActive(false);
            var materials=new HashSet<Material>();foreach(var r in enemy.Root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)
                if(m.shader.name=="Training/KaijuSurface")materials.Add(m);
            if(materials.Count!=3)throw new Exception("Expected three Golza body materials");
            var hd=Resources.Load<Texture2D>("Characters/Golza/GolzaBodyHD");
            if(!hd||hd.width!=724||hd.height!=2172||hd.mipmapCount<2)throw new Exception("HD import resized or removed mipmaps");
            foreach(var m in materials)
            {
                if(m.mainTexture!=hd||m.mainTextureScale!=new Vector2(.75f,1)||m.mainTextureOffset!=new Vector2(.125f,0))
                    throw new Exception("Runtime HD binding failed");
                if(original)
                {
                    m.mainTexture=Resources.Load<Texture2D>("Characters/Golza/GolzaBody");
                    m.mainTextureScale=Vector2.one;m.mainTextureOffset=Vector2.zero;
                    m.SetFloat("_Metallic",.03f);m.SetFloat("_Glossiness",.22f);
                }
            }
            int eyes=0;foreach(var r in enemy.Root.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)
                if(m.name.Contains("Eyes"))
                {
                    if(m.mainTexture!=Resources.Load<Texture2D>("Characters/Golza/GolzaEyes")||m.mainTextureScale!=Vector2.one||m.mainTextureOffset!=Vector2.zero)
                        throw new Exception("Eye texture was replaced or cropped");
                    eyes++;
                }
            if(eyes==0)throw new Exception("Missing eye material");
            var cam=new GameObject("Kaiju material inspection").AddComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<ContactShadows.ActorLayer;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.067f,.082f);cam.allowHDR=true;cam.fieldOfView=34;cam.aspect=4f/3;
            var rt=new RenderTexture(1280,960,24){antiAliasing=4};rt.Create();cam.targetTexture=rt;bool fog=RenderSettings.fog;RenderSettings.fog=false;
            var joints=new StringBuilder();
            try
            {
                foreach(int pose in new[]{0,2,5})foreach(int angle in new[]{-55,0,55,180})
                {
                    enemy.Update(state,world.Camera,0,0,pose);
                    Vector3 offset=Quaternion.AngleAxis(angle,Vector3.up)*(-world.BattleAxis*6.9f);
                    cam.transform.position=world.EnemyHome+offset+Vector3.up*2.3f;cam.transform.LookAt(world.EnemyHome+Vector3.up*1.85f);
                    CharacterReview.Save(cam,rt,$"{folder}/pose-{pose}-{angle}.png");
                    foreach(var bone in enemy.Root.GetComponentsInChildren<Transform>())joints.AppendLine(bone.name+" "+bone.position.ToString("F5")+" "+bone.rotation.ToString("F5"));
                }
                enemy.Update(state,world.Camera,0,0,0);
                cam.transform.position=world.EnemyHome-world.BattleAxis*3.7f+Vector3.up*3.1f;cam.transform.LookAt(world.EnemyHome+Vector3.up*2.85f);
                CharacterReview.Save(cam,rt,folder+"/head.png");
                cam.transform.position=world.EnemyHome-world.BattleAxis*3.8f+Vector3.up*2.5f;cam.transform.LookAt(enemy.HandPosition);
                CharacterReview.Save(cam,rt,folder+"/claw.png");
                for(int frame=0;frame<120;frame++)
                {
                    enemy.Update(state,world.Camera,0,0,0);
                    cam.transform.position=world.EnemyHome+Quaternion.AngleAxis(frame*3,Vector3.up)*(-world.BattleAxis*6.9f)+Vector3.up*2.3f;
                    cam.transform.LookAt(world.EnemyHome+Vector3.up*1.85f);
                    CharacterReview.Save(cam,rt,$"{folder}/frames/{frame:D4}.png");
                }
            }
            finally{RenderSettings.fog=fog;cam.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            File.WriteAllText(folder+"/poses.txt",joints.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Resources/KaijuSurface.shader","Resources/Characters/Golza/Golza.fbx","Resources/Characters/Golza/GolzaBody.png","Resources/Characters/Golza/GolzaEyes.png","Resources/Characters/Golza/GolzaBodyHD.png","Resources/Characters/Golza/GolzaBodyHD.png.meta","Editor/KaijuSkinReview.cs"})
                    sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            string result=$"[KaijuSkinReview] {version} passed views=14 turntableFrames=120 bodyMaterials={materials.Count} originalEyes={eyes} hd={hd.width}x{hd.height} mipmaps={hd.mipmapCount}";
            File.WriteAllText(folder+"/validation.txt",result);Debug.Log(result);
        }
    }
}
