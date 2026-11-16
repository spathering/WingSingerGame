using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    // Rebuilds native uGUI controls from the read-only Figma export, retaining the existing screen GUIDs.
    [InitializeOnLoad]
    public static class FigmaUISetup {
        const string Art = "Assets/Art/UI/Figma20261116/";
        const string Screens = "Assets/Prefabs/UI/Screens/";
        const string RootPath = "Assets/Prefabs/UI/UIFlowRoot.prefab";
        [Serializable] sealed class Layout { public Node[] screens; }
        [Serializable] sealed class Node {
            public string id, name, type, text, asset, align, vertical, fit;
            public float x, y, w, h, fontSize, radius, strokeWidth, rotation, opacity = 1;
            public bool hidden, blur, clip;
            public float[] color, stroke;
            public Node[] children;
        }
        static TMP_FontAsset font;
        static Layout layout;
        static readonly Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Vector2> absolute = new Dictionary<string, Vector2>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly string[] slideIds = { "102:79", "102:106", "102:133", "2:613", "2:634", "2:655", "2:675", "102:160", "102:187" };
        static FigmaUISetup() { EditorApplication.update += Poll; }
        static void Poll() {
            const string request = "Temp/WingSingerFigma.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(request).Trim(); File.Delete(request);
            try {
                if (action == "apply") Apply();
                else if (action == "refine") ApplyRefinements();
                else if (action == "visual-style") FigmaVisualStyle.ApplySavedAssets();
                else if (action == "validate") FigmaUIValidation.Begin();
                else if (action == "state") { }
                else if (action == "diagnose") {
                    var all=Object.FindObjectsOfType<Graphic>();
                    File.WriteAllLines("Temp/FigmaGraphics.txt",all.Select(g=>g.name+" | "+PathOf(g.transform)+" | "+g.GetType().Name+" | "+g.transform.position+" | "+(g is Image image&&image.sprite!=null?AssetDatabase.GetAssetPath(image.sprite):"")));
                }
                else throw new InvalidOperationException("Unknown Figma request: " + action);
                File.WriteAllText("Temp/WingSingerFigma.response.txt", "SUCCESS " + action);
            } catch (Exception e) { File.WriteAllText("Temp/WingSingerFigma.response.txt", e.ToString()); Debug.LogException(e); }
        }
        static string PathOf(Transform t) => t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static void ApplyRefinements() {
            UIIntegrationSetup.RequireSavedScenes();
            layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Art+"Layout.json"));
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/DarumadropOne SDF.asset");
            sprites.Clear();foreach(string file in Directory.GetFiles(Art,"*.png"))sprites[Path.GetFileName(file)]=AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\','/'));
            Directory.CreateDirectory("Temp/FigmaRefinementBackup");
            foreach(string path in new[]{Screens+"03_LevelSelect.prefab",Screens+"12_PracticeFollow.prefab",GameplayNavigationSetup.ScenePath}) {
                string backup="Temp/FigmaRefinementBackup/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);
            }
            SaveScreen("2:164",UIScreenId.LevelSelect,"03_LevelSelect");
            SaveScreen("117:125",UIScreenId.PracticeFollow,"12_PracticeFollow");
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
            var flow=Object.FindObjectOfType<BreathUIFlowController>();
            flow.router.pages=flow.router.GetComponentsInChildren<UIPageView>(true);ConfigureBindings(flow);
            foreach(var component in new Component[]{flow.bindings,flow.router}) {EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            flow.router.SetSelectedLevel(1);flow.router.Initialize();Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("WingSinger: updated selection-only cards, uniform locks and hardware-only breath meter.");
        }
        [MenuItem("WingSinger/UI/Apply November 16 Figma UI")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            Directory.CreateDirectory("Temp/FigmaBackup");
            foreach (string path in Directory.GetFiles(Screens, "*.prefab").Concat(new[] { RootPath, GameplayNavigationSetup.ScenePath })) {
                string backup = "Temp/FigmaBackup/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Art + "Layout.json"));
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/DarumadropOne SDF.asset");
            if (font == null) throw new InvalidOperationException("Darumadrop One font is missing.");
            AssetDatabase.ImportAsset("Assets/Settings/Levels/Level_001_FirstFlight_Definition.asset");
            ImportSprites();
            SaveScreen("2:35", UIScreenId.HomeDashboard, "01_HomeDashboard");
            SaveScreen("2:117", UIScreenId.HomeClean, "02_HomeClean");
            SaveScreen("2:164", UIScreenId.LevelSelect, "03_LevelSelect");
            SaveScreen("2:282", UIScreenId.ReadyForTakeoff, "04_ReadyForTakeoff");
            SaveScreen("2:338", UIScreenId.FlightPaused, "05_FlightPaused");
            SaveScreen("2:358", UIScreenId.FlightComplete, "06_FlightComplete");
            SaveScreen("2:417", UIScreenId.Settings, "07_Settings");
            SaveScreen("2:484", UIScreenId.ControllerConnection, "08_ControllerConnection");
            SaveScreen("2:521", UIScreenId.FlightLog, "09_FlightLog");
            SaveScreen("2:613", UIScreenId.PracticeInhale, "10_PracticeInhale");
            SaveScreen("2:634", UIScreenId.PracticeExhale, "11_PracticeExhale");
            SaveScreen("117:125", UIScreenId.PracticeFollow, "12_PracticeFollow");
            SaveScreen("2:675", UIScreenId.PracticeComplete, "13_PracticeComplete");
            SaveTutorial();
            var prefab = PrefabUtility.LoadPrefabContents(RootPath);
            try {
                foreach (var page in prefab.GetComponentsInChildren<UIPageView>(true)) Object.DestroyImmediate(page.gameObject);
                foreach (string path in Directory.GetFiles(Screens, "*.prefab").OrderBy(p => p)) {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/')), prefab.transform);
                    instance.SetActive(false);
                }
                var router = prefab.GetComponent<UIScreenRouter>();
                prefab.layer=5;
                router.pages = prefab.GetComponentsInChildren<UIPageView>(true);
                router.initialScreen = UIScreenId.HomeClean; router.referencePreviewMode = false; router.enablePreviewShortcuts = false;
                prefab.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(prefab, RootPath);
            } finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
            var flow = Object.FindObjectOfType<BreathUIFlowController>();
            if (flow == null) throw new InvalidOperationException("The main scene has no production UI flow.");
            var routerScene = flow.router;
            flow.gameObject.layer=5;flow.transform.localScale=Vector3.one;
            // Earlier calendar checks were scene additions to removed page instances. Unity
            // reparents those orphaned decorations to the canvas when the page is replaced.
            foreach(var child in flow.transform.Cast<Transform>().Where(t=>t.name=="Check" && t.GetComponent<Image>()!=null && AssetDatabase.GetAssetPath(t.GetComponent<Image>().sprite)=="Assets/Art/UI/Sprites/IconCheckWhite.png").ToArray())
                Object.DestroyImmediate(child.gameObject);
            routerScene.pages = routerScene.GetComponentsInChildren<UIPageView>(true);
            ConfigureBindings(flow);
            foreach (var page in routerScene.pages) page.usesReferencePreviewData = false;
            // Grow the trees while preserving their exact world-space bottom and parallax phase.
            var trees = Object.FindObjectsOfType<LoopingPanorama>().Single(p => p.name == "Looping Foreground");
            trees.Sync(); float bottom = trees.transform.position.y - trees.transform.localScale.y * .5f;
            trees.sizeMultiplier = 2.7f;
            float reference = trees.referenceOrthographicSize * 2 + .02f;
            float height = reference * trees.heightFraction * trees.sizeMultiplier;
            trees.worldOriginY = bottom - (reference-height)*trees.verticalAnchor*.5f - height*trees.verticalOffsetFraction + height*trees.verticalOverscan*.5f;
            trees.Sync();
            foreach (var item in new Component[] { flow, flow.bindings, routerScene, trees, flow.transform }) {
                EditorUtility.SetDirty(item); PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            EditorUtility.SetDirty(flow.gameObject);PrefabUtility.RecordPrefabInstancePropertyModifications(flow.gameObject);
            routerScene.SetSelectedLevel(1); routerScene.Initialize();Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("WingSinger Figma UI installed: native menus, nine tutorial slides, first-level-only selection, larger grounded trees.");
        }
        static void ImportSprites() {
            sprites.Clear();
            foreach (string file in Directory.GetFiles(Art, "*.png")) {
                string path = file.Replace('\\','/'); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.maxTextureSize != 2048) {
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                sprites[Path.GetFileName(file)] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        static Color Col(float[] value, Color fallback) => value != null && value.Length >= 4 ? new Color(value[0],value[1],value[2],value[3]) : fallback;
        static GameObject Rect(string name, Transform parent, float x, float y, float w, float h) {
            var go = new GameObject(name, typeof(RectTransform)); go.layer=5; go.SetActive(false); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1);
            r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); return go;
        }
        static GameObject Build(Node n, Transform parent, Vector2 origin) {
            var go = Rect(n.name.Replace('/','∕'), parent, n.x,n.y,n.w,n.h);
            go.AddComponent<FigmaNodeTag>().nodeId = n.id; nodes[n.id] = go; absolute[n.id] = origin + new Vector2(n.x,n.y);
            if (!string.IsNullOrEmpty(n.asset)) {
                var image = go.AddComponent<Image>(); image.sprite = sprites[n.asset]; image.preserveAspect = n.fit == "contain";
                image.raycastTarget = false;if(n.type=="ELLIPSE")image.preserveAspect=true;
                if (n.fit == "cover" && !n.name.Contains("Background") && !n.name.Contains("Foreground")) {
                    var child = Rect("Cropped artwork", go.transform,0,0,n.w,n.h); var draw = child.AddComponent<Image>();
                    draw.sprite = image.sprite; draw.raycastTarget = false;
                    float factor = Mathf.Max(n.w/image.sprite.rect.width,n.h/image.sprite.rect.height);
                    var rt = child.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f,.5f); rt.anchoredPosition=Vector2.zero;
                    rt.sizeDelta = image.sprite.rect.size * factor; image.color = Color.clear;
                    if(n.radius>0) {
                        var clip=Rect("Rounded thumbnail mask",go.transform,0,0,n.w,n.h);
                        var maskShape=clip.AddComponent<FigmaSurface>();maskShape.radius=n.radius;maskShape.color=Color.white;maskShape.raycastTarget=false;
                        clip.AddComponent<Mask>().showMaskGraphic=false;child.transform.SetParent(clip.transform,false);clip.SetActive(true);
                        var outline=Rect("Thumbnail border",go.transform,0,0,n.w,n.h).AddComponent<FigmaSurface>();outline.radius=n.radius;outline.color=Color.clear;outline.borderWidth=n.strokeWidth;outline.borderColor=Col(n.stroke,Color.clear);outline.raycastTarget=false;outline.gameObject.SetActive(true);
                    } else go.AddComponent<RectMask2D>();
                    child.SetActive(true);
                }
            } else if (n.type == "TEXT") {
                var text = go.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = n.text ?? "";
                text.fontSize = n.fontSize; text.color = Col(n.color, Color.black); text.raycastTarget = false;
                text.alignment = n.align == "CENTER" ? TextAlignmentOptions.Center : n.align == "RIGHT" ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                text.verticalAlignment = VerticalAlignmentOptions.Middle; text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow; text.margin = Vector4.zero;
                if (n.stroke != null && n.strokeWidth > 0) { text.outlineColor = Col(n.stroke,Color.white); text.outlineWidth = .12f; }
            } else if (n.color != null || n.stroke != null) {
                var surface = go.AddComponent<FigmaSurface>(); surface.color = Col(n.color,Color.clear); surface.radius = n.radius;
                surface.borderWidth = n.strokeWidth; surface.borderColor = Col(n.stroke,Color.clear); surface.raycastTarget = false;
                if (n.blur) {
                    surface.color = Color.white; surface.borderWidth = 0;
                    surface.backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "ForestBackdropBlur.png");
                    var p = absolute[n.id]; surface.backdropUV = new Rect(p.x/2420,1-(p.y+n.h)/1668,n.w/2420,n.h/1668);
                    var overlay = Rect("Editable glass tint and border",go.transform,0,0,n.w,n.h).AddComponent<FigmaSurface>();
                    overlay.color = Col(n.color,Color.clear); overlay.radius=n.radius; overlay.borderWidth=n.strokeWidth;
                    overlay.borderColor=Col(n.stroke,new Color(.92f,.84f,.71f)); overlay.raycastTarget=false; overlay.gameObject.SetActive(true);
                }
            }
            if (n.opacity < 1) go.AddComponent<CanvasGroup>().alpha = n.opacity;
            if (n.children != null) foreach (var c in n.children) Build(c,go.transform,absolute[n.id]);
            if (Mathf.Abs(n.rotation) > .01f) go.transform.localRotation = Quaternion.Euler(0,0,n.rotation);
            go.SetActive(!n.hidden && parent!=null); return go;
        }
        static GameObject BuildFrame(string id, Transform parent = null) {
            nodes.Clear(); absolute.Clear(); var n = layout.screens.Single(s => s.id == id);
            float x=n.x,y=n.y; n.x=n.y=0; var go=Build(n,parent,Vector2.zero); n.x=x; n.y=y; go.SetActive(false);
            return go;
        }
        static void SaveScreen(string id, UIScreenId screen, string name) {
            var root = BuildFrame(id); root.name=name;
            try {
                var page = root.AddComponent<UIPageView>(); page.screenId=screen; page.usesReferencePreviewData=false; page.selectedLevelLabels=Array.Empty<TMP_Text>();
                var viewport = root.AddComponent<UIPageViewport>();
                var bg = root.transform.Cast<Transform>().First(t => t.name.Contains("Background")); bg.name="Background"; viewport.background=(RectTransform)bg;
                Configure(screen,root);
                FigmaVisualStyle.Apply(root);
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root,Screens+name+".prefab");
            } finally { Object.DestroyImmediate(root); }
        }
        static GameObject N(string id) => nodes[id];
        static T C<T>(string id) where T : Component => N(id).GetComponent<T>();
        static Button Button(GameObject go) {
            var graphic = go.GetComponent<Graphic>();
            if (graphic == null) { var image=go.AddComponent<Image>(); image.color=Color.clear; graphic=image; }
            graphic.raycastTarget=true;
            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>(); button.targetGraphic=graphic;
            button.transition=Selectable.Transition.ColorTint;
            var colors=button.colors; colors.highlightedColor=new Color(1,1,1,.92f); colors.pressedColor=new Color(.85f,.9f,.85f); colors.disabledColor=new Color(.58f,.58f,.58f,.55f); button.colors=colors;
            if (go.GetComponent<UIInteractionTween>()==null) go.AddComponent<UIInteractionTween>();
            return button;
        }
        static void Route(string id, UIRouteAction action, UIScreenId dest=UIScreenId.HomeDashboard, int level=0) {
            var go=N(id); Button(go); var route=go.AddComponent<UIRouteButton>(); route.action=action; route.destination=dest; route.levelNumber=level;
        }
        static void Bind(string id, UIValueKind value, int index=0) {
            var label=N(id).AddComponent<UIRuntimeLabel>(); label.value=value; label.index=index;
            var text=C<TMP_Text>(id); text.enableAutoSizing=true; text.fontSizeMax=text.fontSize; text.fontSizeMin=Mathf.Min(text.fontSize,22);
            text.overflowMode=TextOverflowModes.Truncate;
            if(value==UIValueKind.SensorReading)N(id).AddComponent<HardwareStatusFont>().fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/HardwareStatus SDF.asset");
        }
        static void Progress(string parent,string track,string fill,bool daily) {
            var progress=N(parent).AddComponent<UIRuntimeProgress>(); progress.track=C<RectTransform>(track); progress.fill=C<RectTransform>(fill); progress.dailyGoal=daily;
        }
        static void Hit(Transform parent, string name, float x,float y,float w,float h, UIScreenId dest) {
            var go=Rect(name,parent,x,y,w,h); var button=Button(go); var route=go.AddComponent<UIRouteButton>(); route.action=UIRouteAction.Navigate; route.destination=dest; go.SetActive(true);
        }
        static void Configure(UIScreenId screen,GameObject root) {
            foreach (var pair in nodes.Where(p => p.Value.name.StartsWith("Navigation ∕ ")).ToArray()) {
                string name=pair.Value.name;
                if (name.Contains("Flight Log")) Route(pair.Key,UIRouteAction.Navigate,UIScreenId.FlightLog);
                else if (name.Contains("Settings")) Route(pair.Key,UIRouteAction.Navigate,UIScreenId.Settings);
                else if (name.Contains("Back")) Route(pair.Key,UIRouteAction.Back);
                else if (name.Contains("Levels")) Route(pair.Key,UIRouteAction.Navigate,UIScreenId.LevelSelect);
                else Route(pair.Key,UIRouteAction.Navigate,UIScreenId.HomeClean);
            }
            switch(screen) {
                case UIScreenId.HomeDashboard:
                case UIScreenId.HomeClean:
                    foreach (var p in nodes.Where(p=>p.Value.name.Contains("Button ∕ VIEW LEVELS")).ToArray()) Route(p.Key,UIRouteAction.Navigate,UIScreenId.LevelSelect);
                    foreach(var p in nodes.Where(p=>p.Value.name.StartsWith("Text ∕ 12 levels")).ToArray()) Bind(p.Key,UIValueKind.WorldLevels);
                    if(screen==UIScreenId.HomeDashboard) {
                        Bind("2:90",UIValueKind.InhaleSeconds);Bind("2:96",UIValueKind.ExhaleSeconds);Bind("2:102",UIValueKind.TodayPractice);Bind("2:106",UIValueKind.Streak);Bind("2:111",UIValueKind.Connection);
                    }
                    break;
                case UIScreenId.LevelSelect:
                    Route("330:230",UIRouteAction.ContinueSelection);Bind("2:279",UIValueKind.LevelTitle);Bind("2:280",UIValueKind.LevelDescription);
                    var cards=nodes.Where(p=>p.Value.name.StartsWith("Level ") && p.Value.name.Length>6 && char.IsDigit(p.Value.name[6]) && p.Value.name.Contains(" ∕ ")).Select(p=>p.Key).ToArray();
                    foreach(string key in cards) {
                        var card=N(key); int number=int.Parse(card.name.Split(' ')[1]); Route(key,UIRouteAction.SelectLevel,level:number);
                        var thumbnail=card.GetComponentsInChildren<FigmaNodeTag>(true).First(t=>t.name.StartsWith("Forest thumbnail")); thumbnail.name="Thumbnail";
                        foreach(var tag in card.GetComponentsInChildren<FigmaNodeTag>(true).Where(t=>t.name.Contains("Completion circle")||t.name.Contains("Next flight status"))) tag.gameObject.SetActive(false);
                        var status=card.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name.StartsWith("Icon ∕ "));
                        status.name="Status"; status.sprite=sprites[number==1?"87635.svg.png":"5a321.svg.png"];status.preserveAspect=true;
                        foreach(var border in thumbnail.GetComponentsInChildren<FigmaSurface>(true).Where(s=>s.name=="Thumbnail border")) {
                            border.borderColor=number==1?new Color(.91f,.57f,.16f):new Color(.65f,.66f,.59f);border.borderWidth=number==1?5:3.2f;
                        }
                        if(number>1) {
                            var fade=thumbnail.GetComponent<CanvasGroup>();if(fade==null)fade=thumbnail.gameObject.AddComponent<CanvasGroup>();fade.alpha=.28f;
                            var icon=status.rectTransform;icon.anchorMin=icon.anchorMax=icon.pivot=new Vector2(0,1);icon.anchoredPosition=new Vector2(310.0199f,-158.5762f);icon.sizeDelta=new Vector2(48.07947f,48.07947f);
                            foreach(var image in thumbnail.GetComponentsInChildren<Image>(true)) if(image.color.a>0) image.color=new Color(.5f,.5f,.5f,.65f);
                            foreach(var t in card.GetComponentsInChildren<TMP_Text>(true)) t.color=new Color(.28f,.33f,.24f);
                            var surface=card.GetComponent<FigmaSurface>();if(surface!=null)surface.borderColor=new Color(.65f,.66f,.59f);
                        }
                    }
                    break;
                case UIScreenId.ReadyForTakeoff:
                    Route("330:232",UIRouteAction.TryRhythm); Route("330:234",UIRouteAction.StartFlight);
                    Bind("2:297",UIValueKind.LevelTitle);Bind("2:302",UIValueKind.InputReadiness);Bind("2:318",UIValueKind.InhaleSeconds);Bind("2:324",UIValueKind.ExhaleSeconds);Bind("2:330",UIValueKind.TodayPractice);
                    var preview=C<RectTransform>("2:331");preview.pivot=new Vector2(.5f,.5f);preview.anchoredPosition+=new Vector2(preview.sizeDelta.x*.5f,-preview.sizeDelta.y*.5f);preview.localScale=new Vector3(1,-1,1);
                    break;
                case UIScreenId.FlightPaused:
                    Route("330:236",UIRouteAction.Resume);Route("330:238",UIRouteAction.Restart);Route("330:240",UIRouteAction.Navigate,UIScreenId.LevelSelect);
                    C<TMP_Text>("330:237").text="RESUME  ›";
                    Route("2:355",UIRouteAction.Navigate,UIScreenId.Settings);Hit(N("2:341").transform,"Settings hit area",410,890,440,90,UIScreenId.Settings);
                    Bind("2:346",UIValueKind.LevelTitle);Bind("2:349",UIValueKind.ResultCycles);Progress("2:345","2:347","2:348",false);
                    break;
                case UIScreenId.FlightComplete:
                    Route("330:242",UIRouteAction.NextLevel);Route("330:244",UIRouteAction.Navigate,UIScreenId.LevelSelect);Route("2:416",UIRouteAction.Restart);
                    Bind("2:384",UIValueKind.LevelTitle);Bind("2:390",UIValueKind.ResultSeconds);Bind("2:396",UIValueKind.ResultCycles);Bind("2:402",UIValueKind.ResultRhythm);Bind("2:407",UIValueKind.TodaySessions);
                    Progress("2:403","2:408","2:409",true);break;
                case UIScreenId.Settings:
                    Slider("Music","2:448","2:449","2:450","2:451","2:452");Slider("Sound","2:454","2:455","2:456","2:457","2:458");
                    Toggle("ReduceMotion","2:460","2:461","2:462");Toggle("HighContrast","2:464","2:465","2:466");
                    var panel=N("2:426").transform;
                    var row=C<RectTransform>("2:467"); float supportY=-row.anchoredPosition.y-22;
                    Hit(panel,"Controller",100,supportY,1350,99,UIScreenId.ControllerConnection);
                    Hit(panel,"Hardware settings",100,-C<RectTransform>("2:473").anchoredPosition.y-22,1350,99,UIScreenId.HardwareSettings);
                    Hit(panel,"How to play",100,-C<RectTransform>("2:479").anchoredPosition.y-22,1350,99,UIScreenId.Tutorial);
                    C<TMP_Text>("2:475").text="Hardware settings";C<TMP_Text>("2:476").text="BLE / USB";
                    Bind("2:470",UIValueKind.Connection);break;
                case UIScreenId.ControllerConnection:
                    Route("330:248",UIRouteAction.CheckConnection);Route("330:250",UIRouteAction.Back);
                    Bind("2:510",UIValueKind.Connection);Bind("2:514",UIValueKind.Battery);Bind("2:518",UIValueKind.Signal);
                    C<TMP_Text>("2:507").text="WingSinger Breath Controller";break;
                case UIScreenId.FlightLog:
                    Bind("2:540",UIValueKind.StreakDays);Bind("2:546",UIValueKind.WeeklyFlights);Bind("2:552",UIValueKind.WeeklyMinutes);
                    for(int i=0;i<3;i++) {
                        string rowId="2:"+(578+i*10);Route(rowId,UIRouteAction.ViewRecord,level:i);Bind("2:"+(584+i*10),UIValueKind.RecentTitle,i);Bind("2:"+(585+i*10),UIValueKind.RecentDetail,i);
                        var parent=N(rowId).transform; var p=absolute[rowId];
                        foreach(int offset in new[]{1,6,7,8}) {string key="2:"+(578+i*10+offset);if(!nodes.ContainsKey(key))continue;var child=N(key);child.transform.SetParent(parent,false);C<RectTransform>(key).anchoredPosition=new Vector2(absolute[key].x-p.x,-(absolute[key].y-p.y));}
                    }
                    Route("330:246",UIRouteAction.Navigate,UIScreenId.HomeClean);
                    for(int i=0;i<7;i++) {
                        int node=559+i*4; if(i>=3) node=571+(i-3)*2;
                        string day="2:"+node;
                        C<Image>(day).sprite=sprites["ae7e6.svg.png"];
                        if(i>=3) {var check=Rect("Check",N(day).transform,20,20,48,48);var image=check.AddComponent<Image>();image.sprite=sprites["fcf89.svg.png"];image.raycastTarget=false;check.SetActive(true);}
                    }
                    break;
                case UIScreenId.PracticeInhale:
                case UIScreenId.PracticeExhale:
                    SetupPractice(root,screen);break;
                case UIScreenId.PracticeFollow: SetupHud(root);break;
                case UIScreenId.PracticeComplete:
                    Route("330:252",UIRouteAction.Navigate,UIScreenId.LevelSelect);Bind("2:689",UIValueKind.Collected);Bind("396:302",UIValueKind.Collected);
                    Route("396:291",UIRouteAction.Navigate,UIScreenId.HomeClean);Bind("396:299",UIValueKind.Connection);break;
            }
        }
        static void Slider(string name,string trackId,string fillId,string handleId,string centerId,string labelId) {
            var track=C<RectTransform>(trackId);var fill=C<RectTransform>(fillId);var handle=C<RectTransform>(handleId);
            var root=Rect(name,track.parent,track.anchoredPosition.x,-track.anchoredPosition.y-30,track.sizeDelta.x,85);
            var area=(RectTransform)root.transform; Vector2 original=track.anchoredPosition;
            foreach(string id in new[]{trackId,fillId,handleId,centerId}) {
                var rect=C<RectTransform>(id);Vector2 delta=rect.anchoredPosition-original;
                rect.SetParent(root.transform,false);rect.anchoredPosition=new Vector2(delta.x,delta.y-30);
            }
            track.anchorMin=track.anchorMax=track.pivot=new Vector2(0,.5f);track.anchoredPosition=Vector2.zero;
            fill.anchorMin=new Vector2(0,.5f);fill.anchorMax=new Vector2(.6f,.5f);fill.pivot=new Vector2(0,.5f);fill.anchoredPosition=Vector2.zero;fill.sizeDelta=new Vector2(0,track.sizeDelta.y);
            handle.anchorMin=handle.anchorMax=handle.pivot=new Vector2(.5f,.5f);handle.anchoredPosition=Vector2.zero;
            var center=C<RectTransform>(centerId);center.SetParent(handle,false);center.anchorMin=center.anchorMax=center.pivot=new Vector2(.5f,.5f);center.anchoredPosition=Vector2.zero;
            var fillArea=Rect("Fill area",root.transform,0,30,track.sizeDelta.x,track.sizeDelta.y).GetComponent<RectTransform>();
            fill.SetParent(fillArea,false);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.anchoredPosition=Vector2.zero;fill.sizeDelta=Vector2.zero;
            var handleArea=Rect("Handle area",root.transform,0,30+(track.sizeDelta.y-handle.sizeDelta.y)*.5f,track.sizeDelta.x,handle.sizeDelta.y).GetComponent<RectTransform>();
            handle.SetParent(handleArea,false);handle.anchoredPosition=Vector2.zero;handle.sizeDelta=new Vector2(handle.sizeDelta.x,0);
            fillArea.gameObject.SetActive(true);handleArea.gameObject.SetActive(true);
            var slider=root.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Graphic>();slider.minValue=0;slider.maxValue=1;slider.value=.6f;
            // The full track is clickable; decorative art never consumes pointer events.
            track.GetComponent<Graphic>().raycastTarget=true;handle.GetComponent<Graphic>().raycastTarget=true;
            var value=root.AddComponent<UISliderValue>();value.valueLabel=C<TMP_Text>(labelId);root.SetActive(true);
        }
        static void Toggle(string name,string trackId,string handleId,string labelId) {
            var track=C<RectTransform>(trackId);var handle=C<RectTransform>(handleId);var toggle=N(trackId).AddComponent<Toggle>();N(trackId).name=name;
            var graphic=N(trackId).GetComponent<Graphic>();graphic.raycastTarget=true;toggle.targetGraphic=graphic;toggle.transition=Selectable.Transition.None;
            handle.SetParent(track,false);handle.anchorMin=handle.anchorMax=handle.pivot=new Vector2(.5f,.5f);handle.anchoredPosition=Vector2.zero;
            var value=N(trackId).AddComponent<UIToggleValue>();value.handle=handle;value.track=graphic;value.valueLabel=C<TMP_Text>(labelId);value.handleTravel=(track.sizeDelta.x-handle.sizeDelta.x)*.5f-8;
        }
        static void SetupPractice(GameObject root,UIScreenId screen) {
            bool inhale=screen==UIScreenId.PracticeInhale; string pause=inhale?"396:252":"396:265",device=inhale?"396:260":"396:273",score=inhale?"396:263":"396:276";
            Route(pause,UIRouteAction.Pause);Bind(device,UIValueKind.SensorReading);Bind(score,UIValueKind.Collected);
            Bind(inhale?"2:632":"2:653",UIValueKind.InstructionTitle);Bind(inhale?"2:633":"2:654",UIValueKind.InstructionDetail);
            foreach(var child in root.transform.Cast<Transform>().ToArray()) if(child.name.StartsWith("02 World")||child.name.StartsWith("03 Foreground")||child.name.StartsWith("04 Guidance")) child.gameObject.SetActive(false);
        }
        static void SetupHud(GameObject root) {
            foreach(string id in new[]{"117:134","117:136","117:137","117:138","117:139","117:140","372:215","372:216"}) N(id).SetActive(false);
            Route("117:141",UIRouteAction.Pause);Bind("117:147",UIValueKind.LevelTitle);Bind("117:160",UIValueKind.Collected);
            var hud=root.AddComponent<FigmaGameplayHud>();hud.cue=C<TMP_Text>("117:156");hud.title=C<TMP_Text>("117:174");hud.detail=C<TMP_Text>("117:175");hud.feedback=C<TMP_Text>("117:179");hud.meterStatus=C<TMP_Text>("117:171");hud.meter=C<FigmaSurface>("117:169");
            hud.cue.enableAutoSizing=true;hud.cue.fontSizeMax=hud.cue.fontSize;hud.cue.fontSizeMin=24;hud.cue.enableWordWrapping=false;
            // Anchor the airflow fill to the bottom so changes grow upwards.
            var meter=hud.meter.rectTransform;meter.anchoredPosition+=new Vector2(0,-meter.sizeDelta.y);meter.pivot=new Vector2(0,0);
            hud.meterGroup=N("117:165").AddComponent<CanvasGroup>();hud.meterGroup.alpha=.45f;
            hud.meterTrack=C<RectTransform>("117:168");hud.meterMarker=C<RectTransform>("117:170");
            C<TMP_Text>("117:167").text="BREATH";C<TMP_Text>("117:171").text="NO SENSOR";
            C<TMP_Text>("117:171").enableAutoSizing=true;C<TMP_Text>("117:171").fontSizeMin=22;C<TMP_Text>("117:171").fontSizeMax=32;
            var panel=C<RectTransform>("117:166");panel.sizeDelta=new Vector2(panel.sizeDelta.x,740);
            hud.meterValue=MeterText(N("117:165").transform,"Sensor value",635,40,26,"-- mV");
            hud.meterReference=MeterText(N("117:165").transform,"Target deviation",682,36,23,"Target: 100 mV");
            meter.sizeDelta=new Vector2(meter.sizeDelta.x,0);
            hud.progress=Enumerable.Range(148,6).Select(i=>C<FigmaSurface>("117:"+i)).ToArray();hud.sound=Button(N("117:161"));
        }
        static TMP_Text MeterText(Transform parent,string name,float y,float h,float size,string value) {
            var go=Rect(name,parent,5,y,155,h);var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.fontSizeMax=size;text.fontSizeMin=16;text.enableAutoSizing=true;text.enableWordWrapping=false;text.color=new Color(.027f,.243f,.216f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;go.SetActive(true);return text;
        }
        static void SaveTutorial() {
            var root=Rect("15_Tutorial",null,0,0,2420,1668);
            try {
                root.AddComponent<UIPageView>().screenId=UIScreenId.Tutorial;
                root.GetComponent<UIPageView>().usesReferencePreviewData=false;
                var tutorial=root.AddComponent<FigmaTutorialController>();var slides=new List<GameObject>();
                var viewport=root.AddComponent<UIPageViewport>();
                for(int i=0;i<slideIds.Length;i++) {
                    var slide=BuildFrame(slideIds[i],root.transform);slides.Add(slide);
                    var bg=slide.transform.Cast<Transform>().First(t=>t.name.Contains("Background"));
                    if(i==0) { bg.SetParent(root.transform,false); bg.name="Background";bg.SetAsFirstSibling();viewport.background=(RectTransform)bg; } else bg.gameObject.SetActive(false);
                    foreach(var pair in nodes.Where(p=>p.Value.name.Contains("choose level button")||p.Value.name.Contains("choose a level")).ToArray()) {
                        if(i==slideIds.Length-1) tutorial.chooseLevel=Button(pair.Value);else pair.Value.SetActive(false);
                    }
                    foreach(var tag in slide.GetComponentsInChildren<FigmaNodeTag>(true)) {
                        if(tag.name=="Pause surface") Route(tag.nodeId,UIRouteAction.Back);
                        if(tag.name=="Device status") Bind(tag.nodeId,UIValueKind.Connection);
                        if(tag.name=="Step") C<TMP_Text>(tag.nodeId).text=(i+1)+" / 9";
                        if(tag.name=="Dialogue ∕ editable tap prompt") C<TMP_Text>(tag.nodeId).text=i==8?"":"Tap to continue";
                    }
                    // Keep tutorial examples clearly instructional; no gameplay profile is written.
                    slide.SetActive(i==0);
                }
                tutorial.slides=slides.ToArray();
                var tap=Rect("Tap to continue",root.transform,0,180,2420,1488);tutorial.next=Button(tap);tap.SetActive(true);
                foreach(var slide in slides)slide.SetActive(false);slides[0].SetActive(true);
                FigmaVisualStyle.Apply(root);
                root.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,Screens+"15_Tutorial.prefab");
            } finally { Object.DestroyImmediate(root); }
        }
        static void ConfigureBindings(BreathUIFlowController flow) {
            var bindings=flow.bindings;bindings.useFigmaArtwork=true;
            var router=flow.router;
            var settings=router.pages.Single(p=>p.screenId==UIScreenId.Settings);
            bindings.music=settings.GetComponentsInChildren<Slider>(true).Single(s=>s.name=="Music");bindings.sound=settings.GetComponentsInChildren<Slider>(true).Single(s=>s.name=="Sound");
            bindings.reduceMotion=settings.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="ReduceMotion");bindings.highContrast=settings.GetComponentsInChildren<Toggle>(true).Single(t=>t.name=="HighContrast");
            var levels=router.pages.Single(p=>p.screenId==UIScreenId.LevelSelect);
            bindings.levelCards=levels.GetComponentsInChildren<UIRouteButton>(true).Where(r=>r.action==UIRouteAction.SelectLevel).OrderBy(r=>r.levelNumber).Select(r=>r.GetComponent<Button>()).ToArray();
            bindings.levelStatus=bindings.levelCards.Select(c=>c.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Status")).ToArray();
            bindings.completedIcon=sprites["e5690.svg.png"];bindings.unlockedIcon=sprites["87635.svg.png"];bindings.lockedIcon=sprites["5a321.svg.png"];
            var log=router.pages.Single(p=>p.screenId==UIScreenId.FlightLog);
            bindings.recentRows=log.GetComponentsInChildren<UIRouteButton>(true).Where(r=>r.action==UIRouteAction.ViewRecord).OrderBy(r=>r.levelNumber).Select(r=>r.GetComponent<Button>()).ToArray();
            var tags=log.GetComponentsInChildren<FigmaNodeTag>(true).ToDictionary(t=>t.nodeId,t=>t.gameObject);
            bindings.weekTiles=new Image[7];bindings.weekChecks=new GameObject[7];
            for(int i=0;i<7;i++) { int n=i<3?559+4*i:571+2*(i-3);bindings.weekTiles[i]=tags["2:"+n].GetComponent<Image>();bindings.weekChecks[i]=i<3?tags["2:"+(n+1)]:tags["2:"+n].transform.Find("Check").gameObject; }
        }
    }
}
