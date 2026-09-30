using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    public sealed class ScannedRockImport : AssetPostprocessor
    {
        bool Applies=>assetPath.StartsWith("Assets/Resources/Environment/ScannedRocks/");
        void OnPreprocessModel()
        {
            if(!Applies)return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.meshCompression=ModelImporterMeshCompression.Off;
        }
        void OnPreprocessTexture()
        {
            if(!Applies)return;
            var importer=(TextureImporter)assetImporter;
            bool normal=assetPath.Contains("_nor_gl_");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal&&!assetPath.Contains("_arm_");
            importer.mipmapEnabled=true;importer.maxTextureSize=2048;
            importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=4;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
        }
    }
}
