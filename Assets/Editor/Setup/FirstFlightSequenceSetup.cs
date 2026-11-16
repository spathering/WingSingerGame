using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class FirstFlightSequenceSetup {
        const string Request="Temp/WingSingerB.request",Response="Temp/WingSingerB.response.txt";
        static FirstFlightSequenceSetup(){EditorApplication.update+=Poll;}
        static void Poll(){
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            string action=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{
                if(action=="state"){
                    var manager=UnityEngine.Object.FindObjectOfType<BreathLevelManager>();
                    File.WriteAllText(Response,"playing="+EditorApplication.isPlaying+", scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+", dirty="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty
                        +", stage="+(manager!=null&&manager.Sequence!=null?manager.Sequence.Stage.ToString():"none")
                        +", playTime="+(manager!=null&&manager.Session!=null?manager.Session.PlayTime:0));
                    if(manager!=null){
                        File.AppendAllText(Response,"\npaused="+manager.IsPaused+", editorPaused="+EditorApplication.isPaused+", timescale="+Time.timeScale+", frame="+Time.frameCount);
                        foreach(var video in manager.GetComponentsInChildren<VideoPlayer>(true))File.AppendAllText(Response,"\n"+video.name+": playing="+video.isPlaying+", paused="+video.isPaused+", prepared="+video.isPrepared+", time="+video.time+", frame="+video.frame+"/"+video.frameCount);
                        if(manager.Sequence!=null)foreach(var video in manager.Sequence.GetComponentsInChildren<VideoPlayer>(true))File.AppendAllText(Response,"\n"+video.name+": playing="+video.isPlaying+", paused="+video.isPaused+", prepared="+video.isPrepared+", time="+video.time+", frame="+video.frame+"/"+video.frameCount);
                    }
                }
                else if(action=="repair-import"){
                    foreach(string path in new[]{"Assets/Prefabs/Levels/Level_001_FirstFlight.prefab","Assets/Prefabs/Levels/Level_002_SlowBreath.prefab"})
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    File.WriteAllText(Response,"TARGETED IMPORT SUCCESS");
                }
                else if(action=="setup"){Apply();File.WriteAllText(Response,"SETUP SUCCESS");}
                else if(action=="cloud-setup"){ApplyClouds();File.WriteAllText(Response,"CLOUD SETUP SUCCESS");}
                else if(action=="validate"){FirstFlightSequenceValidation.Begin();File.WriteAllText(Response,"VALIDATION STARTED");}
                else if(action=="validate-full"){FirstFlightSequenceValidation.Begin(true);File.WriteAllText(Response,"FULL PLAYTHROUGH STARTED");}
                else if(action=="validate-guide"){FirstFlightSequenceValidation.Begin(false,true);File.WriteAllText(Response,"GUIDE VALIDATION STARTED");}
                else if(action=="validate-playback-state"){FirstFlightSequenceValidation.Begin(false,false,true);File.WriteAllText(Response,"PLAYBACK STATE REGRESSION STARTED");}
                else throw new ArgumentException("Unknown B request "+action);
            }catch(Exception e){File.WriteAllText(Response,e.ToString());Debug.LogException(e);}
        }
        static void ApplyClouds(){
            WingSinger.Editor.UIIntegrationSetup.RequireSavedScenes();
            const string folder="Assets/Art/LevelSequences/CloudTransition";
            var paths=System.IO.Directory.GetFiles(folder,"Cloud_*.png");System.Array.Sort(paths,System.StringComparer.Ordinal);
            if(paths.Length!=152)throw new InvalidOperationException("Expected all 152 cloud frames.");
            var frames=new Texture2D[paths.Length];
            for(int i=0;i<paths.Length;i++){
                string path=paths[i].Replace('\\','/');
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null){AssetDatabase.ImportAsset(path);importer=(TextureImporter)AssetImporter.GetAtPath(path);}
                var standalone=importer.GetPlatformTextureSettings("Standalone");
                if(importer.mipmapEnabled||importer.isReadable||!standalone.overridden||standalone.format!=TextureImporterFormat.BC7||standalone.maxTextureSize!=2048){
                    importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.isReadable=false;
                    importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
                    standalone.name="Standalone";standalone.overridden=true;standalone.maxTextureSize=2048;standalone.format=TextureImporterFormat.BC7;standalone.compressionQuality=50;
                    importer.SetPlatformTextureSettings(standalone);importer.SaveAndReimport();
                }
                frames[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            const string prefab="Assets/Prefabs/Levels/Level_001_FirstFlight.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try{
                var cloud=root.GetComponent<CloudTransitionOverlay>();if(cloud==null)cloud=root.AddComponent<CloudTransitionOverlay>();
                cloud.frames=frames;cloud.framesPerSecond=30;cloud.switchFrame=96;
                root.GetComponent<FirstFlightSequence>().cloudTransition=cloud;PrefabUtility.SaveAsPrefabAsset(root,prefab);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/BreathDemo.unity");
            Debug.Log("WingSinger: imported 152 alpha cloud frames and connected both intermission transitions on Level 1 only.");
        }
        [MenuItem("WingSinger/B Group/Apply First Flight Sequence")]
        public static void Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before applying B setup.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/BreathDemo.unity"||scene.isDirty)throw new InvalidOperationException("Open saved BreathDemo before B setup; preserve unsaved user changes.");
            const string art="Assets/Art/LevelSequences/";
            foreach(var name in new[]{"FirstFlightOpening.mp4","FirstFlightIntermission.mp4","OpeningVideo.shader"})AssetDatabase.ImportAsset(art+name);
            var opening=AssetDatabase.LoadAssetAtPath<VideoClip>(art+"FirstFlightOpening.mp4");
            var rest=AssetDatabase.LoadAssetAtPath<VideoClip>(art+"FirstFlightIntermission.mp4");
            if(opening==null||rest==null)throw new InvalidOperationException("Both video clips must import successfully.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(art+"OpeningVideo.mat");
            if(material==null){material=new Material(Shader.Find("WingSinger/Opening Video"));AssetDatabase.CreateAsset(material,art+"OpeningVideo.mat");}
            for(int index=0;index<2;index++){
                string path=index==0?"Assets/Prefabs/Levels/Level_001_FirstFlight.prefab":"Assets/Prefabs/Levels/Level_002_SlowBreath.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try{
                    var level=root.GetComponent<BreathLevel>();
                    if(root.GetComponent<GuideOffscreenArrow>()==null)root.AddComponent<GuideOffscreenArrow>();
                    if(index==0){
                        var sequence=root.GetComponent<FirstFlightSequence>()??root.AddComponent<FirstFlightSequence>();
                        sequence.opening=opening;sequence.intermission=rest;sequence.openingMaterial=material;level.sequence=sequence;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var settings=AssetDatabase.LoadAssetAtPath<BreathLevelSettings>("Assets/Settings/Levels/Level_001_FirstFlight.asset");
            settings.inhaleSeconds=2;settings.exhaleSeconds=3;settings.durationSeconds=100;
            settings.loopCount=2;settings.loopSeconds=50;settings.middleTailSeconds=2;
            settings.preparationSeconds=3;settings.playerEntranceSeconds=0;settings.Validate();EditorUtility.SetDirty(settings);
            var definition=AssetDatabase.LoadAssetAtPath<BreathLevelDefinition>("Assets/Settings/Levels/Level_001_FirstFlight_Definition.asset");
            definition.description="Two 50-second flights: inhale 2 s, exhale 3 s, with a film intermission. Relax on the middle lane at both ends.";EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            // Scene registrations and A's UI prefab are retained; only B-owned prefab assets changed.
        }
    }
}
