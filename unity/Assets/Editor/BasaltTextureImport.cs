using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    // Material data stays linear; color alone is sRGB. Repeat/mipmaps are needed
    // by the world-space projection, including the steep sides of rock meshes.
    public sealed class BasaltTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Art/Basalt/")||!assetPath.EndsWith(".jpg"))return;
            var importer=(TextureImporter)assetImporter;
            bool normal=assetPath.Contains("_nor_gl_");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal&&!assetPath.Contains("_arm_");
            importer.maxTextureSize=2048;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
        }
    }
}
