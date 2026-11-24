using System;
using UnityEditor;
using UnityEngine;

// 音频单独导入，不改变原有Game素材清单和图像导入规则。
public sealed class AudioAssetImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/", StringComparison.Ordinal)) return;
        Apply((AudioImporter)assetImporter, assetPath.Contains("/Music/"));
    }

    private static void Apply(AudioImporter importer, bool music)
    {
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = .7f;
        settings.preloadAudioData = !music;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = !music;
        importer.loadInBackground = music;
    }

    public static void VerifyAndReimport()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;
            bool music = path.Contains("/Music/");
            AudioImporterSampleSettings current = importer.defaultSampleSettings;
            if (current.loadType == (music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad) &&
                current.compressionFormat == (music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM) && importer.forceToMono == !music &&
                importer.loadInBackground == music && current.preloadAudioData == !music &&
                Math.Abs(current.quality - .7f) < .001f) continue;
            Apply(importer, music); importer.SaveAndReimport();
        }
    }
}
