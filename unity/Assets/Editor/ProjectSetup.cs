using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UltramanGame.Editor
{
    [InitializeOnLoad] public static class ProjectSetup
    {
        static ProjectSetup() { EditorApplication.delayCall += Configure; }
        static void Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            PlayerSettings.companyName="UltramanGame";
            PlayerSettings.productName="迪迦体感训练场";
            PlayerSettings.defaultScreenWidth=1280;
            PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.defaultIsNativeResolution=false;
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.ultramangame.training");
            // A Resources material retains Standard in player builds despite procedural actors.
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PrototypeSurface.mat"))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Resources/PrototypeSurface.mat");
            }
            var fontImporter=AssetImporter.GetAtPath("Assets/Resources/Fonts/NotoSansSC-Regular.otf") as TrueTypeFontImporter;
            if(fontImporter!=null && !fontImporter.includeFontData)
            { fontImporter.includeFontData=true; fontImporter.SaveAndReimport(); }
            foreach(var name in new[]{"TigaRear45Actions","GolzaActions","TigaPhotoActions"})
            {
                var importer=AssetImporter.GetAtPath("Assets/Resources/Art/"+name+".png") as TextureImporter;
                if(importer!=null && (importer.mipmapEnabled || importer.textureCompression!=TextureImporterCompression.Uncompressed || importer.npotScale!=TextureImporterNPOTScale.None || !importer.isReadable))
                {
                    importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.npotScale=TextureImporterNPOTScale.None;importer.isReadable=true;importer.maxTextureSize=2048;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                }
            }
            EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene("Assets/Scenes/Arena.unity",true) };
        }
        [MenuItem("UltramanGame/Open Arena")]
        public static void OpenArena() => EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
        [MenuItem("UltramanGame/Build macOS Prototype")]
        public static void BuildMac()
        {
            Configure();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var name in new[]{"TigaRear45Actions","GolzaActions","TigaPhotoActions"})
            {
                var atlas=Resources.Load<Texture2D>("Art/"+name);
                if(!atlas||atlas.width%4!=0||atlas.height%2!=0)throw new System.Exception("Missing or invalid 4 x 2 action atlas: "+name);
            }
            if(!System.IO.File.Exists("Assets/Plugins/macOS/libUltramanMusicPicker.dylib"))
                throw new System.Exception("Build the music picker first: bash scripts/build_native.sh");
            if(!System.IO.File.Exists("Assets/Plugins/macOS/libUltramanPhoto.dylib"))
                throw new System.Exception("Build person segmentation first: bash scripts/build_native.sh");
            foreach(var name in new[] { "music_ready","music_battle","swing","impact","beam","shield","transform","recover","victory","enemy_rush" })
                RequireAudio("Assets/Resources/Audio/"+name+".wav");
            foreach(var name in new[] { "welcome","transform","battle","warning","block","recover","energy","beam","victory","resume","tutorial","beam_help","beam_reset","photo_intro","photo_missing","photo_saved","photo_retry","photo_five","photo_four","photo_three","photo_two","photo_one","arcade_ready","arcade_final" })
                RequireAudio("Assets/Resources/Voice/"+name+".aiff");
            // Optional local original recording is separate from generated guide lines.
            if(System.IO.File.Exists("Assets/Resources/Voice/beam_original.aiff"))
            {
                RequireAudio("Assets/Resources/Voice/beam_original.aiff");
                if(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Voice/beam_original.aiff").length>3)
                    throw new System.Exception("Beam battle cry must be a short recording of at most 3 seconds.");
            }
            foreach(var actor in new[]{"Tiga","Golza"})
                if(!Resources.Load<GameObject>("Characters/"+actor+"/"+actor)||!Resources.Load<TextAsset>("Characters/"+actor+"/ATTRIBUTION"))
                    throw new System.Exception(actor+" model and author attribution are required for this build.");
            foreach(var shader in new[]{"ContactShadow","ShadowSilhouette","CityGround","CitySky","KaijuSurface","EnergyShield","EnergyFlare","CinematicComposite","TransformationVeil","SoftGlow"})
                if(!Resources.Load<Shader>(shader))throw new System.Exception("Missing actor shadow shader: "+shader);
            PhotoCompositionChecks.Run();
            PhotoReview.Proportions();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[] { "Assets/Scenes/Arena.unity" },
                locationPathName="Builds/TigaTraining.app",target=BuildTarget.StandaloneOSX,
                options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("macOS build failed");
        }
        static void RequireAudio(string path)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(!clip || clip.length<.05f)throw new System.Exception("Missing or empty audio: "+path+". Run scripts/generate_audio.py and scripts/generate_voice.py before building.");
        }
        // Batch smoke: scene construction runs when entering Play mode. Editor compilation is the first gate.
        public static void ValidateProject() { Configure(); Debug.Log("UltramanGame scripts compiled and project configured."); }
    }
}
