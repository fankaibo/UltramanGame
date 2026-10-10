using UnityEngine;
using UnityEngine.Profiling;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Development-only breadcrumbs for the memory backlog. The player never
    // changes its allocation policy because of these samples; they make it
    // possible to separate Unity native/managed memory from other processes
    // shown by macOS Activity Monitor.
    public sealed class RuntimeMemoryDiagnostics
    {
        const float Interval=15f;
        float nextSample;
        GamePhase lastPhase=(GamePhase)(-1);
        bool lastPhoto;

        public void Tick(GamePhase phase,bool photoActive,float now)
        {
            if(!Debug.isDebugBuild)return;
            bool transition=phase!=lastPhase||photoActive!=lastPhoto;
            if(!transition&&now<nextSample)return;
            lastPhase=phase;lastPhoto=photoActive;nextSample=now+Interval;
            long allocated=Profiler.GetTotalAllocatedMemoryLong();
            long reserved=Profiler.GetTotalReservedMemoryLong();
            long mono=Profiler.GetMonoUsedSizeLong();
            long monoHeap=Profiler.GetMonoHeapSizeLong();
            Debug.Log($"[RuntimeMemory] phase={phase} photo={photoActive} allocatedMB={allocated/1048576f:F1} reservedMB={reserved/1048576f:F1} monoMB={mono/1048576f:F1} monoHeapMB={monoHeap/1048576f:F1}");
        }
    }
}
