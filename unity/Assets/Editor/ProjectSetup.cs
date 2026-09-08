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
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
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
            EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene("Assets/Scenes/Arena.unity",true) };
        }
        [MenuItem("UltramanGame/Open Arena")]
        public static void OpenArena() => EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
        [MenuItem("UltramanGame/Build macOS Prototype")]
        public static void BuildMac()
        {
            Configure();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var name in new[] { "music_ready","music_battle","swing","impact","beam","shield","transform","recover","victory" })
                RequireAudio("Assets/Resources/Audio/"+name+".wav");
            foreach(var name in new[] { "welcome","transform","battle","warning","block","recover","energy","beam","victory","resume","tutorial","beam_help" })
                RequireAudio("Assets/Resources/Voice/"+name+".aiff");
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
