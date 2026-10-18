#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UltramanGame.Editor
{
    // The supplied song is deliberately ignored by Git, so its generated .meta
    // file cannot be the only place where the safe import settings live. Reapply
    // the settings whenever the user drops the same file into Resources/Audio.
    public sealed class UserMusicImportSettings : AssetPostprocessor
    {
        const string AssetName="Assets/Resources/Audio/miracle_reappearance.mp3";

        void OnPreprocessAudio()
        {
            if(!assetPath.Equals(AssetName,System.StringComparison.OrdinalIgnoreCase))return;
            var importer=(AudioImporter)assetImporter;
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=1;
            settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride=44100;
            importer.defaultSampleSettings=settings;
            importer.forceToMono=false;
            importer.loadInBackground=true;
            importer.ambisonic=false;
        }
    }
}
#endif
