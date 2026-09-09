using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Unity does not destroy materials created by code when their renderers leave a scene.
    public sealed class RuntimeResources : MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();
        public static T Own<T>(Transform root,T value) where T:Object
        {
            var owner=root.GetComponent<RuntimeResources>();if(!owner)owner=root.gameObject.AddComponent<RuntimeResources>();
            owner.owned.Add(value);return value;
        }
        void OnDestroy()
        {foreach(var value in owned)if(value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}owned.Clear();}
    }
}
