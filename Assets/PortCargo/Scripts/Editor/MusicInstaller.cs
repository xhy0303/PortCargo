using System.IO;
using PortCargo.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PortCargo.Editor
{
    public static class MusicInstaller
    {
        [MenuItem("PortCargo/Apply Music v0.4.1")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
            var source=GameObject.Find("AudioRoot/Music").GetComponent<AudioSource>();
            var player=source.GetComponent<HarborMusicPlayer>();
            if(!player)player=source.gameObject.AddComponent<HarborMusicPlayer>();
            var names=new[]{"night_harbor","lantern_current","morning_tide"};
            var serialized=new SerializedObject(player);var tracks=serialized.FindProperty("tracks");tracks.arraySize=names.Length;
            var audit=new System.Collections.Generic.List<string>();
            for(var i=0;i<names.Length;i++)
            {
                var path="Assets/PortCargo/Audio/MusicV041/"+names[i]+".wav";
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono=false;importer.loadInBackground=true;
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;
                settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);tracks.GetArrayElementAtIndex(i).objectReferenceValue=clip;
                audit.Add(path+" "+clip.length+"s channels="+clip.channels+" load="+clip.loadType+" format="+importer.defaultSampleSettings.compressionFormat+" quality="+importer.defaultSampleSettings.quality);
            }
            serialized.FindProperty("volume").floatValue=.65f;serialized.ApplyModifiedPropertiesWithoutUndo();
            source.clip=null;source.loop=false;source.volume=.65f;source.playOnAwake=false;source.spatialBlend=0;
            EditorUtility.SetDirty(source);EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            EditorSceneManager.SaveScene(source.gameObject.scene);
            PlayerSettings.bundleVersion="0.4.1";
            AssetDatabase.SaveAssets();Directory.CreateDirectory("Docs/Evidence/v0.4.1");
            File.WriteAllLines("Docs/Evidence/v0.4.1/audio-imports.txt",audit);
        }
    }
}
