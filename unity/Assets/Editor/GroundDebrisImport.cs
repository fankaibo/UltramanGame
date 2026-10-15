using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    public sealed class GroundDebrisImport : AssetPostprocessor
    {
        public override uint GetVersion()=>2;
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/Resources/Environment/GroundDebris/"))return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            // Only these two <=320-triangle meshes need CPU vertices for the
            // exact rotated bottom support used by GroundImpact's bounce path.
            importer.isReadable=true;
        }
        void OnPostprocessModel(GameObject root)
        {
            if(!assetPath.StartsWith("Assets/Resources/Environment/GroundDebris/"))return;
            // FBX metre/centimetre conversion may live on the prefab parent.
            // GroundImpact intentionally loads shared meshes without that
            // hierarchy, so bake a known radius into the mesh itself.
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(!mesh)continue;
                var vertices=mesh.vertices;var centre=mesh.bounds.center;float radius=0;
                foreach(var vertex in vertices)radius=Mathf.Max(radius,(vertex-centre).magnitude);
                if(radius<.000001f)throw new System.InvalidOperationException("Empty scanned ground fragment");
                for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-centre)*(.90f/radius);
                mesh.vertices=vertices;mesh.RecalculateBounds();
            }
        }
    }
}
