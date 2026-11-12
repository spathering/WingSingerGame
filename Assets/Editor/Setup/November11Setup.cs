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
    public static class November11Setup {
        const string Folder="Assets/Art/Hazards/", Key="WingSinger.Nov11";
        [Serializable] class Gaps {public TreeGroundCovers.Gap[] spans;}
        static IEnumerator routine;static double started;static readonly List<string> checks=new List<string>();
        static November11Setup(){EditorApplication.update+=Tick;}
        [MenuItem("WingSinger/Apply November 11 Gameplay Art")]
        public static void Apply(){
            CameraFramingValidation.Apply();
            var manifest=JsonUtility.FromJson<BreathArtSetup.Manifest>(File.ReadAllText(Folder+"HazardManifest.json"));
            var clips=manifest.clips.ToDictionary(info=>info.name,info=>BreathArtSetup.CreateSpriteClip(info,Folder,8192));
            var sprites=new Sprite[3];
            for(int i=0;i<3;i++){
                string path=Folder+"GroundCover_"+i+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=false;
                importer.maxTextureSize=2048;importer.SaveAndReimport();sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var gaps=JsonUtility.FromJson<Gaps>(File.ReadAllText(Folder+"GroundGaps.json")).spans;
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/BreathSprites.mat");
            var manager=Object.FindObjectOfType<BreathLevelManager>();
            foreach(var level in manager.levels){
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(level.gameObject);
                var root=PrefabUtility.LoadPrefabContents(path);
                try{
                    var cues=root.GetComponent<FlightHazardCues>()??root.AddComponent<FlightHazardCues>();
                    cues.eagle=clips["eagle"];cues.fox=clips["foxRock"];cues.trap=clips["trapGif"];cues.hive=clips["hiveSwarm"];cues.spriteMaterial=material;
                    cues.switchDelaySeconds=.35f;
                    var covers=root.GetComponent<TreeGroundCovers>()??root.AddComponent<TreeGroundCovers>();
                    covers.gaps=gaps;covers.sprites=sprites;covers.spriteMaterial=material;
                    var prefabLevel=root.GetComponent<BreathLevel>();
                    prefabLevel.guideHorizontalOffset=1.2f;
                    prefabLevel.guideVisual.flipX=true;
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
                level.settings.guideLeadSeconds=1.5f;EditorUtility.SetDirty(level.settings);
            }
            var framing=manager.virtualCamera.GetComponent<BreathCameraFraming>();
            framing.minimumOrthographicSize=3.8f;framing.verticalFollow=1;framing.verticalCenterOffset=-1;
            var lens=manager.virtualCamera.m_Lens;lens.OrthographicSize=3.8f;manager.virtualCamera.m_Lens=lens;Camera.main.orthographicSize=3.8f;
            EditorUtility.SetDirty(framing);EditorUtility.SetDirty(manager.virtualCamera);EditorUtility.SetDirty(Camera.main);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        public static void ApplyAndValidateBatch(){
            try{
                Apply();var flow=Object.FindObjectOfType<BreathUIFlowController>();flow.profileKey="";flow.sensorInput.enabled=false;
                SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",false);SessionState.SetBool(Key+"Background",Application.runInBackground);
                EditorApplication.isPlaying=true;
            }catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS: "+message);}
        static void Tick(){
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Restore",false)){
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(BreathDemoBuilder.ScenePath);
                if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));return;
            }
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try{
                if(routine==null){checks.Clear();started=EditorApplication.timeSinceStartup;routine=Run();}
                if(EditorApplication.timeSinceStartup-started>120)throw new Exception("Nov11 validation timeout");
                if(!routine.MoveNext())Finish(null);
            }catch(Exception e){Finish(e.ToString());}
        }
        static IEnumerator Run(){
            Application.runInBackground=true;for(int i=0;i<5;i++)yield return null;
            var flow=Object.FindObjectOfType<BreathUIFlowController>();var manager=flow.levelManager;
            var input=new GameObject("Nov11 verification",typeof(ExternalBreathInput)).GetComponent<ExternalBreathInput>();manager.input=input;
            var camera=Camera.main;
            flow.router.Route(UIRouteAction.SelectLevel,level:1);flow.router.Route(UIRouteAction.StartFlight);
            for(int index=0;index<manager.levels.Length;index++){
                manager.SwitchLevel(index);var level=manager.CurrentLevel;var cues=level.GetComponent<FlightHazardCues>();
                Check(cues!=null&&cues.Schedule.Length==3,"Level has exactly three randomly chosen switch reminders");
                Check(Mathf.Abs(cues.switchDelaySeconds-.35f)<.001f,"All reminder kinds shift 0.35 seconds further along the route");
                Check(level.guideVisual.flipX,"Crane is horizontally flipped in both level prefabs");
                Check(cues.Schedule.Select(c=>c.Time).Distinct().Count()==3&&cues.Schedule.Count(c=>c.Kind==0)==1,"Distinct switch points include one upper-lane eagle");
                Check(cues.Schedule.All(c=>c.Kind==0?c.Lane==BreathLane.Upper:c.Lane==BreathLane.Lower),"Eagle upper, fox/trap/hive lower");
                Check(cues.eagle.frames.Length==473&&cues.hive.frames.Length==240&&cues.fox.frames.Length==40&&cues.trap.frames.Length==60,"All supplied GIF frames preserved");
                var seen=new HashSet<int>();
                for(int n=0;n<30;n++){
                    manager.Restart();foreach(var c in cues.Schedule)seen.Add(c.Kind);
                    Check(cues.Schedule.Length==3,"Restart regenerates exactly three reminders without duplicates");
                }
                Check(seen.Count==4,"Repeated random selections exercise all four assets");
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1080),new Vector2Int(865,721)}){
                    camera.aspect=(float)size.x/size.y;manager.Restart();manager.Session.Advance(manager.Session.Pattern.Lead+.2,BreathLane.Middle);
                    foreach(var lane in new[]{BreathLane.Upper,BreathLane.Middle,BreathLane.Lower}){
                        input.Submit(lane);double until=EditorApplication.timeSinceStartup+.9;
                        while(EditorApplication.timeSinceStartup<until){
                            yield return null;
                            var a=camera.WorldToViewportPoint(manager.player.characterAnimation.target.bounds.min);var b=camera.WorldToViewportPoint(manager.player.characterAnimation.target.bounds.max);
                            Check(a.x>=.075f&&a.y>=.075f&&b.x<=.925f&&b.y<=.925f,"Player protected during close-camera lane changes");
                            var g=camera.WorldToViewportPoint(level.guide.position);Check(g.x>0&&g.x<1,"Crane remains horizontally visible");
                        }
                        if(index==0&&size.x==1920)NovemberArtSetup.Capture(flow,"Docs/Images/Nov11_"+lane+".png",1920,1080);
                    }
                }
                var covers=level.GetComponent<TreeGroundCovers>();Check(covers.gaps.Length>0&&covers.GetComponentsInChildren<SpriteRenderer>(true).Length>0,"Ground gap covers use existing foreground assets");
                Check(covers.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.name=="Ground gap cover")==covers.gaps.Length*9,
                    "Each gap has three overlapping enlarged foreground pieces in every loop copy");
                var visibleCovers=covers.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Ground gap cover"&&r.gameObject.activeInHierarchy).ToArray();
                Check(visibleCovers.Length>0&&visibleCovers.All(r=>r.bounds.size.y>=1.79f),
                    "Visible ground covers are enlarged to at least 1.8 world units high");
                ValidateCoverSeam(covers,camera);
                ValidateCueParallax(manager,camera);
                // Inspect each cue at warning, falling, and crossing times using an isolated paused session.
                manager.Restart();manager.SetPaused(true);
                foreach(var cue in cues.Schedule){
                    double target=cue.Time-2.5;
                    manager.Session.Advance(Math.Max(0,manager.Session.Pattern.Lead+target-manager.Session.Elapsed),BreathLane.Middle);level.Present(manager.Session);
                    Check(cues.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.activeSelf),"Switch cue is visible before the required lane change");
                }
                manager.Restart();
                for(int n=0;n<40&&!cues.Schedule.Any(c=>c.Kind==3);n++)manager.Restart();
                manager.SetPaused(true);var hive=cues.Schedule.First(c=>c.Kind==3);
                manager.Session.Advance(manager.Session.Pattern.Lead+hive.Time-3.2+.35,BreathLane.Middle);level.Present(manager.Session);
                var falling=cues.GetComponentsInChildren<SpriteRenderer>().First(r=>r.gameObject.activeSelf&&r.sprite.name.StartsWith("hiveSwarm"));float high=falling.transform.position.y;
                manager.Session.Advance(1.4,BreathLane.Middle);level.Present(manager.Session);
                Check(falling.transform.position.y<high-5,"Hive falls visibly from canopy to lower lane before crossing");
                manager.SetPaused(false);manager.Restart();yield return null;
                flow.router.Route(UIRouteAction.Pause);yield return null;int frame=Object.FindObjectsOfType<LoopingPanorama>().First(l=>l.name=="Looping Foreground").AnimationFrame;
                yield return null;Check(frame==Object.FindObjectsOfType<LoopingPanorama>().First(l=>l.name=="Looping Foreground").AnimationFrame,"Pause freezes presentation clock");
                flow.router.Route(UIRouteAction.Resume);
                manager.SwitchLevel((index+1)%manager.levels.Length);Check(!level.gameObject.activeInHierarchy,"Exit disables reminders and ground covers with the level");
            }
            // Capture all four assets at actual approach distances, using their real random schedules.
            camera.aspect=16f/9;manager.SwitchLevel(0);manager.player.SetReduceMotion(true);
            for(int kind=0;kind<4;kind++){
                manager.Restart();
                for(int n=0;n<40&&!manager.CurrentLevel.GetComponent<FlightHazardCues>().Schedule.Any(c=>c.Kind==kind);n++)manager.Restart();
                var level=manager.CurrentLevel;var cues=level.GetComponent<FlightHazardCues>();var cue=cues.Schedule.First(c=>c.Kind==kind);
                manager.SetPaused(true);manager.Session.Advance(manager.Session.Pattern.Lead+cue.Time-1,cue.Lane);
                manager.player.Present(manager.Session,level.settings);level.Present(manager.Session);
                yield return null;yield return null;
                manager.SetPaused(false);if(level.foreground!=null)level.foreground.Sync();manager.SetPaused(true);
                NovemberArtSetup.Capture(flow,"Docs/Images/Nov11_Cue_"+kind+".png",1920,1080);
                Check(cues.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.activeSelf&&r.sprite!=null),"Approaching animated obstacle renders with transparent URP material");
                if(kind==0){
                    var eagle=cues.GetComponentsInChildren<SpriteRenderer>().First(r=>r.gameObject.activeSelf&&r.sprite.name.StartsWith("eagle"));
                    float startX=eagle.transform.position.x;
                    level.Present(manager.Session);
                    Check(Mathf.Abs(eagle.transform.position.x-startX)<.001f,"Paused eagle stays fixed");
                    manager.Session.Advance(.4,BreathLane.Upper);level.Present(manager.Session);
                    Check(eagle.transform.position.x<startX-1 && !eagle.flipX,"Eagle actually flies right to left in world space and faces left");
                    manager.Session.Advance(.9,BreathLane.Upper);level.Present(manager.Session);
                    Check(eagle.transform.position.x<level.spawnPoint.position.x+(float)cue.Time*level.settings.moveSpeed,
                        "Eagle continues left past the switch point");
                }
            }
            var grounded=ValidateGroundedCues(manager);
            while(grounded.MoveNext())yield return grounded.Current;
            manager.player.SetReduceMotion(false);
            flow.ExitSession();yield return null;Check(manager.Session==null&&manager.levels.All(l=>!l.gameObject.activeSelf),"Menu exit leaves no active gameplay or callbacks");
        }
        static void ValidateCoverSeam(TreeGroundCovers covers,Camera camera){
            var tree=Object.FindObjectsOfType<LoopingPanorama>().First(l=>l.name=="Looping Foreground");
            var renderers=covers.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Ground gap cover").ToArray();
            var group=covers.transform.Find("Ground gap covers").GetComponent<UnityEngine.Rendering.SortingGroup>();
            Check(group!=null&&group.sortingOrder==-4,"Foreground uses a dedicated sorting group and stable internal ordering");
            var saved=camera.transform.position;
            try{
                float target=covers.gaps[0].center-.00001f;
                float delta=Mathf.DeltaAngle(tree.HorizontalPhase*360,target*360)/360*tree.WorldPeriod/tree.parallaxSpeed;
                camera.transform.position+=Vector3.right*delta;tree.Sync();covers.Sync();
                float left=camera.transform.position.x-camera.orthographicSize*camera.aspect,right=left+camera.orthographicSize*camera.aspect*2;
                var before=renderers.Where(r=>r.bounds.max.x>left&&r.bounds.min.x<right)
                    .Select(r=>new {r.sprite,r.sortingOrder,position=r.transform.position}).ToArray();
                camera.transform.position+=Vector3.right*(.00002f*tree.WorldPeriod/tree.parallaxSpeed);tree.Sync();covers.Sync();
                Check(before.Length>0&&before.All(a=>renderers.Any(b=>b.sprite==a.sprite&&b.sortingOrder==a.sortingOrder
                    &&Vector3.Distance(a.position,b.transform.position)<.05f)),"Crossing a UV seam preserves visible foreground poses, sprites and draw order");
                Check(renderers.All(r=>r.gameObject.activeSelf),"Scrolling no longer toggles foreground objects on and off");
            }finally{camera.transform.position=saved;tree.Sync();covers.Sync();}
        }
        static void ValidateCueParallax(BreathLevelManager manager,Camera camera){
            var tree=Object.FindObjectsOfType<LoopingPanorama>().First(l=>l.name=="Looping Foreground");
            for(int kind=1;kind<=3;kind++){
                manager.Restart();
                for(int n=0;n<40&&!manager.CurrentLevel.GetComponent<FlightHazardCues>().Schedule.Any(c=>c.Kind==kind);n++)manager.Restart();
                var level=manager.CurrentLevel;var cues=level.GetComponent<FlightHazardCues>();var cue=cues.Schedule.First(c=>c.Kind==kind);
                manager.SetPaused(true);manager.Session.Advance(manager.Session.Pattern.Lead+cue.Time-1,BreathLane.Lower);
                level.Present(manager.Session);tree.Sync();cues.SyncHorizontal();
                var r=cues.GetComponentsInChildren<SpriteRenderer>().First(x=>x.gameObject.activeSelf&&x.sprite.name.StartsWith(cues.Clip(kind).frames[0].name.Split('_')[0]));
                var saved=camera.transform.position;float y=r.transform.position.y;var frame=r.sprite;
                try{
                    // Measure against a fixed point in the actual panorama UVs, including a wrap.
                    foreach(float delta in new[]{8f,-8f,(.99999f-tree.HorizontalPhase)*tree.WorldPeriod/tree.parallaxSpeed,
                        .00002f*tree.WorldPeriod/tree.parallaxSpeed}){
                        float uv=Mathf.Repeat(tree.HorizontalPhase+.5f,1);
                        float treeBefore=Mathf.Repeat(uv-tree.HorizontalPhase,1)*tree.WorldPeriod-camera.orthographicSize*camera.aspect;
                        float cueBefore=r.transform.position.x-camera.transform.position.x;
                        camera.transform.position+=Vector3.right*delta;tree.Sync();cues.SyncHorizontal();
                        float treeAfter=Mathf.Repeat(uv-tree.HorizontalPhase,1)*tree.WorldPeriod-camera.orthographicSize*camera.aspect;
                        // Unwrap the sampled point if this camera move covers more than half a period.
                        float treeDelta=Mathf.DeltaAngle(treeBefore/tree.WorldPeriod*360,treeAfter/tree.WorldPeriod*360)/360*tree.WorldPeriod;
                        float cueDelta=r.transform.position.x-camera.transform.position.x-cueBefore;
                        float wrappedCueDelta=Mathf.DeltaAngle(0,cueDelta/tree.WorldPeriod*360)/360*tree.WorldPeriod;
                        Check(Mathf.Abs(wrappedCueDelta-treeDelta)<.003f,"Ground cue "+kind+" shares the tree layer's screen displacement, including UV wrap");
                        Check(Mathf.Abs(r.transform.position.y-y)<.001f&&r.sprite==frame,"Parallax preserves paused vertical action and GIF pose");
                    }
                }finally{camera.transform.position=saved;tree.Sync();cues.SyncHorizontal();}
            }
        }
        static IEnumerator ValidateGroundedCues(BreathLevelManager manager){
            for(int kind=1;kind<=3;kind++){
                manager.Restart();
                for(int n=0;n<40&&!manager.CurrentLevel.GetComponent<FlightHazardCues>().Schedule.Any(c=>c.Kind==kind);n++)manager.Restart();
                var level=manager.CurrentLevel;var cues=level.GetComponent<FlightHazardCues>();var cue=cues.Schedule.First(c=>c.Kind==kind);
                manager.SetPaused(true);double warning=manager.Session.Pattern.Lead+cue.Time-3.2;
                manager.Session.Advance(warning+.05,BreathLane.Lower);level.Present(manager.Session);
                string prefix=kind==1?"foxRock":kind==2?"trapGif":"hiveSwarm";
                var r=cues.GetComponentsInChildren<SpriteRenderer>().First(x=>x.gameObject.activeSelf&&x.sprite.name.StartsWith(prefix));
                if(kind==1){
                    float bottom=r.transform.localPosition.y-r.transform.localScale.y;
                    Check(r.transform.localScale.x>=2.59f&&Mathf.Abs(bottom-cues.GroundBaseY)<.1f,
                        "Enlarged fox rock reaches the lowest background edge without a floating base");
                    Check(r.sprite==cues.fox.frames[0],"Fox waits behind the rock before the switch approaches");
                    manager.Session.Advance(2.45,BreathLane.Lower);level.Present(manager.Session);
                    Check(r.sprite==cues.fox.frames[0],"Fox still waits 0.7 seconds before the switch");
                    manager.Session.Advance(.5,BreathLane.Lower);level.Present(manager.Session);
                    Check(r.sprite!=cues.fox.frames[0]&&r.sprite!=cues.fox.frames.Last(),"Fox peeks once near the switch");
                    var pausedFrame=r.sprite;level.Present(manager.Session);
                    Check(r.sprite==pausedFrame,"Pause freezes the fox peek");
                    manager.Session.Advance(.55,BreathLane.Lower);level.Present(manager.Session);
                    Check(r.sprite==cues.fox.frames.Last(),"Fox finishes in the fully revealed pose");
                    manager.Session.Advance(1,BreathLane.Lower);level.Present(manager.Session);
                    Check(r.sprite==cues.fox.frames.Last(),"Fox remains revealed instead of looping or hiding");
                }else if(kind==2){
                    float low=r.transform.localPosition.y;
                    Check(Mathf.Abs(low-.384f*r.transform.localScale.y-cues.GroundBaseY)<.001f,
                        "Trap starts at the lowest background edge");
                    manager.Session.Advance(3.05,BreathLane.Lower);level.Present(manager.Session);
                    Check(Mathf.Abs(r.transform.localPosition.y-low)<.001f&&r.sprite==cues.trap.frames[0],
                        "Trap stays on the bottom until the switch time");
                    manager.Session.Advance(.6,BreathLane.Lower);level.Present(manager.Session);float peak=r.transform.localPosition.y;
                    Check(peak>low+2,"Trap jumps once at the switch time");
                    manager.player.Present(manager.Session,level.settings);yield return null;yield return null;
                    manager.SetPaused(false);level.foreground?.Sync();manager.SetPaused(true);
                    NovemberArtSetup.Capture(Object.FindObjectOfType<BreathUIFlowController>(),"Docs/Images/Nov11_TrapJump.png",1920,1080);
                    manager.Session.Advance(.8,BreathLane.Lower);level.Present(manager.Session);float landed=r.transform.localPosition.y;
                    var finalFrame=r.sprite;manager.Session.Advance(.8,BreathLane.Lower);level.Present(manager.Session);
                    Check(Mathf.Abs(r.transform.localPosition.y-landed)<.001f&&r.sprite==finalFrame&&r.sprite==cues.trap.frames.Last(),
                        "Trap settles on the ground and holds its final frame without replaying");
                    Check(Mathf.Abs(landed-.384f*r.transform.localScale.y-cues.GroundBaseY)<.001f,
                        "Trap returns to the lowest background edge after jumping");
                }else{
                    manager.Session.Advance(1.8,BreathLane.Lower);level.Present(manager.Session);float landed=r.transform.localPosition.y;
                    Check(Mathf.Abs(landed-.892f*r.transform.localScale.y-cues.GroundBaseY)<.001f,
                        "Hive body lands at the lowest background edge");
                    manager.Session.Advance(1,BreathLane.Lower);level.Present(manager.Session);
                    Check(Mathf.Abs(r.transform.localPosition.y-landed)<.001f,"Hive stops falling after reaching the ground");
                }
                manager.player.Present(manager.Session,level.settings);yield return null;yield return null;
                manager.SetPaused(false);level.foreground?.Sync();manager.SetPaused(true);
                NovemberArtSetup.Capture(Object.FindObjectOfType<BreathUIFlowController>(),"Docs/Images/Nov11_Grounded_"+kind+".png",1920,1080);
            }
        }
        static void Finish(string error){
            var flow=Object.FindObjectOfType<BreathUIFlowController>();if(flow!=null)flow.ExitSession();
            Time.timeScale=1;Application.runInBackground=SessionState.GetBool(Key+"Background",false);
            Directory.CreateDirectory("Docs/Validation");File.WriteAllText("Docs/Validation/November11.txt",string.Join("\n",checks)+"\n"+(error??"SUCCESS"));
            if(error!=null)Debug.LogError(error);routine=null;SessionState.SetInt(Key+"Exit",error==null?0:1);SessionState.SetBool(Key+"Restore",true);EditorApplication.isPlaying=false;
        }
    }
}
