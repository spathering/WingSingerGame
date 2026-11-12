using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace WingSinger {
    [DefaultExecutionOrder(10003)]
    public sealed class TreeGroundCovers:MonoBehaviour {
        [Serializable] public struct Gap { public float center,width; }
        public Gap[] gaps;
        public Sprite[] sprites;
        public Material spriteMaterial;
        const int PiecesPerGap=3;
        LoopingPanorama trees;
        SpriteRenderer[] covers;
        void Start(){Ensure();}
        void Ensure(){
            if(trees==null)foreach(var layer in FindObjectsOfType<LoopingPanorama>())if(layer.name=="Looping Foreground")trees=layer;
            if(covers!=null || gaps==null || sprites==null || sprites.Length==0)return;
            covers=new SpriteRenderer[gaps.Length*3*PiecesPerGap];
            var groupObject=new GameObject("Ground gap covers");groupObject.transform.SetParent(transform,false);
            var group=groupObject.AddComponent<SortingGroup>();group.sortingOrder=-4;
            for(int i=0;i<covers.Length;i++){
                var go=new GameObject("Ground gap cover");go.transform.SetParent(groupObject.transform,false);
                int gap=i/(3*PiecesPerGap),piece=i%PiecesPerGap;
                var r=go.AddComponent<SpriteRenderer>();r.sharedMaterial=spriteMaterial;r.sprite=sprites[(gap+piece)%sprites.Length];
                r.sortingOrder=gap*PiecesPerGap+piece;covers[i]=r;
            }
        }
        void LateUpdate(){Sync();}
        public void Sync(){
            Ensure(); var camera=Camera.main;
            if(trees==null || covers==null || camera==null)return;
            float left=camera.transform.position.x-camera.orthographicSize*camera.aspect;
            for(int i=0;i<gaps.Length;i++)for(int k=0;k<3;k++)for(int piece=0;piece<PiecesPerGap;piece++){
                var r=covers[(i*3+k)*PiecesPerGap+piece];
                float groupWidth=Mathf.Max(2.8f,gaps[i].width*trees.WorldPeriod+2)*1.65f;
                float x=left+(Mathf.Repeat(gaps[i].center-trees.HorizontalPhase,1)+k-1)*trees.WorldPeriod
                    +(piece-1)*groupWidth*.22f;
                float scale=Mathf.Max(groupWidth*.62f/r.sprite.bounds.size.x,
                    (piece==1?2.4f:1.8f)/r.sprite.bounds.size.y);
                // Keep pooled renderers enabled and update even offscreen. Unity handles frustum culling.
                r.transform.localScale=Vector3.one*scale;
                // Keep the base on the ground as props grow; overlapping silhouettes cover transparent edges.
                float bottom=trees.transform.position.y-trees.transform.localScale.y*.5f
                    +trees.transform.localScale.y*(piece==1?.025f:.015f);
                float y=bottom+r.sprite.bounds.size.y*scale*.5f;
                r.transform.position=new Vector3(x,y,3);
            }
        }
    }
}
