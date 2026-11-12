using System;
using System.Collections.Generic;
using UnityEngine;

namespace WingSinger {
    // Visual route-switch reminders, deliberately independent of scoring and hardware input.
    [DefaultExecutionOrder(10004)]
    public sealed class FlightHazardCues : MonoBehaviour {
        public CharacterSpriteClip eagle, fox, trap, hive;
        public Material spriteMaterial;
        [Min(.1f)] public float eagleFlightSpeed = 3;
        [Min(0), Tooltip("Shift reminders forward along the route by this many seconds of travel.")]
        public float switchDelaySeconds = .35f;
        public struct Cue { public double Time; public BreathLane Lane; public int Kind; }
        public Cue[] Schedule { get; private set; } = Array.Empty<Cue>();
        readonly List<SpriteRenderer> visuals = new List<SpriteRenderer>();
        readonly float[] encounterCameraX = new float[3];
        readonly bool[] anchored = new bool[3];
        BreathLevel level;
        LoopingPanorama trees;
        BreathSession presentationSession;
        public float GroundBaseY { get; private set; }
        public float GroundSurfaceY { get; private set; }
        public void Prepare(BreathLevel owner) {
            level = owner;
            trees = null;
            GroundBaseY=owner.spawnPoint.localPosition.y+owner.settings.LaneHeight(-1)-3.6f;
            GroundSurfaceY=GroundBaseY+1.6f;
            foreach(var layer in FindObjectsOfType<LoopingPanorama>())if(layer.name=="Looping Foreground"){
                trees=layer;
                float bottom=layer.transform.position.y-layer.transform.localScale.y*.5f;
                GroundBaseY=transform.InverseTransformPoint(new Vector3(0,bottom,0)).y;
                GroundSurfaceY=transform.InverseTransformPoint(new Vector3(0,bottom+layer.transform.localScale.y*.105f,0)).y;
            }
            var p = owner.settings.CreatePattern();
            double delay = Math.Max(0, switchDelaySeconds);
            var upper = new List<Cue>(); var lower = new List<Cue>();
            for (double t = p.Preparation + p.GuideTransition + p.Inhale; t + p.GuideTransition < p.Duration; t += p.Cycle) {
                double upperTime=t+p.GuideTransition*.6+delay;
                if(upperTime<p.Duration)upper.Add(new Cue { Time=upperTime, Lane=BreathLane.Upper, Kind=0 });
                double down = t+p.GuideTransition+p.Exhale;
                double lowerTime=down+p.GuideTransition*.6+delay;
                if (down+p.GuideTransition < p.Duration && lowerTime<p.Duration)
                    lower.Add(new Cue {Time=lowerTime,Lane=BreathLane.Lower});
            }
            var chosen = new List<Cue>();
            if (upper.Count > 0) chosen.Add(upper[UnityEngine.Random.Range(0,upper.Count)]);
            int firstKind = UnityEngine.Random.Range(1,4);
            for (int i=0; i<2 && lower.Count>0; i++) {
                int at=UnityEngine.Random.Range(0,lower.Count); var cue=lower[at]; lower.RemoveAt(at);
                cue.Kind=1+(firstKind-1+i)%3; chosen.Add(cue);
            }
            chosen.Sort((a,b)=>a.Time.CompareTo(b.Time)); Schedule=chosen.ToArray();
            while(visuals.Count<3) {
                var go=new GameObject("Switch reminder"); go.transform.SetParent(transform,false);
                var r=go.AddComponent<SpriteRenderer>();r.sharedMaterial=spriteMaterial;r.sortingOrder=5;visuals.Add(r);
            }
            ResetPresentation();
        }
        public CharacterSpriteClip Clip(int kind) => kind==0?eagle:kind==1?fox:kind==2?trap:hive;
        public void Present(BreathSession session) {
            presentationSession=session;
            for(int i=0;i<visuals.Count;i++) {
                var r=visuals[i]; if(i>=Schedule.Length){r.gameObject.SetActive(false);continue;}
                var cue=Schedule[i]; double age=session.PlayTime-(cue.Time-3.2);
                bool visible=session.Phase!=BreathPhase.Preview && age>=0 && session.PlayTime<cue.Time+2.5;
                r.gameObject.SetActive(visible);if(!visible)continue;
                // Fox starts peeking shortly before the switch; trap springs at the switch.
                // Both hold frame zero while waiting, then play once and hold their last pose.
                double untilSwitch=cue.Time-session.PlayTime;
                double animationAge=cue.Kind==1?Math.Max(0,.6-untilSwitch)
                    :cue.Kind==2?Math.Max(0,-untilSwitch):age;
                r.sprite=Clip(cue.Kind).Sample(animationAge,cue.Kind==0);
                float scale=cue.Kind==0?1.7f:cue.Kind==1?2.6f:cue.Kind==2?2.6f:1.65f;
                float y=level.settings.LaneHeight((int)cue.Lane);
                if(cue.Kind==1)y=GroundBaseY+scale-.08f;
                if(cue.Kind==2){
                    float jump=Mathf.Clamp01((float)-untilSwitch);
                    y=GroundBaseY+.384f*scale+Mathf.Sin(jump*Mathf.PI)*3;
                }
                if(cue.Kind==3) {
                    float fall=Mathf.SmoothStep(0,1,Mathf.Clamp01(((float)age-.3f)/1.4f));
                    y=Mathf.Lerp(level.settings.LaneHeight(0)+1,GroundBaseY+.892f*scale,fall);
                }
                float x=(float)cue.Time*level.settings.moveSpeed;
                // Meet the route's departure point at the switch time, approaching from the right.
                if(cue.Kind==0)x+=(float)(cue.Time-session.PlayTime)*eagleFlightSpeed;
                r.transform.localPosition=level.spawnPoint.localPosition+new Vector3(x,y,-.3f);
                r.transform.localScale=Vector3.one*scale;
            }
        }
        // Run after Cinemachine and the panorama's UV update. Ground cues share the trees'
        // screen displacement; keep their vertical actions and the eagle's flight independent.
        public void SyncHorizontal() {
            if(trees==null || trees.targetCamera==null || presentationSession==null || level==null)return;
            float cameraX=trees.targetCamera.transform.position.x;
            for(int i=0;i<Schedule.Length;i++){
                var cue=Schedule[i];var r=visuals[i];
                if(cue.Kind==0 || !r.gameObject.activeSelf)continue;
                if(!anchored[i]){
                    // Project the encounter camera from the actual visible composition, so changing
                    // the parallax speed does not pull the reminder away from its intended encounter.
                    encounterCameraX[i]=cameraX+(float)(cue.Time-presentationSession.PlayTime)*level.settings.moveSpeed;
                    anchored[i]=true;
                }
                float routeX=level.spawnPoint.localPosition.x+(float)cue.Time*level.settings.moveSpeed;
                float worldX=transform.TransformPoint(new Vector3(routeX,0,0)).x;
                worldX+=(1-Mathf.Max(0,trees.parallaxSpeed))*(cameraX-encounterCameraX[i]);
                var position=r.transform.position;position.x=worldX;r.transform.position=position;
            }
        }
        void LateUpdate()=>SyncHorizontal();
        public void ResetPresentation(){
            presentationSession=null;Array.Clear(anchored,0,anchored.Length);
            foreach(var r in visuals)if(r!=null)r.gameObject.SetActive(false);
        }
        void OnDisable()=>ResetPresentation();
    }
}
