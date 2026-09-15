using UnityEditor;
namespace UltramanGame.Editor
{
    public class RosterAssetImporter : AssetPostprocessor
    {
        bool Roster=>assetPath.Contains("/Characters/Mebius/")||assetPath.Contains("/Characters/Zero/")||assetPath.Contains("/Characters/Geed/")||assetPath.Contains("/Characters/Grigio/");
        void OnPreprocessModel()
        {
            if(!Roster)return;var model=(ModelImporter)assetImporter;
            model.animationType=ModelImporterAnimationType.Legacy;model.animationCompression=ModelImporterAnimationCompression.Off;
            model.importAnimation=true;model.optimizeGameObjects=false;model.importCameras=false;model.importLights=false;
        }
        void OnPreprocessTexture()
        {
            if(!Roster)return;var texture=(TextureImporter)assetImporter;
            texture.textureCompression=TextureImporterCompression.Uncompressed;texture.maxTextureSize=2048;
            if(assetPath.EndsWith("/Photo.png")){texture.isReadable=true;texture.alphaIsTransparency=true;texture.mipmapEnabled=false;}
        }
    }
}
