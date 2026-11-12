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
                }
                else if(action=="repair-import"){
                    foreach(string path in new[]{"Assets/Prefabs/Levels/Level_001_FirstFlight.prefab","Assets/Prefabs/Levels/Level_002_SlowBreath.prefab"})
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    File.WriteAllText(Response,"TARGETED IMPORT SUCCESS");
                }
                else if(action=="setup"){Apply();File.WriteAllText(Response,"SETUP SUCCESS");}
                else if(action=="validate"){FirstFlightSequenceValidation.Begin();File.WriteAllText(Response,"VALIDATION STARTED");}
                else if(action=="validate-full"){FirstFlightSequenceValidation.Begin(true);File.WriteAllText(Response,"FULL PLAYTHROUGH STARTED");}
                else throw new ArgumentException("Unknown B request "+action);
            }catch(Exception e){File.WriteAllText(Response,e.ToString());Debug.LogException(e);}
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
