using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WingSinger.UI;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class ReferenceUIPlayValidation {
        const string Key="WingSinger.UI.PlayValidation";
        const string Report="Docs/Validation/UIPlayValidation.json";
        [Serializable] class Result { public string date=DateTimeOffset.Now.ToString("O"); public bool success; public string[] checks; public string error; }
        static ReferenceUIPlayValidation() { EditorApplication.update+=Tick; }
        [MenuItem("WingSinger/UI/Validate UI Play Mode")]
        public static void Begin() {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save open scenes first.");
            EditorSceneManager.OpenScene("Assets/Scenes/UI/UIFlowPreview.unity");
            SessionState.SetBool(Key,true); SessionState.SetInt(Key+"Stage",0); SessionState.SetInt(Key+"Frames",0);
            SessionState.SetFloat(Key+"Started",(float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying=true;
        }
        static void Tick() {
            if(!SessionState.GetBool(Key,false)) return;
            if(SessionState.GetInt(Key+"Stage",0)==1 && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetBool(Key,false); if(Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key+"Exit",0)); return;
            }
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Started",0)>120) { Finish(new Result{success=false,error="UI Play validation timeout"}); return; }
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key+"Stage",0)!=0) return;
            int frame=SessionState.GetInt(Key+"Frames",0)+1; SessionState.SetInt(Key+"Frames",frame); if(frame<4) return;
            var checks=new List<string>();
            try {
                void Check(bool ok,string name) { if(!ok) throw new Exception(name); checks.Add(name); }
                var router=UnityEngine.Object.FindObjectOfType<UIScreenRouter>();
                Check(router!=null&&router.pages.Length==14,"Actual Play Mode has all 14 pages");
                UIPageView Current()=>router.pages.Single(p=>p.screenId==router.CurrentScreen);
                void Click(string name) { var button=Current().GetComponentsInChildren<Button>().First(b=>b.name==name); Check(button.interactable,"Interactable button: "+name); button.onClick.Invoke(); }
                Check(router.CurrentScreen==UIScreenId.HomeDashboard,"Awake opens HomeDashboard");
                Click("Settings"); Check(router.CurrentScreen==UIScreenId.Settings,"Runtime button opens Settings");
                var settings=Current(); var slider=settings.GetComponentsInChildren<Slider>().Single(s=>s.name=="Music"); slider.value=.35f;
                Check(slider.GetComponent<UISliderValue>().valueLabel.text=="35%","Runtime Slider event updates percentage");
                var toggle=settings.GetComponentsInChildren<Toggle>().Single(t=>t.name=="ReduceMotion"); toggle.isOn=true;
                Check(toggle.GetComponent<UIToggleValue>().valueLabel.text=="On"&&toggle.GetComponent<UIToggleValue>().handle.anchoredPosition.x>0,"Runtime Toggle updates label and handle");
                Click("Controller"); Check(router.CurrentScreen==UIScreenId.ControllerConnection,"Runtime Controller route opens connection page");
                int checksRequested=0; router.controllerCheckRequested.AddListener(()=>checksRequested++); Click("CheckConnection");
                Check(checksRequested==1,"Check Connection raises one integration event");
                Click("Done"); Check(router.CurrentScreen==UIScreenId.Settings,"Done restores the Settings caller");
                Click("Back"); Check(router.CurrentScreen==UIScreenId.HomeDashboard,"Back restores Home");
                Click("ViewLevels"); Click("LevelCard");
                Check(router.CurrentScreen==UIScreenId.ReadyForTakeoff&&router.SelectedLevel==1,"Runtime level card selects a level");
                Check(Current().selectedLevelLabels.All(l=>l.text=="Level 1"),"Ready titles reflect actual selected level");
                Click("TryRhythm"); Check(router.CurrentScreen==UIScreenId.PracticeInhale,"Try Rhythm starts inhale preview");
                Click("Instruction"); Check(router.CurrentScreen==UIScreenId.PracticeExhale,"Instruction button advances tutorial");
                Click("Pause"); Check(router.CurrentScreen==UIScreenId.FlightPaused,"HUD Pause opens pause page");
                Click("Settings"); Click("Back"); Click("Resume");
                Check(router.CurrentScreen==UIScreenId.PracticeExhale,"Nested settings and resume retain tutorial stage");
                Click("Instruction"); Click("Instruction"); Check(router.CurrentScreen==UIScreenId.PracticeComplete,"Tutorial completes through real button listeners");
                Click("ChooseLevel"); Check(router.CurrentScreen==UIScreenId.LevelSelect,"Choose Level returns to level selection");
                router.Show(UIScreenId.Settings); router.Show(UIScreenId.HomeDashboard); Click("Settings"); Click("Back");
                Check(router.CurrentScreen==UIScreenId.HomeDashboard,"Re-enabled buttons do not duplicate route listeners");
                foreach(UIScreenId id in Enum.GetValues(typeof(UIScreenId))) { router.Show(id); Check(router.pages.Count(p=>p.gameObject.activeSelf)==1,"One active page in Play: "+id); }
                Check(SceneManager.GetActiveScene().path=="Assets/Scenes/UI/UIFlowPreview.unity","UI navigation does not load another Unity scene");
                Finish(new Result{success=true,checks=checks.ToArray()});
            } catch(Exception e) { Finish(new Result{success=false,checks=checks.ToArray(),error=e.ToString()}); }
        }
        static void Finish(Result result) {
            Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report,JsonUtility.ToJson(result,true));
            if(result.success) Debug.Log("WingSinger UI Play validation passed: "+result.checks.Length+" checks."); else Debug.LogError(result.error);
            SessionState.SetInt(Key+"Exit",result.success?0:1); SessionState.SetInt(Key+"Stage",1); EditorApplication.isPlaying=false;
        }
    }
}
