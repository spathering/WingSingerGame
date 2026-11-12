using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WingSinger.UI;
using Object=UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class FirstFlightSequenceValidation {
        const string Key="WingSinger.B.SequenceValidation";
        static IEnumerator routine;static double started;static readonly List<string> checks=new List<string>();
        static FirstFlightSequenceValidation(){EditorApplication.update+=Tick;}
        public static void Begin(bool fullPlaythrough=false){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before B validation.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene before isolated B validation.");
            var flow=Object.FindObjectOfType<BreathUIFlowController>();flow.profileKey="";flow.sensorInput.enabled=false;
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",false);SessionState.SetBool(Key+"Background",Application.runInBackground);
            SessionState.SetBool(Key+"Full",fullPlaythrough);
            EditorApplication.isPlaying=true;
        }
        static void Check(bool ok,string message){
            if(!ok)throw new Exception(message);
            if(checks.Count==0||checks[checks.Count-1]!="PASS: "+message){checks.Add("PASS: "+message);File.WriteAllText("Temp/WingSingerB.validation-progress.txt",message);}
        }
        static void Tick(){
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Restore",false)){
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key,false);EditorSceneManager.OpenScene("Assets/Scenes/BreathDemo.unity");return;
            }
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try{
                if(routine==null){checks.Clear();started=EditorApplication.timeSinceStartup;routine=Run();}
                if(EditorApplication.timeSinceStartup-started>300)throw new Exception("Sequence validation timeout");
                if(!routine.MoveNext())Finish(null);
            }catch(Exception e){Finish(e.ToString());}
        }
        static IEnumerator Run(){
            Application.runInBackground=true;for(int i=0;i<5;i++)yield return null;
            var flow=Object.FindObjectOfType<BreathUIFlowController>();var manager=flow.levelManager;
            var input=new GameObject("B test input",typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();manager.input=input;
            flow.router.Route(UIRouteAction.SelectLevel,level:1);flow.router.Route(UIRouteAction.StartFlight);
            var level=manager.CurrentLevel;var sequence=level.sequence;var pattern=manager.Session.Pattern;var camera=Camera.main;
            Check(pattern.Segmented&&pattern.LoopCount==2&&pattern.Duration==100&&pattern.Inhale==2&&pattern.Exhale==3,"Level 1 has two 50-second loops and 2/3-second dwell times");
            Check(Math.Abs(pattern.MatchingSeconds(0,100,BreathLane.Upper)-28)<.001&&Math.Abs(pattern.MatchingSeconds(0,100,BreathLane.Lower)-42)<.001,"Matching time excludes middle sections and transitions across both loops");
            foreach(double offset in new[]{0d,50d}){
                Check(pattern.LaneAt(offset)==BreathLane.Middle&&Math.Abs(pattern.HeightAt(offset))<.001,"Each loop begins on the middle lane");
                Check(pattern.LaneAt(offset+49.9)==BreathLane.Middle&&Math.Abs(pattern.HeightAt(offset+49.9))<.001,"Each loop ends on the middle lane");
                Check(!level.PaperLayout.Any(p=>p.Time>offset+pattern.ReturnStart+.0001&&p.Time<offset+50),"No stars appear in the ending middle section");
            }
            var position=manager.player.transform.position;
            while(!sequence.HasDecodedOpening){Check(sequence.PlaybackError==null,"Opening prepares without decoder errors");yield return null;}
            yield return null;yield return null;
            Check(sequence.Stage==LevelPresentationStage.Opening&&manager.Session.Elapsed==0,"Opening holds route time at zero");
            Check(camera.orthographicSize>11&&Vector3.Distance(position,manager.player.transform.position)<.001,"Opening uses overview camera and stationary player");
            Capture("Opening");
            manager.SetPaused(true);double videoTime=sequence.StageTime;double until=EditorApplication.timeSinceStartup+.35;
            while(EditorApplication.timeSinceStartup<until)yield return null;
            Check(Math.Abs(sequence.StageTime-videoTime)<.1&&manager.Session.Elapsed==0,"Pause freezes opening video and route clock");manager.SetPaused(false);
            while(sequence.Stage==LevelPresentationStage.Opening)yield return null;
            Check(sequence.Stage==LevelPresentationStage.ZoomIn,"Opening completes before the zoom begins");
            until=EditorApplication.timeSinceStartup+.6;while(EditorApplication.timeSinceStartup<until)yield return null;
            Check(camera.orthographicSize>3.8&&camera.orthographicSize<12.3,"Camera smoothly interpolates from overview to gameplay");Capture("Zoom");
            while(sequence.IsBlocking)yield return null;
            Check(manager.Session.Phase==BreathPhase.Running&&camera.orthographicSize<5,"Gameplay starts directly after the zoom");
            var arrow=level.GetComponent<GuideOffscreenArrow>();
            manager.SetPaused(true);var guidePosition=level.guide.position;
            // Test direction against the final camera; manager pause hides it, so temporarily release within this synchronous sample.
            manager.SetPaused(false);level.guide.position=camera.transform.position+new Vector3(1,30,10);arrow.Sync();
            Check(arrow.IsVisible&&arrow.ScreenDirection.y>0,"Offscreen crane above the viewport shows an upward arrow");
            level.guide.position=camera.transform.position+new Vector3(-1,-30,10);arrow.Sync();
            Check(arrow.IsVisible&&arrow.ScreenDirection.y<0,"Offscreen crane below the viewport shows a downward arrow");
            level.guide.position=camera.transform.position+Vector3.forward*10;arrow.Sync();Check(!arrow.IsVisible,"Arrow hides when crane returns on screen");
            level.guide.position=guidePosition;manager.SetPaused(true);
            if(SessionState.GetBool(Key+"Full",false)){
                manager.SetPaused(false);
                bool capturedArrow=false;
                while(sequence.Stage!=LevelPresentationStage.Intermission){
                    double local=pattern.LocalTime(manager.Session.PlayTime);
                    input.Submit(pattern.IsPreparing(manager.Session.PlayTime)?BreathLane.Upper:pattern.LaneAt(manager.Session.PlayTime));
                    yield return null;
                    if(sequence.Stage==LevelPresentationStage.Intermission)break;
                    if(local<pattern.Preparation-.1||local>pattern.ReturnStart+.5)
                        Check(manager.Session.PlayerLane==BreathLane.Middle,"First loop overrides held hardware input on the middle sections");
                    if(!capturedArrow&&local>5){
                        // The real camera follows the lower player while the guide remains above.
                        input.Submit(BreathLane.Lower);
                        double untilArrow=EditorApplication.timeSinceStartup+.5;
                        while(EditorApplication.timeSinceStartup<untilArrow)yield return null;
                        if(arrow.IsVisible){Capture("Arrow");capturedArrow=true;}
                    }
                }
                Check(manager.Session.CollectedCount>5,"Continuous first-loop play collects actual paper stars");
                Check(Math.Abs(manager.Session.PlayTime-50)<.001,"Actual first loop runs for 50 seconds before intermission");
            }else{
                manager.Session.Advance(pattern.Lead+50-manager.Session.Elapsed,BreathLane.Middle);manager.player.Present(manager.Session,level.settings);level.Present(manager.Session);sequence.AfterRouteAdvance();
            }
            int firstScore=manager.Session.CollectedCount;
            Check(manager.Session.Phase==BreathPhase.Running&&sequence.Stage==LevelPresentationStage.Intermission,"Loop 1 enters intermission without exposing Finished to the existing UI");
            manager.SetPaused(false);while(!sequence.HasDecodedIntermission){Check(sequence.PlaybackError==null,"Intermission prepares without decoder errors");yield return null;}
            until=EditorApplication.timeSinceStartup+1;while(EditorApplication.timeSinceStartup<until)yield return null;
            Check(sequence.RestAlpha>.8f&&Math.Abs(manager.Session.PlayTime-50)<.001,"Fullscreen intermission fades in while gameplay stays frozen at 50 seconds");Capture("Intermission");
            manager.SetPaused(true);videoTime=sequence.RestPlaybackTime;until=EditorApplication.timeSinceStartup+.35;
            while(EditorApplication.timeSinceStartup<until)yield return null;
            Check(Math.Abs(sequence.RestPlaybackTime-videoTime)<.1&&Math.Abs(manager.Session.PlayTime-50)<.001,"Pause freezes the intermission video too");manager.SetPaused(false);
            bool sawFadeOut=false;
            while(sequence.Stage==LevelPresentationStage.Intermission){
                if(sequence.RestPlaybackTime>sequence.intermission.length-.6&&sequence.RestAlpha<.8f)sawFadeOut=true;
                Check(sequence.PlaybackError==null,"Intermission continues without decoder errors");yield return null;
            }
            Check(sawFadeOut&&manager.Session.PlayTime>=50&&manager.Session.PlayTime<51,"Full source video fades out and seamlessly resumes loop 2");
            Check(flow.CurrentResult==null&&manager.Session.CollectedCount==firstScore,"Intermission does not settle or alter accumulated score");
            if(SessionState.GetBool(Key+"Full",false)){
                while(manager.Session.PlayTime<99){
                    input.Submit(pattern.IsPreparing(manager.Session.PlayTime)?BreathLane.Lower:pattern.LaneAt(manager.Session.PlayTime));
                    yield return null;
                    if(manager.Session!=null&&pattern.LocalTime(manager.Session.PlayTime)>pattern.ReturnStart+.5)
                        Check(manager.Session.PlayerLane==BreathLane.Middle,"Second loop forces middle lane even while lower input remains held");
                }
                Check(manager.Session.CollectedCount>firstScore,"Second loop adds real collections to the same score");Capture("Ending");
            }
            manager.SetPaused(true);manager.Session.Advance(pattern.Lead+99.9-manager.Session.Elapsed,BreathLane.Middle);
            manager.player.Present(manager.Session,level.settings);level.Present(manager.Session);sequence.AfterRouteAdvance();yield return null;yield return null;
            Check(Math.Abs(level.guide.position.y-level.spawnPoint.position.y-level.settings.LaneHeight(0))<.01,"Crane is on the middle lane before final completion");
            Check(Math.Abs(manager.player.body.position.y-level.spawnPoint.position.y-level.settings.LaneHeight(0))<.01,"Player is on the middle lane before final completion");
            manager.SetPaused(false);while(manager.Session!=null)yield return null;
            Check(flow.CurrentResult!=null&&flow.CurrentResult.seconds==100,"Existing UI settles exactly once after loop 2");
            manager.SwitchLevel(1);yield return null;
            Check(manager.Sequence==null&&!manager.Session.Pattern.Segmented&&manager.Session.Pattern.Inhale==4&&manager.Session.Pattern.Exhale==6,"Level 2 retains its original single-loop configuration");
            manager.SwitchLevel(0);yield return null;Check(manager.Sequence.Stage==LevelPresentationStage.Opening&&manager.Session.Elapsed==0,"Restarting Level 1 resets opening and route clocks");
            flow.ExitSession();yield return null;
            Check(manager.Session==null&&manager.levels.All(l=>!l.gameObject.activeSelf)&&!manager.player.HoldPresentation,"Exit cleans both video players, arrow and player presentation override");
        }
        static void Capture(string label){
            Directory.CreateDirectory("Docs/Images/BSequence");
            // Include the B-owned overlay too, preserving its ordering above A's HUD.
            var camera=Camera.main;var oldTarget=camera.targetTexture;float aspect=camera.aspect;
            var oldActive=RenderTexture.active;var target=new RenderTexture(1280,720,24);
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.enabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
            var raw=Object.FindObjectsOfType<UnityEngine.UI.RawImage>();var sizes=raw.Select(r=>r.rectTransform.sizeDelta).ToArray();
            try{
                target.Create();camera.targetTexture=target;camera.aspect=16f/9;
                foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=.5f;}
                Canvas.ForceUpdateCanvases();
                foreach(var r in raw)if(r.texture!=null){var parent=r.transform.parent as RectTransform;float a=(float)r.texture.width/r.texture.height;float w=Mathf.Min(parent.rect.width,parent.rect.height*a);r.rectTransform.sizeDelta=new Vector2(w,w/a);}
                foreach(var layer in Object.FindObjectsOfType<LoopingPanorama>())layer.Sync();
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes("Docs/Images/BSequence/"+label+".png",image.EncodeToPNG());
            }finally{
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
                for(int i=0;i<raw.Length;i++)raw[i].rectTransform.sizeDelta=sizes[i];
                camera.targetTexture=oldTarget;camera.aspect=aspect;RenderTexture.active=oldActive;
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
            }
        }
        static void Finish(string error){
            var flow=Object.FindObjectOfType<BreathUIFlowController>();if(flow!=null)flow.ExitSession();
            Time.timeScale=1;Application.runInBackground=SessionState.GetBool(Key+"Background",false);
            Directory.CreateDirectory("Docs/Validation");File.WriteAllText("Docs/Validation/FirstFlightSequence.txt",string.Join("\n",checks)+"\n"+(error??"SUCCESS"));
            if(error!=null)Debug.LogError(error);routine=null;SessionState.SetBool(Key+"Restore",true);EditorApplication.isPlaying=false;
        }
    }
}
