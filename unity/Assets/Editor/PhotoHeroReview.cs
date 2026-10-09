using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PhotoHeroReview
    {
        static string Folder
        {
            get {var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--photo-output");
                return Path.GetFullPath(at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.dataPath,"../../artifacts/photo-3d/after"));}
        }
        public static void Run()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");var report=new StringBuilder();
            for(int i=0;i<HeroRoster.Count;i++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var id=HeroRoster.At(i).Id;
                var assetPath="Assets/Resources/Characters/"+id+"/"+id+".fbx";
                var importer=AssetImporter.GetAtPath(assetPath) as ModelImporter;
                // Zeta and DeckerStrong remain documented roster placeholders until their
                // licensed/readable FBX files are supplied. A missing importer must be an
                // explicit review result, rather than a null-reference failure that hides
                // the usable heroes' photo evidence.
                if(importer==null)
                {
                    report.AppendLine($"{id}: model=fallback-skipped photo=skipped reason=missing-fbx asset={assetPath}");
                    continue;
                }
                if(!importer.isReadable)throw new Exception("Player cannot read photo head weights or helmet vertices: "+id);
                var world=new GameWorld();
                var battleHero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(battleHero,enemy);
                var state=new Battle();battleHero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                var masks=lights.Select(l=>l.cullingMask).ToArray();var enabled=lights.Select(l=>l.enabled).ToArray();
                var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
                var mode=RenderSettings.ambientMode;var fog=RenderSettings.fog;var reflection=RenderSettings.customReflectionTexture;
                var reflectionMode=RenderSettings.defaultReflectionMode;float intensity=RenderSettings.reflectionIntensity;
                using(var photo=new PhotoComposition(1920,1080,id))
                {
                    var hero=photo.Hero.Root;var position=hero.localPosition;var scale=hero.localScale;var rotation=hero.localRotation;
                    var actual=hero.GetComponentsInChildren<MeshRenderer>();
                    if(actual.Length==0||hero.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.enabled)||!hero.name.StartsWith(id+" "))
                        throw new Exception("Photo does not use selected frozen 3D hero: "+id);
                    var expected=battleHero.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                    foreach(var mat in actual.SelectMany(r=>r.sharedMaterials).Distinct())
                    {
                        var source=expected.Single(m=>m.name==mat.name);
                        if(mat.shader!=source.shader||mat.mainTexture!=source.mainTexture||mat.GetColor("_Color")!=source.GetColor("_Color"))
                            throw new Exception("Photo changed battle material: "+id+"/"+mat.name);
                        if(mat.HasProperty("_CostumeFinish")&&(mat.GetFloat("_CostumeFinish")!=source.GetFloat("_CostumeFinish")||
                            mat.GetTexture("_CostumeOcclusion")!=source.GetTexture("_CostumeOcclusion")))
                            throw new Exception("Photo lost costume finish: "+id);
                        if(mat.HasProperty("_SurfaceCavity")&&(mat.GetTexture("_SurfaceCavity")!=source.GetTexture("_SurfaceCavity")||
                            mat.GetFloat("_SurfaceCavityStrength")!=source.GetFloat("_SurfaceCavityStrength")))
                            throw new Exception("Photo lost surface cavity: "+id);
                    }
                    foreach(bool portrait in new[]{false,true})
                    {
                        var person=Person(portrait,out var pose);photo.ResetFraming();
                        try
                        {
                            for(int step=0;step<4;step++)if(!photo.SetPerson(person,pose))throw new Exception("Person framing failed: "+id);
                            var image=photo.Snapshot();File.WriteAllBytes(Folder+"/"+id+(portrait?"-half.png":"-full.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                            var matte=new Texture2D(2,2);matte.LoadImage(photo.PersonMatte());var pixels=matte.GetPixels32();
                            for(int y=0;y<matte.height;y++)for(int x=0;x<matte.width/2;x++)
                                if(pixels[y*matte.width+x].r>0)throw new Exception("Hero leaked into person-only mask: "+id);
                            UnityEngine.Object.DestroyImmediate(matte);
                            var clean=photo.CleanPlate();if(portrait)File.WriteAllBytes(Folder+"/"+id+"-plate.png",clean);
                            // Retake must preserve the complete fixed 3D landmark.
                            photo.HidePerson();photo.Render();photo.ResetFraming();photo.SetPerson(person,pose);photo.Render();
                            if(hero.localPosition!=position||hero.localScale!=scale||hero.localRotation!=rotation)
                                throw new Exception("Person update or retake changed hero placement: "+id);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(person);}
                    }
                    var bounds=photo.Hero.Body;
                    float bottom=position.y+bounds.Bottom*photo.Hero.Scale,top=position.y+bounds.Top*photo.Hero.Scale;
                    if(Math.Abs(bottom+3.9f)>.001f||top>3.551f)throw new Exception("Cropped photo hero: "+id);
                    report.AppendLine($"{id}: selected battle materials match; meshSurfaces={actual.Length}; fixed bottom={bottom:F3} top={top:F3}; half/full person, retake and person-only matte=passed");
                }
                for(int n=0;n<lights.Length;n++)if(lights[n].cullingMask!=masks[n]||lights[n].enabled!=enabled[n])
                    throw new Exception("Photo changed arena lamp: "+lights[n].name);
                if(RenderSettings.ambientSkyColor!=sky||RenderSettings.ambientEquatorColor!=equator||RenderSettings.ambientGroundColor!=ground||
                    RenderSettings.ambientMode!=mode||RenderSettings.fog!=fog||RenderSettings.customReflectionTexture!=reflection||
                    RenderSettings.defaultReflectionMode!=reflectionMode||RenderSettings.reflectionIntensity!=intensity)
                    throw new Exception("Photo changed arena environment: "+id);
                report.AppendLine(id+": arena light masks, enable states, ambient, fog and reflection restored");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            PhotoColorChecks.Run();
            string proportions=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/photo-proportions"));
            foreach(string name in new[]{"half-body.png","full-body.png"})File.Copy(Path.Combine(proportions,name),Folder+"/"+name,true);
            File.WriteAllText(Folder+"/validation.txt",report.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Runtime/PhotoComposition.cs","Scripts/Runtime/PhotoHero.cs","Scripts/Runtime/PhotoLighting.cs","Scripts/Runtime/RuntimeResources.cs","Scripts/Core/PhotoLayout.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/PhotoGroundShadow.cs","Scripts/Runtime/FootPlantCalibration.cs","Resources/PhotoGroundMask.shader","Resources/PhotoGroundShadow.shader","Resources/HeroSurface.shader","Editor/CharacterAssetImport.cs","Editor/PhotoHeroReview.cs","Editor/PhotoReview.cs","Editor/PhotoCompositionChecks.cs","Editor/PhotoGroundingReview.cs","Editor/PhotoFramingReview.cs"})
                    if(File.Exists(Path.Combine(Application.dataPath,path)))sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(Folder+"/sources.txt",sources.ToString());Debug.Log("[PhotoHeroReview] PASS\n"+report);
        }
        static Texture2D Person(bool portrait,out PoseFrame pose)
        {
            int radius=portrait?70:30,headY=portrait?335:390,shoulder=portrait?225:340,halfWidth=portrait?125:60;
            var texture=new Texture2D(640,480,TextureFormat.RGBA32,false);var pixels=new Color32[640*480];
            for(int y=0;y<480;y++)for(int x=0;x<640;x++)
            {
                bool head=(x-320)*(x-320)+(y-headY)*(y-headY)<radius*radius;
                bool body=Math.Abs(x-320)<halfWidth&&y<shoulder+10&&y>(portrait?0:20);
                if(head||body)pixels[y*640+x]=head?new Color32(230,184,132,255):new Color32(35,165,230,255);
            }
            texture.SetPixels32(pixels);texture.Apply();
            var points=new PosePoint[33];for(int i=0;i<33;i++)points[i]=new PosePoint(.5f,.5f);
            points[0]=new PosePoint(.5f,1-(headY-10)/480f);
            points[11]=new PosePoint((320+halfWidth)/640f,1-shoulder/480f);
            points[12]=new PosePoint((320-halfWidth)/640f,1-shoulder/480f);
            if(!portrait)
            {points[25]=new PosePoint(.58f,.7f);points[26]=new PosePoint(.42f,.7f);points[27]=new PosePoint(.58f,.92f);points[28]=new PosePoint(.42f,.92f);}
            pose=new PoseFrame{schema=1,source="synthetic",streamId="photo-3d",tracked=true,sequence=1,capturedMs=10000,points=points};
            return texture;
        }
    }
}
