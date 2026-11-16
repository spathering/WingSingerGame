using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WingSinger.UI;
using Object=UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class FigmaUIValidation {
        const string Key="WingSinger.Figma.Validation";
        const string Folder="Docs/Validation/Figma20261116";
        static IEnumerator routine;
        static readonly List<string> checks=new List<string>();
        static BreathUIFlowController flow;
        static Camera camera;
        static RenderTexture target;
        static float previousScale;
        [Serializable] sealed class Report { public string date;public bool success;public string error;public string[] checks;public string hardware="Keyboard, BLE/USB protocol mapping and simulated input verified; no physical device connection claimed."; }
        static FigmaUIValidation(){EditorApplication.update+=Tick;}
        public static void Begin(){
            UIIntegrationSetup.RequireSavedScenes();EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
            var f=Object.FindObjectOfType<BreathUIFlowController>();f.profileKey="";f.sensorInput.enabled=false;f.sensorInput.loadSavedConfiguration=false;
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Stopping",false);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key+"Background",Application.runInBackground);EditorApplication.isPlaying=true;
        }
        static void Tick(){
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Stopping",false)){
                if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);}return;
            }
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>180){Finish(false,"Figma validation timeout");return;}
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try{if(routine==null){checks.Clear();routine=Run();}if(!routine.MoveNext())Finish(true,null);}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
        static GameObject Node(UIPageView page,string id)=>page.GetComponentsInChildren<FigmaNodeTag>(true).Single(t=>t.nodeId==id).gameObject;
        static void Route(UIRouteAction action,UIScreenId page=UIScreenId.HomeDashboard,int level=0){flow.router.Route(action,page,level);Check(flow.router.pages.Count(p=>p.gameObject.activeSelf)==1,"Exactly one visible page after "+action+" "+page);}
        static void Click(UIPageView page,string id){var button=Node(page,id).GetComponent<Button>();Check(button!=null&&button.interactable,"Native button enabled: "+id);button.onClick.Invoke();}
        static IEnumerator Run(){
            Application.runInBackground=true;previousScale=Time.timeScale;
            for(int i=0;i<5;i++)yield return null;
            flow=Object.FindObjectOfType<BreathUIFlowController>();var router=flow.router;var manager=flow.levelManager;
            Check(flow.Progress!=null&&flow.profileKey=="","Isolated profile, no real save or physical sensor accessed");
            Check(router.pages.Length==15&&router.CurrentScreen==UIScreenId.HomeClean,"15 unique registered pages including hardware settings and tutorial");
            Check(!flow.transform.Cast<Transform>().Any(t=>t.name=="Check"),"No orphaned calendar decorations on the shared canvas");
            Check(KeyboardBreathInput.Resolve(true,false)==BreathLane.Lower&&KeyboardBreathInput.Resolve(false,true)==BreathLane.Upper&&KeyboardBreathInput.Resolve(true,true)==BreathLane.Middle,"W inhale descends, S exhale ascends, conflicting keys neutral");
            Check(BreathSensorProtocol.Resolve(400)==BreathLane.Lower&&BreathSensorProtocol.Resolve(1000)==BreathLane.Upper&&BreathSensorProtocol.Resolve(700)==BreathLane.Middle,"USB thresholds preserve boundaries with new directions");
            Check(BreathSensorProtocol.ResolveCentered(1000,1500,100)==BreathLane.Lower&&BreathSensorProtocol.ResolveCentered(1700,1500,100)==BreathLane.Upper&&BreathSensorProtocol.ResolveCentered(1400,1500,100)==BreathLane.Middle,"BLE centered thresholds map inhale down, exhale up; equality remains neutral");
            ValidateMeterReadings();
            flow.Progress.Data.unlockedLevels=99;
            Check(flow.CanSelect(1)&&Enumerable.Range(2,11).All(i=>!flow.CanSelect(i)),"Only level 1 is available even with an old fully unlocked profile");
            var original=manager.levels.Select(l=>l.GetInstanceID()).ToArray();
            var external=new GameObject("Figma validation external input").AddComponent<ExternalBreathInput>();manager.input=external;
            Route(UIRouteAction.Navigate,UIScreenId.Settings);
            var settings=router.Page(UIScreenId.Settings);
            var tutorialEntry=settings.GetComponentsInChildren<UIRouteButton>(true).Single(r=>r.destination==UIScreenId.Tutorial);
            tutorialEntry.GetComponent<Button>().onClick.Invoke();
            Check(router.CurrentScreen==UIScreenId.Tutorial&&!flow.IsSessionActive,"Settings tutorial entry opens the tutorial without a game session");
            var tutorial=router.Page(UIScreenId.Tutorial).GetComponent<FigmaTutorialController>();
            Check(tutorial.SlideCount==9&&tutorial.CurrentSlide==0,"Tutorial starts at the first of nine slides");
            var canvas=flow.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
            camera=new GameObject("Figma UI capture camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<5;camera.orthographic=true;camera.orthographicSize=10;
            camera.transform.position=new Vector3(0,0,-10);camera.nearClipPlane=.1f;camera.farClipPlane=100;
            canvas.worldCamera=camera;canvas.planeDistance=10;canvas.sortingOrder=32000;SetResolution(1210,834);
            for(int i=0;i<9;i++){
                Check(tutorial.CurrentSlide==i&&tutorial.slides.Count(s=>s.activeSelf)==1,"Tutorial slide "+(i+1)+" shows alone");
                Check(tutorial.chooseLevel.gameObject.activeInHierarchy==(i==8),"Choose-level button only on final slide "+(i+1));
                for(int k=0;k<2;k++)yield return null;Capture("Tutorial_"+(i+1).ToString("00"));
                if(i<8)tutorial.next.onClick.Invoke();
            }
            tutorial.next.onClick.Invoke();Check(tutorial.CurrentSlide==8,"Final tutorial slide cannot advance out of bounds");
            tutorial.chooseLevel.onClick.Invoke();Check(router.CurrentScreen==UIScreenId.LevelSelect,"Final choose-level button navigates to level selection");
            var cards=flow.bindings.levelCards;Check(cards.Length==12&&cards[0].interactable&&cards.Skip(1).All(c=>!c.interactable),"Twelve native level cards, eleven locked and disabled");
            foreach(var card in cards.Skip(1)) {
                var thumb=card.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Thumbnail");
                var status=card.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Status");
                Check(Mathf.Abs(thumb.GetComponent<CanvasGroup>().alpha-.28f)<.001f&&Vector2.Distance(status.rectTransform.sizeDelta,new Vector2(48.07947f,48.07947f))<.001f,"Uniform thumbnail opacity and lock geometry: "+card.name);
            }
            cards[0].onClick.Invoke();
            Check(router.CurrentScreen==UIScreenId.LevelSelect&&router.SelectedLevel==1&&!flow.IsSessionActive,"Clicking level 1 selects it without opening preparation or starting gameplay");
            Click(router.Page(UIScreenId.LevelSelect),"330:230");
            Check(router.CurrentScreen==UIScreenId.ReadyForTakeoff&&!flow.IsSessionActive,"Only Continue opens the selected level's preparation page");
            Route(UIRouteAction.Navigate,UIScreenId.LevelSelect);
            Route(UIRouteAction.SelectLevel,level:2);Check(router.CurrentScreen==UIScreenId.LevelSelect&&router.SelectedLevel==1,"Direct navigation cannot bypass level locks");
            router.SetSelectedLevel(2);flow.Refresh();Check(!Node(router.Page(UIScreenId.LevelSelect),"330:230").GetComponent<Button>().interactable,"Continue is disabled for unavailable selections");
            Route(UIRouteAction.ContinueSelection);Check(router.CurrentScreen==UIScreenId.LevelSelect,"Unavailable selections cannot bypass Continue guard");router.SetSelectedLevel(1);flow.Refresh();
            Route(UIRouteAction.Navigate,UIScreenId.Tutorial);Check(tutorial.CurrentSlide==0,"Re-entering tutorial resets to the first slide");
            Route(UIRouteAction.Navigate,UIScreenId.Settings);
            flow.bindings.music.value=.37f;flow.bindings.sound.value=.64f;flow.bindings.reduceMotion.isOn=true;flow.bindings.highContrast.isOn=true;
            yield return null;
            Check(Mathf.Abs(flow.Progress.Data.musicVolume-.37f)<.001f&&Mathf.Abs(flow.Progress.Data.soundVolume-.64f)<.001f&&flow.Progress.Data.reduceMotion&&flow.Progress.Data.highContrast,"Settings controls update the actual progress/settings store");
            Canvas.ForceUpdateCanvases();
            Check(Mathf.Abs(flow.bindings.music.fillRect.rect.height-Node(settings,"2:448").GetComponent<RectTransform>().rect.height)<.01f,"Music fill retains the authored track height");
            Check(Mathf.Abs(flow.bindings.music.handleRect.rect.height-flow.bindings.music.handleRect.rect.width)<.01f,"Music handle remains circular instead of stretching");
            flow.bindings.reduceMotion.isOn=false;flow.bindings.highContrast.isOn=false;
            Route(UIRouteAction.Navigate,UIScreenId.ControllerConnection);int reconnects=0;flow.ReconnectOverride=()=>reconnects++;
            Click(router.Page(UIScreenId.ControllerConnection),"330:248");Check(reconnects==1,"Check connection uses the existing production reconnect path");
            Route(UIRouteAction.Navigate,UIScreenId.HardwareSettings);Check(router.CurrentScreen==UIScreenId.HardwareSettings,"Independent hardware settings page is retained");
            foreach(var size in new[]{new Vector2Int(1210,834),new Vector2Int(1280,720),new Vector2Int(1024,768)}){
                SetResolution(size.x,size.y);
                foreach(var id in new[]{UIScreenId.HomeDashboard,UIScreenId.HomeClean,UIScreenId.LevelSelect,UIScreenId.ReadyForTakeoff,UIScreenId.FlightPaused,UIScreenId.FlightComplete,UIScreenId.Settings,UIScreenId.ControllerConnection,UIScreenId.FlightLog}){
                    router.Show(id,false);flow.Refresh();yield return null;yield return null;Canvas.ForceUpdateCanvases();
                    var page=router.Page(id);Check(page.GetComponentsInChildren<TMP_Text>(true).All(t=>t.font!=null),"Fonts present: "+id+" at "+size);
                    foreach(var button in page.GetComponentsInChildren<Button>().Where(b=>b.interactable)){
                        var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
                        Check(points.All(p=>p.x>=-2&&p.y>=-2&&p.x<=size.x+2&&p.y<=size.y+2),"Button stays in viewport: "+id+"/"+button.name+" at "+size);
                    }
                    Capture(size.x+"x"+size.y+"_"+id);
                }
            }
            SetResolution(1280,720);Route(UIRouteAction.SelectLevel,level:1);Route(UIRouteAction.ContinueSelection);
            Route(UIRouteAction.TryRhythm);
            Check(flow.IsPracticing&&router.CurrentScreen==UIScreenId.PracticeInhale,"Try the rhythm retains the real input practice session");
            manager.Session.Advance(manager.Session.Pattern.Lead+manager.Session.Pattern.Preparation+1,BreathLane.Middle);
            var practiceTick=typeof(BreathUIFlowController).GetMethod("TickPractice",BindingFlags.Instance|BindingFlags.NonPublic);
            external.Submit(BreathLane.Upper);practiceTick.Invoke(flow,new object[]{4f});
            Check(router.CurrentScreen==UIScreenId.PracticeInhale,"Wrong-direction input cannot finish inhale practice");
            external.Submit(BreathLane.Lower);practiceTick.Invoke(flow,new object[]{manager.CurrentLevel.settings.exhaleSeconds+.01f});
            Check(router.CurrentScreen==UIScreenId.PracticeExhale,"Sustained inhale/down advances practice to exhale");
            external.Submit(BreathLane.Upper);practiceTick.Invoke(flow,new object[]{manager.CurrentLevel.settings.inhaleSeconds+.01f});
            Check(router.CurrentScreen==UIScreenId.PracticeFollow,"Sustained exhale/up advances practice to following");
            Route(UIRouteAction.Navigate,UIScreenId.LevelSelect);Check(flow.Progress.Data.records.Count==0,"Exiting incomplete practice creates no result record");
            Route(UIRouteAction.SelectLevel,level:1);Route(UIRouteAction.ContinueSelection);var ready=router.Page(UIScreenId.ReadyForTakeoff);
            Canvas.ForceUpdateCanvases();yield return null;camera.Render();
            var start=Node(ready,"330:234").GetComponent<Button>();var eventData=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,start.transform.TransformPoint(start.GetComponent<RectTransform>().rect.center))};
            var hits=new List<RaycastResult>();flow.GetComponent<GraphicRaycaster>().Raycast(eventData,hits);Check(hits.Any(h=>h.gameObject.GetComponentInParent<Button>()==start),"Native Start Flight has a correct pointer hit target after scaling");
            Click(ready,"330:234");for(int i=0;i<4;i++)yield return null;
            Check(flow.IsSessionActive&&manager.levels.Count(l=>l.gameObject.activeSelf)==1&&router.CurrentScreen==UIScreenId.PracticeFollow,"Start Flight activates one existing level prefab and the new live HUD");
            Check(!router.Page(UIScreenId.PracticeFollow).transform.Find("Background").gameObject.activeSelf,"Live HUD exposes the actual game world instead of Figma example artwork");
            var hud=router.Page(UIScreenId.PracticeFollow).GetComponent<FigmaGameplayHud>();
            Check(!hud.MeterReading.Available&&hud.meterStatus.text=="NO SENSOR"&&hud.meter.rectTransform.rect.height==0,"Keyboard/external input cannot activate or fill the hardware-only meter");
            Route(UIRouteAction.Pause);Check(flow.IsPaused&&Time.timeScale==0,"Pause freezes the real game");Capture("LivePaused");
            var pausedSession=manager.Session;
            Route(UIRouteAction.Navigate,UIScreenId.Settings);Route(UIRouteAction.Navigate,UIScreenId.Tutorial);
            Check(flow.IsPaused&&manager.Session==pausedSession&&Time.timeScale==0,"Opening tutorial from paused settings preserves the paused game");
            Route(UIRouteAction.Back);Route(UIRouteAction.Back);Route(UIRouteAction.Resume);Check(!flow.IsPaused&&Time.timeScale>0,"Resume restores the real game and time scale");
            Route(UIRouteAction.Restart);Check(flow.IsSessionActive&&manager.levels.Select(l=>l.GetInstanceID()).SequenceEqual(original),"Restart reuses registered level instances");
            // Skip only video presentation in this isolated test to inspect all three live camera/lane views.
            manager.Sequence.End();manager.CurrentLevel.sequence=null;
            manager.SetPaused(true);
            var pattern=manager.Session.Pattern;
            foreach(var sample in new[]{new Vector2((float)(pattern.Preparation+pattern.GuideTransition+.2),1),new Vector2((float)(pattern.Preparation+pattern.GuideTransition+pattern.Inhale+pattern.GuideTransition+.2),-1)}) {
                double offset=Math.Max(0,manager.CurrentLevel.guideHorizontalOffset)/manager.CurrentLevel.settings.moveSpeed;
                manager.Session.Advance(sample.x-offset-manager.Session.Elapsed,BreathLane.Middle);manager.CurrentLevel.Present(manager.Session);
                yield return null;yield return null;
                var expected=sample.y>0?BreathLane.Upper:BreathLane.Lower;
                Check(hud.CurrentPromptLane==expected&&Math.Abs(manager.CurrentLevel.SampledGuideTime-sample.x)<.001,"HUD prompt follows the actual leading crane clock on "+expected+" track");
                Check(Math.Abs(manager.CurrentLevel.guide.localPosition.y-manager.CurrentLevel.spawnPoint.localPosition.y-manager.CurrentLevel.settings.LaneHeight(sample.y))<.01,"Prompt agrees with the crane's rendered track height: "+expected);
            }
            Check(manager.Session.RequiredLane!=hud.CurrentPromptLane,"Lead-time regression: HUD follows crane even when the player's route is still in the previous phase");
            manager.SetPaused(false);
            canvas.worldCamera=Camera.main;Camera.main.targetTexture=target;Camera.main.aspect=1280f/720;camera.enabled=false;
            manager.Session.Advance(manager.Session.Pattern.Lead+manager.Session.Pattern.Preparation+1,BreathLane.Middle);
            foreach(var lane in new[]{BreathLane.Upper,BreathLane.Middle,BreathLane.Lower}){
                external.Submit(lane);double until=EditorApplication.timeSinceStartup+1.5;
                while(EditorApplication.timeSinceStartup<until)yield return null;
                Check(manager.Session.PlayerLane==lane,"Actual live player reaches "+lane+" lane");
                var rt=RenderTexture.active;Camera.main.Render();RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();Directory.CreateDirectory(Folder);File.WriteAllBytes(Folder+"/Live_"+lane+".png",image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=rt;
            }
            var tree=Object.FindObjectsOfType<LoopingPanorama>().Single(p=>p.name=="Looping Foreground");tree.Sync();Check(Mathf.Abs(tree.sizeMultiplier-2.7f)<.001f,"Midground trees enlarged by 35 percent");
            typeof(BreathUIFlowController).GetMethod("Finish",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
            Check(router.CurrentScreen==UIScreenId.FlightComplete&&!flow.IsSessionActive&&flow.Progress.Data.records.Count==1,"Results display the actual completed session record without leaving a level active");
            Check(!Node(router.Page(UIScreenId.FlightComplete),"330:242").GetComponent<Button>().interactable,"Next level remains disabled after finishing level 1");
            Route(UIRouteAction.Navigate,UIScreenId.FlightLog);Route(UIRouteAction.ViewRecord,level:0);Check(flow.ViewingHistory&&router.CurrentScreen==UIScreenId.FlightComplete,"Flight history opens the saved result");
            Route(UIRouteAction.Navigate,UIScreenId.HomeClean);Check(manager.levels.All(l=>!l.gameObject.activeSelf)&&Time.timeScale>0,"Returning home shuts down level presentation and restores time");
            Check(flow.Progress.Data.records.Count==1,"Reading the tutorial and result history never creates duplicate game records");
        }
        static void ValidateMeterReadings() {
            var config=new BreathSensorConfiguration{bluetoothCenterMv=1300,bluetoothDeviationMv=100,sampleTimeoutSeconds=.5f,inhaleMaximum=400,exhaleMinimum=1000};
            Func<double,double,BreathSensorSnapshot> sample=(raw,filtered)=>new BreathSensorSnapshot(BreathSensorStatus.Connected,"simulated","",true,raw,0,filteredValue:filtered);
            var resting=BreathMeterReading.Evaluate(sample(1300,1300),config,true);
            Check(resting.Available&&!resting.TargetReached&&resting.Strength==0&&resting.CenterMv==1300&&resting.TargetDeltaMv==100,"Meter baseline uses 1300 mV resting center and 100 mV target deviation");
            var halfway=BreathMeterReading.Evaluate(sample(2000,1250),config,true);
            Check(Math.Abs(halfway.Strength-.5)<.001&&!halfway.TargetReached,"Meter uses filtered hardware amplitude and shows half-target proportionally");
            Check(!BreathMeterReading.Evaluate(sample(1200,1200),config,true).TargetReached&&!BreathMeterReading.Evaluate(sample(1400,1400),config,true).TargetReached,"BLE values exactly at target boundaries remain neutral");
            Check(BreathMeterReading.Evaluate(sample(1199,1199),config,true).Lane==BreathLane.Lower&&BreathMeterReading.Evaluate(sample(1401,1401),config,true).Lane==BreathLane.Upper,"Crossing the target produces inhale/down or exhale/up without changing input mapping");
            Check(!BreathMeterReading.Evaluate(sample(900,900),config,true,false).Available,"Keyboard mode cannot report a successful hardware measurement");
            var stale=new BreathSensorSnapshot(BreathSensorStatus.Connected,"simulated","",true,900,1);
            Check(!BreathMeterReading.Evaluate(stale,config,true).Available&&!BreathMeterReading.Evaluate(default,config,true).Available,"Stale samples and disconnection disable the meter");
            config.bluetoothDeviationMv=150;
            Check(Math.Abs(BreathMeterReading.Evaluate(sample(1225,1225),config,true).Strength-.5)<.001,"Meter follows edited hardware target values");
            var usb=BreathMeterReading.Evaluate(sample(400,900),config,false);
            Check(usb.Available&&usb.TargetReached&&usb.Lane==BreathLane.Lower&&usb.Strength==1,"USB mode uses its actual 400/1000 mV thresholds and raw sample");
            config.bluetoothDeviationMv=0;
            Check(!BreathMeterReading.Evaluate(sample(900,900),config,true).Available,"Invalid targets do not enable the hardware meter");
        }
        static void SetResolution(int w,int h){if(target!=null){camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);}target=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=(float)w/h;Canvas.ForceUpdateCanvases();}
        static void Capture(string name){Directory.CreateDirectory(Folder);Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Check(image.GetPixel(target.width/2,target.height/2).maxColorComponent>.05f,"Capture contains rendered UI: "+name);}finally{RenderTexture.active=previous;Object.DestroyImmediate(image);}}
        static void Finish(bool success,string error){
            if(Camera.main!=null)Camera.main.targetTexture=null;if(camera!=null)camera.targetTexture=null;
            if(target!=null){target.Release();Object.DestroyImmediate(target);target=null;}if(camera!=null)Object.DestroyImmediate(camera.gameObject);
            Time.timeScale=previousScale>0?previousScale:1;Application.runInBackground=SessionState.GetBool(Key+"Background",false);
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/Validation.json",JsonUtility.ToJson(new Report{date=DateTime.Now.ToString("O"),success=success,error=error,checks=checks.ToArray()},true));
            routine=null;SessionState.SetBool(Key+"Stopping",true);EditorApplication.isPlaying=false;if(!success)Debug.LogError(error);else Debug.Log("Figma UI validation passed: "+checks.Count+" checks.");
        }
    }
}
