using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    public sealed class ZeroSurfaceImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Characters/Zero/Surface/"))return;
            var importer=(TextureImporter)assetImporter;
            bool normal=assetPath.EndsWith("Normal.png");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=false;importer.flipGreenChannel=false;
            importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.alphaSource=TextureImporterAlphaSource.None;
        }
    }
}
