using System;
using UnityEngine;
using UnityEngine.UI;

namespace WingSinger {
    public sealed class CloudTransitionOverlay : MonoBehaviour {
        public Texture2D[] frames=Array.Empty<Texture2D>();
        [Min(1)] public float framesPerSecond=30;
        [Min(0)] public int switchFrame=96;
        public bool IsConfigured=>frames!=null&&frames.Length>1;
        public bool IsPlaying {get;private set;}
        public float Elapsed {get;private set;}
        public bool Released {get;private set;}
        public int Frame {get;private set;}
        public bool AtSwitch=>Elapsed>=SwitchTime;
        public float Duration=>IsConfigured?frames.Length/Mathf.Max(1,framesPerSecond):0;
        float SwitchTime=>Mathf.Clamp(switchFrame,0,frames.Length-1)/Mathf.Max(1,framesPerSecond);
        Canvas canvas;
        RawImage image;
        Image wash;
        Material material;
        bool paused;
        public float CoverAlpha=>wash==null?0:wash.color.a;
        void Ensure() {
            if(canvas!=null)return;
            var root=new GameObject("Cloud transition screen",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=600;
            var group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var cover=new GameObject("Cloud gap cover",typeof(RectTransform),typeof(Image));cover.transform.SetParent(root.transform,false);
            wash=cover.GetComponent<Image>();wash.raycastTarget=false;
            var rect=cover.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var view=new GameObject("Cloud frames",typeof(RectTransform),typeof(RawImage));view.transform.SetParent(root.transform,false);
            image=view.GetComponent<RawImage>();image.raycastTarget=false;
            var shader=Resources.Load<Shader>("WingSingerUIInterpolation");
            if(shader==null)throw new InvalidOperationException("Cloud transitions need the existing UI frame-blend shader.");
            material=new Material(shader);material.SetVector("_FrameRect",new Vector4(1,1,0,0));material.SetVector("_NextRect",new Vector4(1,1,0,0));image.material=material;
            canvas.enabled=false;
        }
        public void Play() {if(!IsConfigured)return;Ensure();Elapsed=0;Released=false;paused=false;IsPlaying=true;Present();}
        public void Release() {Released=true;}
        public bool Advance(float dt) {
            if(!IsPlaying)return true;
            if(paused)return false;
            Elapsed=Mathf.Min(Elapsed+Mathf.Max(0,dt),Released?Duration:SwitchTime);
            Present();if(Elapsed<Duration)return false;Stop();return true;
        }
        public void SetPaused(bool value) {paused=value;if(canvas!=null)canvas.enabled=IsPlaying&&!paused;}
        void Present() {
            float sample=Mathf.Min(Elapsed*Mathf.Max(1,framesPerSecond),frames.Length-1);
            Frame=(int)sample;image.texture=frames[Frame];material.SetTexture("_NextTex",frames[Mathf.Min(Frame+1,frames.Length-1)]);material.SetFloat("_FrameBlend",sample-Frame);
            // The supplied transparent sequence retains tiny gaps even at peak coverage.
            // A gentle cloud-colored wash closes those gaps only around the content switch.
            float cover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(switchFrame-36,switchFrame-16,sample))
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(switchFrame+4,switchFrame+29,sample)));
            wash.color=new Color(.9f,.91f,.92f,cover);canvas.enabled=IsPlaying&&!paused;Fit();
        }
        void Fit() {
            if(image==null||image.texture==null)return;
            var parent=(RectTransform)canvas.transform;float aspect=(float)image.texture.width/image.texture.height;
            float width=Mathf.Max(parent.rect.width,parent.rect.height*aspect);image.rectTransform.sizeDelta=new Vector2(width,width/aspect);
        }
        void LateUpdate(){if(IsPlaying)Fit();}
        public void Stop() {IsPlaying=false;Elapsed=0;Released=false;Frame=0;if(canvas!=null){canvas.enabled=false;image.texture=null;wash.color=Color.clear;}}
        void OnDisable()=>Stop();
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
