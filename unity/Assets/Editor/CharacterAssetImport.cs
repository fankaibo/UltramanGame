using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    public sealed class CharacterAssetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if(!CharacterPath())return;
            var importer=(ModelImporter)assetImporter;
            importer.animationType=ModelImporterAnimationType.Legacy;
            importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
            importer.addCollider=false;importer.isReadable=false;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.importBlendShapes=false;
        }
        void OnPreprocessTexture()
        {
            if(!CharacterPath())return;
            var importer=(TextureImporter)assetImporter;
            importer.maxTextureSize=4096;importer.mipmapEnabled=true;
            importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
        }
        bool CharacterPath() => assetPath.StartsWith("Assets/Resources/Characters/") ||
            assetPath.StartsWith("Assets/Editor/CombatSample/Characters/");
    }
}
