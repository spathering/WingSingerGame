using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace WingSinger {
    public enum LevelPresentationStage { Idle, Opening, ZoomIn, GameplayOne, Intermission, GameplayTwo, ReturningToMiddle, Complete, Failed }

    // Owns presentation time only; the manager remains the sole owner of route time and level activation.
    public sealed class FirstFlightSequence : MonoBehaviour {
        public VideoClip opening, intermission;
        public Material openingMaterial;
        [Min(.1f)] public float zoomSeconds = 1.8f, fadeSeconds = .8f;
        [Min(1)] public float overviewSize = 12.3f;
        public LevelPresentationStage Stage { get; private set; }
        public event Action<LevelPresentationStage> StageChanged;
        public bool IsBlocking => active && (Stage == LevelPresentationStage.Opening || Stage == LevelPresentationStage.ZoomIn || Stage == LevelPresentationStage.Intermission);
        public float OverviewBlend => Stage == LevelPresentationStage.Opening ? 1 : Stage == LevelPresentationStage.ZoomIn ? 1-Mathf.SmoothStep(0,1,stageSeconds/zoomSeconds) : 0;
        public double StageTime => Stage == LevelPresentationStage.Opening ? introPlayer.time : Stage == LevelPresentationStage.Intermission ? restPlayer.time : stageSeconds;
        public string PlaybackError { get; private set; }
        public bool HasDecodedOpening => introFrame;
        public bool HasDecodedIntermission => restFrame;
        public double RestPlaybackTime => restPlayer == null ? 0 : restPlayer.time;
        public float RestAlpha => restGroup == null ? 0 : restGroup.alpha;
        public bool IsActive => active;
        BreathLevelManager manager;
        VideoPlayer introPlayer, restPlayer;
        RenderTexture introTexture, restTexture;
        MeshRenderer introQuad;
        Material runtimeMaterial;
        Canvas restCanvas;
        CanvasGroup restGroup;
        RawImage restImage;
        RectTransform restRect;
        AudioSource introAudio, restAudio;
        bool active, introEnded, restEnded, introFrame, restFrame, restPlayed;
        float stageSeconds, prepareSeconds;

        void SetStage(LevelPresentationStage value) {
            if(Stage==value)return;Stage=value;stageSeconds=0;StageChanged?.Invoke(value);
        }
        public void Begin(BreathLevelManager owner) {
            End();manager=owner;Ensure();active=true;restPlayed=false;
            manager.player.HoldPresentation=true;
            introEnded=restEnded=introFrame=restFrame=false;PlaybackError=null;prepareSeconds=0;
            introPlayer.clip=opening;restPlayer.clip=intermission;
            SetStage(LevelPresentationStage.Opening);
            introPlayer.Prepare();restPlayer.Prepare();
        }
        void Ensure() {
            if(introPlayer!=null)return;
            introTexture=new RenderTexture(1280,1280,0,RenderTextureFormat.ARGB32);introTexture.Create();
            restTexture=new RenderTexture(1112,834,0,RenderTextureFormat.ARGB32);restTexture.Create();
            introPlayer=CreatePlayer("Opening video",introTexture,out introAudio);
            restPlayer=CreatePlayer("Intermission video",restTexture,out restAudio);
            introPlayer.prepareCompleted+=OnPrepared;restPlayer.prepareCompleted+=OnPrepared;
            introPlayer.frameReady+=OnFrame;restPlayer.frameReady+=OnFrame;
            introPlayer.loopPointReached+=OnEnded;restPlayer.loopPointReached+=OnEnded;
            introPlayer.errorReceived+=OnError;restPlayer.errorReceived+=OnError;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Opening character video";quad.transform.SetParent(transform,false);
            Destroy(quad.GetComponent<Collider>());introQuad=quad.GetComponent<MeshRenderer>();
            runtimeMaterial=new Material(openingMaterial);runtimeMaterial.mainTexture=introTexture;introQuad.sharedMaterial=runtimeMaterial;
            introQuad.sortingOrder=12;introQuad.enabled=false;
            var canvasGo=new GameObject("Intermission screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));canvasGo.transform.SetParent(transform,false);
            restCanvas=canvasGo.GetComponent<Canvas>();restCanvas.renderMode=RenderMode.ScreenSpaceOverlay;restCanvas.sortingOrder=500;
            restGroup=canvasGo.GetComponent<CanvasGroup>();restGroup.blocksRaycasts=false;restGroup.interactable=false;restGroup.alpha=0;
            var black=new GameObject("Letterbox",typeof(RectTransform),typeof(Image));black.transform.SetParent(canvasGo.transform,false);
            var rect=black.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=black.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;
            var view=new GameObject("Video",typeof(RectTransform),typeof(RawImage));view.transform.SetParent(canvasGo.transform,false);
            restImage=view.GetComponent<RawImage>();restImage.texture=restTexture;restImage.raycastTarget=false;restRect=view.GetComponent<RectTransform>();
            restCanvas.enabled=false;
        }
        VideoPlayer CreatePlayer(string label,RenderTexture texture,out AudioSource audio) {
            var go=new GameObject(label,typeof(VideoPlayer),typeof(AudioSource));go.transform.SetParent(transform,false);
            audio=go.GetComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;
            var v=go.GetComponent<VideoPlayer>();v.playOnAwake=false;v.isLooping=false;v.waitForFirstFrame=true;v.skipOnDrop=true;
            v.source=VideoSource.VideoClip;v.renderMode=VideoRenderMode.RenderTexture;v.targetTexture=texture;v.sendFrameReadyEvents=true;
            v.audioOutputMode=VideoAudioOutputMode.AudioSource;v.controlledAudioTrackCount=1;v.SetTargetAudioSource(0,audio);
            return v;
        }
        void OnPrepared(VideoPlayer video) { if(active && video==introPlayer && Stage==LevelPresentationStage.Opening && !manager.IsPaused)video.Play(); }
        void OnFrame(VideoPlayer video,long frame) {if(video==introPlayer)introFrame=true;else restFrame=true;}
        void OnEnded(VideoPlayer video) { if(!active)return;if(video==introPlayer)introEnded=true;else restEnded=true; }
        void OnError(VideoPlayer video,string error) {if(!active)return;PlaybackError=error;Debug.LogError("WingSinger video: "+error,this);SetStage(LevelPresentationStage.Failed);}

        // Returns true only when route time is allowed to advance.
        public bool Tick(float dt) {
            if(!active)return true;
            if(manager.IsPaused)return false;
            stageSeconds+=Mathf.Max(0,dt);
            if(Stage==LevelPresentationStage.Failed)return false;
            if(Stage==LevelPresentationStage.Opening){
                if(!introFrame){prepareSeconds+=dt;if(prepareSeconds>20)OnError(introPlayer,"Opening video did not decode within 20 seconds.");}
                if(introPlayer.isPrepared&&!introPlayer.isPlaying&&!introEnded)introPlayer.Play();
                if(introEnded)SetStage(LevelPresentationStage.ZoomIn);
                return false;
            }
            if(Stage==LevelPresentationStage.ZoomIn){
                // Use the zoom to establish the guide's lead while the player stays still.
                double preview=manager.Session.Pattern.Lead*Mathf.Clamp01(stageSeconds/zoomSeconds);
                manager.Session.Advance(Math.Max(0,preview-manager.Session.Elapsed),BreathLane.Middle);
                manager.CurrentLevel.Present(manager.Session);
                if(stageSeconds<zoomSeconds)return false;
                introQuad.enabled=false;manager.player.HoldPresentation=false;manager.player.characterAnimation.target.enabled=true;SetStage(LevelPresentationStage.GameplayOne);
            }
            if(Stage==LevelPresentationStage.Intermission){
                if(!restPlayer.isPrepared || !restFrame){
                    prepareSeconds+=dt;if(prepareSeconds>20)OnError(restPlayer,"Intermission video did not decode within 20 seconds.");
                }
                if(restPlayer.isPrepared&&!restPlayer.isPlaying&&!restEnded)restPlayer.Play();
                if(restFrame){
                    float t=(float)restPlayer.time;float length=(float)intermission.length;
                    restGroup.alpha=Mathf.Min(Mathf.Clamp01(t/fadeSeconds),Mathf.Clamp01((length-t)/fadeSeconds));
                    restAudio.volume=restGroup.alpha;
                }
                if(!restEnded)return false;
                restGroup.alpha=0;restCanvas.enabled=false;manager.player.HoldPresentation=false;SetStage(LevelPresentationStage.GameplayTwo);
            }
            return true;
        }
        public float LimitDelta(float dt) {
            if(!active||restPlayed)return dt;
            return Mathf.Min(dt,(float)Math.Max(0,manager.Session.Pattern.Lead+manager.Session.Pattern.LoopSeconds-manager.Session.Elapsed));
        }
        public void AfterRouteAdvance() {
            if(!active)return;
            var s=manager.Session;
            if(!restPlayed && s.PlayTime>=s.Pattern.LoopSeconds-.00001){
                restPlayed=true;prepareSeconds=0;manager.player.StopAnimation();manager.player.HoldPresentation=true;SetStage(LevelPresentationStage.Intermission);
                if(restPlayer.isPrepared)restPlayer.Play();return;
            }
            if(s.Phase==BreathPhase.Finished){SetStage(LevelPresentationStage.Complete);return;}
            if(s.Pattern.IsPreparing(s.PlayTime)&&s.Pattern.LocalTime(s.PlayTime)>=s.Pattern.ReturnStart)
                SetStage(LevelPresentationStage.ReturningToMiddle);
            else SetStage(s.PlayTime<s.Pattern.LoopSeconds?LevelPresentationStage.GameplayOne:LevelPresentationStage.GameplayTwo);
        }
        public void SetPaused(bool value) {
            if(!active)return;
            if(value){introPlayer.Pause();restPlayer.Pause();}
            else if(Stage==LevelPresentationStage.Opening&&introPlayer.isPrepared&&!introEnded)introPlayer.Play();
            else if(Stage==LevelPresentationStage.Intermission&&restPlayer.isPrepared&&!restEnded)restPlayer.Play();
        }
        void LateUpdate() {
            if(!active||manager==null)return;
            bool showIntro=(Stage==LevelPresentationStage.Opening||Stage==LevelPresentationStage.ZoomIn)&&introFrame;
            introQuad.enabled=showIntro;
            manager.player.characterAnimation.target.enabled=!showIntro;
            if(showIntro){
                var bounds=manager.player.characterAnimation.target.bounds;
                float height=bounds.size.y*1.25f;
                introQuad.transform.position=new Vector3(bounds.center.x,bounds.min.y+height*.5f,-1);
                introQuad.transform.localScale=Vector3.one*height;
            }
            restCanvas.enabled=Stage==LevelPresentationStage.Intermission&&restFrame&&!manager.IsPaused;
            float aspect=intermission!=null?(float)intermission.width/intermission.height:4f/3;
            float width=Mathf.Min(Screen.width,Screen.height*aspect);restRect.sizeDelta=new Vector2(width,width/aspect);
        }
        public void End() {
            active=false;
            if(introPlayer!=null){introPlayer.Stop();restPlayer.Stop();introQuad.enabled=false;restCanvas.enabled=false;restGroup.alpha=0;}
            if(manager!=null&&manager.player!=null){manager.player.characterAnimation.target.enabled=true;manager.player.HoldPresentation=false;}
            manager=null;SetStage(LevelPresentationStage.Idle);
        }
        void OnDisable()=>End();
        void OnDestroy(){
            if(introTexture!=null){introTexture.Release();Destroy(introTexture);}
            if(restTexture!=null){restTexture.Release();Destroy(restTexture);}
            if(runtimeMaterial!=null)Destroy(runtimeMaterial);
        }
    }
}
