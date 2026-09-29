using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    public sealed class CharacterAssetImport : AssetPostprocessor
    {
        public override uint GetVersion()=>2;
        void OnPreprocessModel()
        {
            if(!CharacterPath())return;
            var importer=(ModelImporter)assetImporter;
            importer.animationType=ModelImporterAnimationType.Legacy;
            importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
            importer.addCollider=false;
            // Beam contacts read monster vertices. Photo framing also reads
            // hero head weights and rigid helmet vertices once on photo entry.
            // Editor mesh access alone would hide a stripped-player failure.
            importer.isReadable=true;
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
            // Occlusion is a numeric visibility factor, never display color.
            if(assetPath.EndsWith("/TigaBodyOcclusion.png",System.StringComparison.Ordinal))
            {importer.sRGBTexture=false;importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;}
        }
        bool CharacterPath() => assetPath.StartsWith("Assets/Resources/Characters/") ||
            assetPath.StartsWith("Assets/Editor/CombatSample/Characters/");
    }
}
