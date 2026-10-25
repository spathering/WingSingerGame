using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    [InitializeOnLoad]
    public static class UIViewportValidation {
        const string Key = "WingSinger.UIViewportValidation";
        const string Report = "Docs/Validation/UIViewportValidation.json";
        [Serializable] sealed class Result {
            public string date;
            public bool success;
            public int legacyOverflowCount, checkedControls, checkedText, pageResolutionCases;
            public string[] resolutions, checks;
            public string error;
        }
        static readonly Vector2Int[] sizes = {
            new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(2560,1440),
            new Vector2Int(1600,900), new Vector2Int(1920,1200), new Vector2Int(1024,768),
            new Vector2Int(2560,1080), new Vector2Int(800,600), new Vector2Int(640,360)
        };
        static readonly List<string> checks = new List<string>();
        static IEnumerator routine;
        static Result result;
        static Camera camera;
        static Canvas canvas;
        static RenderTexture target;
        static UIViewportValidation() { EditorApplication.update += Tick; }
        [MenuItem("WingSinger/UI/Validate Responsive Viewports")]
        public static void Begin() {
            UIIntegrationSetup.RequireSavedScenes();
            EditorSceneManager.OpenScene("Assets/Scenes/UI/UIFlowPreview.unity");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Stopping", false);
            SessionState.SetFloat(Key + "Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }
        static void Tick() {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + "Stopping", false)) {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath); return;
            }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Started", 0) > 180) { Finish(false, "Viewport validation timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try { if (routine == null) routine = Run(); if (!routine.MoveNext()) Finish(true, null); }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Check(bool value, string description) { if (!value) throw new Exception(description); checks.Add(description); }
        static IEnumerator Run() {
            checks.Clear(); result = new Result { date = DateTimeOffset.Now.ToString("O"), resolutions = sizes.Select(s => s.x + "x" + s.y).ToArray() };
            for (int i = 0; i < 4; i++) yield return null;
            var router = Object.FindObjectOfType<UIScreenRouter>(); canvas = router.GetComponent<Canvas>();
            Check(router.pages.Length == 13 && router.pages.All(p => p.GetComponent<UIPageViewport>() != null), "All 13 pages carry the responsive layout component");
            var scaler = canvas.GetComponent<CanvasScaler>();
            Check(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == new Vector2(1920,1080), "Canvas uses Scale With Screen Size and a 1920x1080 reference resolution");
            camera = new GameObject("Viewport Validation Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.orthographic = true;
            camera.transform.position = new Vector3(0,0,-10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 32000;
            SetResolution(new Vector2Int(1920,1080));
            scaler.referenceResolution = new Vector2(2420,1668);
            foreach (var page in router.pages) page.GetComponent<UIPageViewport>().enabled = false;
            foreach (var page in router.pages) {
                router.Show(page.screenId, false);
                var rt = page.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(.5f,.5f); rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                result.legacyOverflowCount += page.GetComponentsInChildren<Selectable>().Count(control => !Inside(control.GetComponent<RectTransform>(), 1920, 1080));
            }
            Check(result.legacyOverflowCount > 0, "Reproduced interactive controls outside the 16:9 viewport with the previous Canvas settings");
            scaler.referenceResolution = new Vector2(1920,1080);
            foreach (var page in router.pages) page.GetComponent<UIPageViewport>().enabled = true;
            foreach (var size in sizes) {
                SetResolution(size); yield return null; yield return null;
                foreach (var page in router.pages) {
                    router.Show(page.screenId, false); Canvas.ForceUpdateCanvases(); page.GetComponent<UIPageViewport>().Fit(); Canvas.ForceUpdateCanvases();
                    Check(Inside(page.GetComponent<RectTransform>(), size.x, size.y), "Entire content frame fits: " + size + " / " + page.screenId);
                    foreach (var control in page.GetComponentsInChildren<Selectable>()) {
                        Check(Inside(control.GetComponent<RectTransform>(), size.x, size.y), "Control fits: " + size + " / " + page.screenId + "/" + control.name);
                        result.checkedControls++;
                    }
                    foreach (var text in page.GetComponentsInChildren<TMP_Text>()) {
                        if (string.IsNullOrWhiteSpace(text.text)) continue;
                        text.ForceMeshUpdate();
                        Check(TextInside(text, size.x, size.y), "Rendered text fits: " + size + " / " + page.screenId + "/" + text.name);
                        result.checkedText++;
                    }
                    var background = page.transform.Find("Background").GetComponent<RectTransform>();
                    Check(Covers(background, size.x, size.y), "Background covers viewport: " + size + " / " + page.screenId);
                    result.pageResolutionCases++;
                    if (size == new Vector2Int(1280,720) || size == new Vector2Int(1024,768) && page.screenId == UIScreenId.Settings
                        || size == new Vector2Int(2560,1080) && page.screenId == UIScreenId.HomeDashboard) Capture(page.screenId, size);
                }
            }
            SetResolution(new Vector2Int(1280,720)); router.Show(UIScreenId.ReadyForTakeoff, false); Canvas.ForceUpdateCanvases();
            camera.Render(); yield return null;
            var start = router.Page(UIScreenId.ReadyForTakeoff).GetComponentsInChildren<Button>().Single(b => b.name == "StartFlight");
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, start.GetComponent<RectTransform>().TransformPoint(start.GetComponent<RectTransform>().rect.center)) };
            var hits = new List<RaycastResult>(); canvas.GetComponent<GraphicRaycaster>().Raycast(data, hits);
            Check(hits.Any(hit => hit.gameObject.GetComponentInParent<Button>() == start),
                "Scaled takeoff button remains correctly hit-testable (hits=" + hits.Count + ", depth=" + start.targetGraphic.depth + ")");
            var effect = start.GetComponent<UIInteractionTween>(); effect.OnPointerEnter(data);
            double until = EditorApplication.timeSinceStartup + .25;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(Inside(start.GetComponent<RectTransform>(),1280,720), "Hover animation stays inside the resized viewport");
            effect.OnPointerExit(data);
        }
        static void SetResolution(Vector2Int size) {
            if (target != null) { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); }
            target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32); target.Create();
            camera.targetTexture = target; camera.rect = new Rect(0,0,1,1); camera.aspect = (float)size.x / size.y;
            Canvas.ForceUpdateCanvases();
        }
        static Rect ScreenBounds(RectTransform rect) {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera,c)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        static bool Inside(RectTransform rect, int w, int h) => Inside(ScreenBounds(rect),w,h);
        static bool Inside(Rect bounds, int w, int h) => bounds.xMin >= -1 && bounds.yMin >= -1 && bounds.xMax <= w+1 && bounds.yMax <= h+1;
        static bool Covers(RectTransform rect, int w, int h) { var b = ScreenBounds(rect); return b.xMin <= 1 && b.yMin <= 1 && b.xMax >= w-1 && b.yMax >= h-1; }
        static bool TextInside(TMP_Text text, int w, int h) {
            var bounds = text.textBounds; Vector2 min = new Vector2(float.MaxValue,float.MaxValue), max = -min;
            for (int x=0;x<2;x++) for(int y=0;y<2;y++) {
                var local = new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,bounds.center.z);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(local)); min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            return Inside(Rect.MinMaxRect(min.x,min.y,max.x,max.y),w,h);
        }
        static void Capture(UIScreenId id, Vector2Int size) {
            const string directory = "Docs/Design/UI/ViewportPreviews"; Directory.CreateDirectory(directory);
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try { image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(directory+"/"+size.x+"x"+size.y+"_"+((int)id).ToString("00")+"_"+id+".png",image.EncodeToPNG()); }
            finally { RenderTexture.active=previous;Object.DestroyImmediate(image); }
        }
        static void Finish(bool success, string error) {
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release();Object.DestroyImmediate(target); target=null; }
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if(result==null)result=new Result();result.success=success;result.error=error;result.checks=checks.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,JsonUtility.ToJson(result,true));
            if(success)Debug.Log("WingSinger viewport validation passed: "+result.pageResolutionCases+" page/resolution cases, "+checks.Count+" checks.");else Debug.LogError(error);
            routine=null;SessionState.SetBool(Key+"Stopping",true);EditorApplication.isPlaying=false;
        }
    }
}
