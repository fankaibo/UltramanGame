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
            PlayerSettings.runInBackground=true;
            // A Resources material retains Standard in player builds despite procedural actors.
            if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/PrototypeSurface.mat"))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Resources/PrototypeSurface.mat");
            }
            EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene("Assets/Scenes/Arena.unity",true) };
        }
        [MenuItem("UltramanGame/Open Arena")]
        public static void OpenArena() => EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
        [MenuItem("UltramanGame/Build macOS Prototype")]
        public static void BuildMac()
        {
            Configure();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[] { "Assets/Scenes/Arena.unity" },
                locationPathName="Builds/UltramanGame.app",target=BuildTarget.StandaloneOSX,
                options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("macOS build failed");
        }
        // Batch smoke: scene construction runs when entering Play mode. Editor compilation is the first gate.
        public static void ValidateProject() { Configure(); Debug.Log("UltramanGame scripts compiled and project configured."); }
    }
}
