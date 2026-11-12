using UnityEngine;
using UnityEngine.UI;

namespace WingSinger {
    [DefaultExecutionOrder(10005)]
    public sealed class GuideOffscreenArrow : MonoBehaviour {
        [Range(.02f,.2f)] public float edgeMargin=.065f;
        public bool IsVisible => canvas!=null&&canvas.enabled;
        public Vector2 ScreenDirection {get;private set;}
        BreathLevel level;
        Canvas canvas;
        RectTransform arrow;
        Sprite sprite;
        Texture2D texture;
        void Ensure(){
            if(canvas!=null)return;level=GetComponent<BreathLevel>();
            var go=new GameObject("Crane direction overlay",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Bilinear;
            // A right-facing arrow with a narrow shaft and triangular head.
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){
                bool inside=x>=5&&x<36&&y>=26&&y<=38 || x>=30&&x<=59&&Mathf.Abs(y-32)<=((59-x)*.8f);
                pixels[y*64+x]=inside?new Color(1,.82f,.2f,1):Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply();sprite=Sprite.Create(texture,new Rect(0,0,64,64),Vector2.one*.5f,64);
            var view=new GameObject("Direction",typeof(RectTransform),typeof(Image),typeof(Outline));view.transform.SetParent(go.transform,false);
            arrow=view.GetComponent<RectTransform>();arrow.sizeDelta=new Vector2(58,58);
            var image=view.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
            var outline=view.GetComponent<Outline>();outline.effectColor=new Color(.2f,.25f,.1f,1);outline.effectDistance=new Vector2(2,-2);
            canvas.enabled=false;
        }
        public void Sync(){
            Ensure();var camera=Camera.main;var manager=FindObjectOfType<BreathLevelManager>();
            if(camera==null||level==null||manager==null||manager.CurrentLevel!=level||manager.IsPaused||manager.Session==null
                || manager.Session.Phase==BreathPhase.Finished||manager.IsPresentationBlocking){canvas.enabled=false;return;}
            var p=camera.WorldToViewportPoint(level.guide.position);
            if(p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1){canvas.enabled=false;return;}
            Vector2 direction=new Vector2((p.x-.5f)*camera.pixelWidth,(p.y-.5f)*camera.pixelHeight);
            if(p.z<0)direction=-direction;if(direction.sqrMagnitude<.001f)direction=Vector2.up;
            ScreenDirection=direction.normalized;
            Rect area=Screen.safeArea;
            float halfWidth=Mathf.Max(1,area.width*(.5f-edgeMargin)-29),halfHeight=Mathf.Max(1,area.height*(.5f-edgeMargin)-29);
            float length=Mathf.Min(halfWidth/Mathf.Max(.0001f,Mathf.Abs(ScreenDirection.x)),halfHeight/Mathf.Max(.0001f,Mathf.Abs(ScreenDirection.y)));
            arrow.position=area.center+ScreenDirection*length;
            arrow.rotation=Quaternion.Euler(0,0,Mathf.Atan2(ScreenDirection.y,ScreenDirection.x)*Mathf.Rad2Deg);canvas.enabled=true;
        }
        void LateUpdate()=>Sync();
        void OnDisable(){if(canvas!=null)canvas.enabled=false;}
        void OnDestroy(){if(sprite!=null)Destroy(sprite);if(texture!=null)Destroy(texture);}
    }
}
