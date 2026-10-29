using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    /// <summary>Builds editable uGUI from the documented reference slices, not full-screen screenshot quads.</summary>
    public static class ReferenceUIBuilder {
        const string Art = "Assets/Art/UI/";
        const string Screens = "Assets/Prefabs/UI/Screens/";
        const string Shared = "Assets/Prefabs/UI/Shared/";
        const string FlowPrefab = "Assets/Prefabs/UI/UIFlowRoot.prefab";
        const string PreviewScene = "Assets/Scenes/UI/UIFlowPreview.unity";
        const string PreviewDir = "Docs/Design/UI/Previews/";
        const string Report = "Docs/Validation/UIBuildValidation.json";
        const float SX = 2420f / 1902, SY = 1668f / 1312;
        const float TypeScale = 1.25f;
        static readonly Color Ink = Hex("004b42"), Muted = Hex("7f8479"), Orange = Hex("ff8500"), Green = Hex("438b3d");
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly List<string> checks = new List<string>();
        static TMP_FontAsset font;
        static bool sharedReady;

        [Serializable] class AssetEntry { public string key, path; public int width, height, border; }
        [Serializable] class AssetManifest { public AssetEntry[] assets; }
        [Serializable] class BuildReport {
            public string unityVersion, date, previewScene, flowPrefab;
            public int screenCount, spriteCount, sharedPrefabCount;
            public string[] prefabPaths, passedChecks;
            public string validationScope = "Actual Unity import, prefab serialization, UI navigation and offscreen Canvas rendering. Reference values are preview data, not measured hardware telemetry.";
        }

        public static void BuildBatch() {
            try { Build(); }
            catch (Exception e) {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/UIBuildError.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        [MenuItem("WingSinger/UI/Build Reference UI")]
        public static void Build() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Build UI outside Play mode after compilation.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save the current scene before building UI.");
            checks.Clear(); sprites.Clear(); sharedReady = false;
            Directory.CreateDirectory(PreviewDir); Directory.CreateDirectory(Path.GetDirectoryName(Report));
            Folder(Screens.TrimEnd('/')); Folder(Shared.TrimEnd('/')); Folder("Assets/Scenes/UI");
            ImportSprites(); PrepareFont(); BuildShared(); sharedReady = true;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = new GameObject("UIFlowRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var router = canvas.AddComponent<UIScreenRouter>();
            var pages = new List<UIPageView>();
            var paths = new List<string>();
            foreach (UIScreenId id in Enum.GetValues(typeof(UIScreenId)).Cast<UIScreenId>().Where(id => (int)id <= 13)) {
                var page = MakeScreen(id);
                var path = Screens + ((int)id).ToString("00") + "_" + id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(page.gameObject, path);
                Object.DestroyImmediate(page.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), canvas.transform);
                Stretch(instance.GetComponent<RectTransform>());
                pages.Add(instance.GetComponent<UIPageView>()); paths.Add(path);
            }
            router.pages = pages.ToArray(); router.Initialize();
            ValidateStructure(router, paths);
            ValidateNavigation(router);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Require(Object.FindObjectsOfType<EventSystem>().Length==1,"Preview scene has exactly one EventSystem");
            var previewCamera=new GameObject("UI Preview Camera",typeof(Camera)).GetComponent<Camera>();
            previewCamera.clearFlags=CameraClearFlags.SolidColor; previewCamera.backgroundColor=Ink; previewCamera.cullingMask=0;
            PrefabUtility.SaveAsPrefabAsset(canvas, FlowPrefab);
            EditorSceneManager.SaveScene(scene, PreviewScene);
            CapturePages(canvas, router);
            router.Initialize(); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            EditorSceneManager.SaveScene(scene, PreviewScene);
            AssetDatabase.SaveAssets();
            var report = new BuildReport {
                unityVersion = Application.unityVersion, date = "2026-10-25 Asia/Shanghai",
                previewScene = PreviewScene, flowPrefab = FlowPrefab, screenCount = paths.Count,
                spriteCount = sprites.Count, sharedPrefabCount = 7, prefabPaths = paths.ToArray(), passedChecks = checks.ToArray()
            };
            File.WriteAllText(Report, JsonUtility.ToJson(report, true));
            UIIntegrationSetup.InstallInteractions();
            HardwareSettingsSetup.Apply();
            Debug.Log($"WingSinger UI build passed: {paths.Count} screens, {sprites.Count} sprites, {checks.Count} checks. Open {PreviewScene}");
        }

        static void ImportSprites() {
            var manifest = JsonUtility.FromJson<AssetManifest>(File.ReadAllText(Art + "UIAssetManifest.json"));
            foreach (var entry in manifest.assets) {
                AssetDatabase.ImportAsset(entry.path);
                var importer = AssetImporter.GetAtPath(entry.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Texture not imported: " + entry.path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.spriteBorder = Vector4.one * entry.border;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                sprites.Add(entry.key, AssetDatabase.LoadAssetAtPath<Sprite>(entry.path));
                Require(sprites[entry.key] != null, "Sprite imported: " + entry.key);
            }
        }
        static void PrepareFont() {
            const string path = Art + "Fonts/DarumadropOne SDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return;
            AssetDatabase.ImportAsset(Art + "Fonts/DarumadropOne-Regular.ttf");
            var source = AssetDatabase.LoadAssetAtPath<Font>(Art + "Fonts/DarumadropOne-Regular.ttf");
            if (source == null) throw new InvalidOperationException("Darumadrop One font is missing.");
            font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
            font.name = "DarumadropOne SDF";
            AssetDatabase.CreateAsset(font, path);
            font.material.name = "DarumadropOne UI Material"; AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            var characters = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            Require(font.TryAddCharacters(characters, out string missing), "ASCII reference typography has glyph coverage: " + missing);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
        }

        static void BuildShared() {
            var primary = ButtonRaw(null,"PrimaryButton",0,0,460,100,"PRIMARY ACTION",true);
            PrefabUtility.SaveAsPrefabAsset(primary.gameObject, Shared + "PrimaryButton.prefab"); Object.DestroyImmediate(primary.gameObject);
            var secondary = ButtonRaw(null,"SecondaryButton",0,0,320,100,"SECONDARY",false);
            PrefabUtility.SaveAsPrefabAsset(secondary.gameObject, Shared + "SecondaryButton.prefab"); Object.DestroyImmediate(secondary.gameObject);
            var stat = Panel(null,"StatCard",0,0,270,230,"FillBlue");
            Picture(stat.transform,"Icon","IconInhale",24,24,55,55); Label(stat.transform,"Label","Practice time",24,105,225,40,24);
            Label(stat.transform,"Value","2:48",24,150,225,60,42);
            PrefabUtility.SaveAsPrefabAsset(stat, Shared + "StatCard.prefab"); Object.DestroyImmediate(stat);
            var level = Panel(null,"LevelCard",0,0,294,178,"PanelOutline");
            level.AddComponent<Mask>().showMaskGraphic=true;
            Picture(level.transform,"Thumbnail","ForestValley",3,3,288,172,false);
            Label(level.transform,"Number","4",0,40,294,92,72,Color.white,TextAlignmentOptions.Center);
            Panel(level.transform,"StatusBackground",232,120,47,47,"ButtonCream");
            Picture(level.transform,"Status","IconPlay",244,131,24,26);
            PrefabUtility.SaveAsPrefabAsset(level, Shared + "LevelCard.prefab"); Object.DestroyImmediate(level);
            var badge = Panel(null,"ConnectionBadge",0,0,275,88,"ButtonCream");
            Picture(badge.transform,"Icon","IconLink",25,26,35,35); Label(badge.transform,"Label","Connected",80,24,180,40,28,Green);
            PrefabUtility.SaveAsPrefabAsset(badge, Shared + "ConnectionBadge.prefab"); Object.DestroyImmediate(badge);
            var header = Group(null,"ScreenHeader");
            Picture(header,"Logo","Logo",671,42,380,89); Nav(header,"Back",80,43,228,80,"Back",UIScreenId.HomeDashboard);
            PrefabUtility.SaveAsPrefabAsset(header.gameObject, Shared + "ScreenHeader.prefab"); Object.DestroyImmediate(header.gameObject);
            var hud = Group(null,"PracticeHUD"); PracticeHeader(hud,1,0);
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, Shared + "PracticeHUD.prefab"); Object.DestroyImmediate(hud.gameObject);
        }

        static UIPageView MakeScreen(UIScreenId id) {
            var root = new GameObject(((int)id).ToString("00") + "_" + id, typeof(RectTransform), typeof(UIPageView));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(2420,1668);
            var view = root.GetComponent<UIPageView>(); view.screenId = id;
            var bg = Picture(root.transform,"Background","ForestValley",0,0,1902,1312,false);
            Stretch(bg.rectTransform);
            var art = Group(root.transform,"Artwork");
            var header = Group(root.transform,"Header");
            var content = Group(root.transform,"Content");
            var footer = Group(root.transform,"Footer");
            switch (id) {
                case UIScreenId.HomeDashboard: Home(art,header,content,footer,true); break;
                case UIScreenId.HomeClean: Home(art,header,content,footer,false); break;
                case UIScreenId.LevelSelect: Levels(art,header,content); break;
                case UIScreenId.ReadyForTakeoff: Ready(art,header,content); break;
                case UIScreenId.FlightPaused: Paused(art,content); break;
                case UIScreenId.FlightComplete: Complete(art,header,content); break;
                case UIScreenId.Settings: Settings(header,content); break;
                case UIScreenId.ControllerConnection: Controller(art,header,content); break;
                case UIScreenId.FlightLog: FlightLog(art,header,content); break;
                default: Practice(id,art,header,content,footer); break;
            }
            view.selectedLevelLabels=root.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="LevelTitle" || (id==UIScreenId.FlightPaused && t.name=="Level")).ToArray();
            UIViewportSetup.ConfigurePage(view);
            return view;
        }

        static void Home(Transform art,Transform header,Transform content,Transform footer,bool dashboard) {
            Picture(header,"Logo","Logo",75,42,380,89);
            var log=Nav(header,"FlightLog",1333,43,240,80,"Flight Log",UIScreenId.FlightLog); IconInside(log,"IconLog",22,20,40,40); MoveButtonLabel(log,68,0,165,80,26);
            var settings=Nav(header,"Settings",1594,43,228,80,"Settings",UIScreenId.Settings); IconInside(settings,"IconSettings",22,20,40,40); MoveButtonLabel(settings,69,0,154,80,26);
            Picture(art,"Airship","AirshipNeutral",405,dashboard?133:315,551,815);
            float shift=dashboard?0:202;
            Picture(content,"ForestEmblem","ForestEmblem",1285,365+shift,140,125);
            Label(content,"WorldTitle","Windbell Forest",1110,514+shift,565,95,56,Ink,TextAlignmentOptions.Center);
            var levels=Panel(content,"LevelCount",1284,611+shift,208,61,"FillGreen"); Label(levels.transform,"Label","12 levels",0,0,208,61,27,Ink,TextAlignmentOptions.Center);
            var prev=IconButton(content,"PreviousWorld","IconChevronLeft",970,511+shift,108,108); prev.interactable=false;
            var next=IconButton(content,"NextWorld","IconChevronRight",1725,511+shift,108,108); next.interactable=false;
            for(int i=0;i<3;i++) { var dot=Panel(content,"WorldDot"+i,1306+i*60,706+shift,35,35,"ButtonCream"); dot.GetComponent<Image>().color=i==0?Orange:Color.white; }
            Nav(content,"ViewLevels",1158,783+shift,461,98,"VIEW LEVELS  >",UIScreenId.LevelSelect,true);
            if (!dashboard) return;
            var tray=Panel(footer,"TodayPanel",0,1047,1902,265,"PanelCream");
            Label(tray.transform,"Title","TODAY'S FLIGHT",70,27,450,48,28);
            Stat(tray.transform,68,85,263,141,"FillBlue","IconInhale","Inhale","3 s");
            Stat(tray.transform,355,85,263,141,"FillPeach","IconExhale","Exhale","4 s");
            Stat(tray.transform,641,85,263,141,"FillLavender","IconPractice","Practice","2 / 6");
            Line(tray.transform,969,52,2,164,Hex("efd5ad"));
            var streak=Panel(tray.transform,"Streak",1026,44,320,61,"FillPeach"); Picture(streak.transform,"Icon","IconFire",20,10,39,42); Label(streak.transform,"Label","5-day streak",79,7,230,48,28);
            var connected=Nav(tray.transform,"Controller",1411,44,420,61,"Controller connected",UIScreenId.ControllerConnection); SetSkin(connected,"FillGreen"); IconInside(connected,"IconGamepad",20,13,40,36); MoveButtonLabel(connected,87,0,320,61,25);
            Picture(tray.transform,"Heart","IconHeart",1035,148,48,45);
            Label(tray.transform,"Tip","Sit comfortably. Relax your shoulders.",1104,148,730,62,28,Muted);
        }

        static void Levels(Transform art,Transform header,Transform content) {
            Picture(art,"Airship","AirshipGuiding",-130,390,650,960);
            Back(header,"Maps",UIScreenId.HomeDashboard); TopSettings(header);
            Picture(header,"ForestEmblem","ForestEmblem",865,10,126,112);
            Label(header,"Title","WINDBELL FOREST",650,133,690,88,63,Ink,TextAlignmentOptions.Center);
            var subtitle=Panel(header,"Subtitle",781,208,353,60,"FillBlue"); Label(subtitle.transform,"Text","Choose a level",0,0,353,60,26,Ink,TextAlignmentOptions.Center);
            var panel=Panel(content,"LevelsPanel",461,272,1361,963,"PanelCream");
            for(int n=1;n<=12;n++) {
                float x=48+((n-1)%4)*320, y=47+((n-1)/4)*202;
                var card=InstantiateShared("LevelCard",panel.transform); Place(card.GetComponent<RectTransform>(),x,y,294,178);
                var number=card.transform.Find("Number").GetComponent<TMP_Text>(); number.text=n.ToString(); number.color=n<=3?Color.white:n==4?Hex("6c3500"):Hex("405537");
                var image=card.transform.Find("Thumbnail").GetComponent<Image>(); image.color=n>4?new Color(.67f,.73f,.57f,.6f):Color.white;
                var status=card.transform.Find("Status").GetComponent<Image>(); status.sprite=S(n<=3?"IconCheck":n==4?"IconPlay":"IconLock");
                card.transform.Find("StatusBackground").gameObject.SetActive(n<=4);
                if(n>4) Place(status.rectTransform,240,124,34,40);
                var button=card.AddComponent<Button>(); button.targetGraphic=card.GetComponent<Image>(); button.interactable=n<=4;
                card.GetComponent<Image>().raycastTarget=true;
                var route=card.AddComponent<UIRouteButton>(); route.action=UIRouteAction.SelectLevel; route.levelNumber=n;
                if(n==4) { var outline=card.AddComponent<Outline>(); outline.effectColor=Orange; outline.effectDistance=new Vector2(3,-3); }
            }
            Legend(panel.transform,"IconCheck","Completed",91,678); Legend(panel.transform,"IconPlay","Next flight",502,678); Legend(panel.transform,"IconLock","Locked",924,678);
            var detail=Panel(panel.transform,"SelectedLevel",36,765,1286,161,"PanelOutline");
            Label(detail.transform,"LevelTitle","LEVEL 4 - GENTLE BREEZE",45,31,690,54,33);
            Label(detail.transform,"Description","A calm flight through the forest canopy.",45,90,740,45,26,Muted);
            Nav(detail.transform,"Continue",819,30,415,100,"CONTINUE  >",UIScreenId.ReadyForTakeoff,true);
        }
        static void Ready(Transform art,Transform header,Transform content) {
            Back(header,"Levels",UIScreenId.LevelSelect);
            Picture(art,"Airship","AirshipNeutral",210,158,606,922);
            var panel=Panel(content,"ReadyPanel",983,171,839,1064,"PanelCream");
            Label(panel.transform,"Title","Ready for takeoff?",48,36,745,78,51);
            var level=Panel(panel.transform,"LevelTitle",50,120,606,61,"FillPeach"); Label(level.transform,"Label","Level 4 - Gentle Breeze",0,0,606,61,28,Ink,TextAlignmentOptions.Center);
            string[] checklist={"Controller connected","Sit comfortably","Relax your shoulders"};
            for(int i=0;i<3;i++) { var row=Panel(panel.transform,"Checklist"+i,50,218+i*95,738,78,"FillGreen"); Picture(row.transform,"Check","IconCheck",26,21,40,36); Label(row.transform,"Text",checklist[i],94,10,625,60,30); }
            Stat(panel.transform,50,520,231,174,"FillBlue","IconInhale","Inhale","3 s");
            Stat(panel.transform,305,520,231,174,"FillPeach","IconExhale","Exhale","4 s");
            Stat(panel.transform,560,520,231,174,"FillLavender","IconPractice","Practice","2 / 6");
            Label(panel.transform,"InhaleHint","Inhale - descend",64,705,370,38,23,Hex("009bce"));
            Picture(panel.transform,"BlueWave","RhythmWaveBlue",50,733,380,74,false);
            Picture(panel.transform,"OrangeWave","RhythmWaveOrange",429,797,359,47,false);
            Label(panel.transform,"ExhaleHint","Exhale - rise",438,838,350,38,23,Hex("ef6c00"));
            var practice=Nav(panel.transform,"TryRhythm",50,900,312,97,"Try the rhythm",UIScreenId.PracticeInhale); practice.GetComponent<UIRouteButton>().action=UIRouteAction.TryRhythm;
            var start=Nav(panel.transform,"StartFlight",386,900,402,97,"START FLIGHT",UIScreenId.PracticeFollow,true); start.GetComponent<UIRouteButton>().action=UIRouteAction.StartFlight;
        }
        static void Paused(Transform art,Transform content) {
            var dim=Panel(art,"ForestDim",0,0,1902,1312,"PixelWhite"); dim.GetComponent<Image>().color=new Color(.11f,.26f,.28f,.12f);
            var panel=Panel(content,"PausePanel",497,316,908,801,"PanelCream");
            Picture(art,"Mascot","MascotRelaxed",425,43,378,287);
            Label(panel.transform,"Title","FLIGHT PAUSE",55,69,800,112,66,Ink,TextAlignmentOptions.Center);
            Label(panel.transform,"Subtitle","Take your time.",55,176,800,58,35,Muted,TextAlignmentOptions.Center);
            var progress=Panel(panel.transform,"ProgressPanel",54,269,799,124,"FillPeach");
            Label(progress.transform,"Level","Level 4 - Gentle Breeze",35,14,690,46,28);
            Progress(progress.transform,35,73,479,22,.5f); Label(progress.transform,"Cycles","6 / 12 cycles",540,62,230,45,27);
            var resume=Nav(panel.transform,"Resume",67,434,774,98,"RESUME  >",UIScreenId.PracticeFollow,true); resume.GetComponent<UIRouteButton>().action=UIRouteAction.Resume;
            var restart=Nav(panel.transform,"Restart",67,565,371,98,"RESTART",UIScreenId.PracticeInhale); restart.GetComponent<UIRouteButton>().action=UIRouteAction.Restart;
            Nav(panel.transform,"Levels",471,565,371,98,"LEVELS",UIScreenId.LevelSelect);
            var settings=TextButton(panel.transform,"Settings",345,704,230,61,"Settings",UIScreenId.Settings,Muted,29); IconInside(settings,"IconSettings",2,9,39,39);
        }
        static void Complete(Transform art,Transform header,Transform content) {
            Picture(header,"Logo","Logo",75,42,380,89); Picture(art,"Celebration","AirshipComplete",36,140,720,1070);
            Label(header,"Title","Flight complete!",1002,97,820,120,76);
            Label(header,"Subtitle","A little practice, a little progress.",1060,215,770,70,32);
            var panel=Panel(content,"CompletePanel",851,329,971,906,"PanelCream");
            Picture(panel.transform,"Emblem","ForestEmblem",53,27,103,97);
            Label(panel.transform,"LevelTitle","Level 4 - Gentle Breeze",198,57,722,69,39);
            Stat(panel.transform,49,156,272,227,"FillBlue","IconInhale","Practice time","2:48");
            Stat(panel.transform,349,156,272,227,"FillPeach","IconPractice","Cycles completed","12 / 12");
            Stat(panel.transform,649,156,272,227,"FillLavender","IconInhale","Rhythm matched","8 / 12");
            var today=Panel(panel.transform,"TodayProgress",49,433,871,114,"FillGreen");
            Picture(today.transform,"Calendar","IconCalendar",30,26,45,50);
            Label(today.transform,"Title","Today's practice",113,17,390,46,30);
            Label(today.transform,"Value","1 / 2 sessions",113,61,390,38,27,Green); Progress(today.transform,504,49,320,22,.5f);
            var praise=Panel(panel.transform,"Praise",49,580,871,91,"FillPeach"); Picture(praise.transform,"Heart","IconHeart",28,22,45,44); Label(praise.transform,"Text","Nice work finding your rhythm.",93,18,740,58,31);
            var next=Nav(panel.transform,"NextLevel",49,711,501,98,"NEXT LEVEL  >",UIScreenId.ReadyForTakeoff,true); next.GetComponent<UIRouteButton>().action=UIRouteAction.NextLevel;
            Nav(panel.transform,"Levels",578,711,342,98,"LEVELS",UIScreenId.LevelSelect);
            var again=TextButton(panel.transform,"PlayAgain",305,829,350,55,"Play again",UIScreenId.PracticeFollow,Hex("ef6c00"),29); again.GetComponent<UIRouteButton>().action=UIRouteAction.Restart;
        }
        static void Settings(Transform header,Transform content) {
            Back(header,"Back",UIScreenId.HomeDashboard,true); Picture(header,"Logo","Logo",671,42,380,89);
            var panel=Panel(content,"SettingsPanel",343,186,1215,1049,"PanelCream");
            Label(panel.transform,"Title","Settings",60,31,1090,95,58,Ink,TextAlignmentOptions.Center);
            var audio=Panel(panel.transform,"AudioSection",50,134,1115,263,"PanelOutline"); SectionPill(audio.transform,"Audio","IconAudio","FillPeach");
            Label(audio.transform,"MusicLabel","Music",49,98,350,56,30); Slider(audio.transform,"Music",415,114,505,.6f,962,97);
            Label(audio.transform,"SfxLabel","Sound effects",49,181,350,56,30); Slider(audio.transform,"SoundEffects",415,197,505,.8f,962,180);
            var display=Panel(panel.transform,"DisplaySection",50,427,1115,263,"PanelOutline"); SectionPill(display.transform,"Display","IconDisplay","FillBlue");
            Label(display.transform,"ReduceMotionLabel","Reduce motion",49,98,650,56,30); Toggle(display.transform,"ReduceMotion",842,99,991,98);
            Label(display.transform,"ContrastLabel","High contrast",49,181,650,56,30); Toggle(display.transform,"HighContrast",842,182,991,181);
            var support=Panel(panel.transform,"SupportSection",50,719,1115,300,"PanelOutline"); SectionPill(support.transform,"Support","IconHelp","FillLavender");
            SupportRow(support.transform,"Controller","IconGamepad","Connected",96,UIScreenId.ControllerConnection,Green);
            var language=SupportRow(support.transform,"Language","IconLanguage","English",171,UIScreenId.Settings,Muted); language.interactable=false;
            SupportRow(support.transform,"How to play","IconHelp","",237,UIScreenId.PracticeInhale,Muted).GetComponent<UIRouteButton>().action=UIRouteAction.TryRhythm;
        }
        static void Controller(Transform art,Transform header,Transform content) {
            Back(header,"Back",UIScreenId.Settings,true); Picture(header,"Logo","Logo",671,42,380,89);
            Picture(art,"Airship","AirshipGuiding",145,177,711,849);
            foreach(var offset in new[]{new Vector2(-2,0),new Vector2(2,0),new Vector2(0,-2),new Vector2(0,2)})
                Label(art,"MessageOutline","Your controller\nguides your flight.",237+offset.x,1048+offset.y,610,130,40,new Color(1,.97f,.91f,.85f));
            Label(art,"Message","Your controller\nguides your flight.",237,1048,610,130,40);
            var panel=Panel(content,"ControllerPanel",965,171,857,1064,"PanelCream");
            Label(panel.transform,"Title","Connect your controller",50,42,756,93,47,Ink,TextAlignmentOptions.Center);
            Picture(panel.transform,"DeviceIllustration","ControllerDevice",215,239,485,143);
            Label(panel.transform,"DeviceName","Wind Controller",80,471,700,84,45,Ink,TextAlignmentOptions.Center);
            var badge=Panel(panel.transform,"Status",274,551,309,62,"FillGreen"); Label(badge.transform,"Label","Connected",0,0,309,62,30,Ink,TextAlignmentOptions.Center);
            Detail(panel.transform,"Battery","IconBattery","82%",76,657);
            Detail(panel.transform,"Signal","IconSignal","Good",76,733);
            var check=Nav(panel.transform,"CheckConnection",49,821,757,98,"CHECK CONNECTION",UIScreenId.ControllerConnection); SetSkin(check,"ButtonBlue"); check.GetComponent<UIRouteButton>().action=UIRouteAction.CheckConnection;
            var done=Nav(panel.transform,"Done",49,942,757,98,"DONE  >",UIScreenId.Settings,true); done.GetComponent<UIRouteButton>().action=UIRouteAction.Back;
        }
        static void FlightLog(Transform art,Transform header,Transform content) {
            Back(header,"Home",UIScreenId.HomeDashboard); Picture(header,"Logo","Logo",435,42,380,89);
            Picture(art,"Mascot","MascotRelaxed",1295,110,433,330);
            Label(header,"Title","Flight Log",661,213,580,104,62,Ink,TextAlignmentOptions.Center);
            Label(header,"Subtitle","Look at your journey. You're doing great!",674,316,620,62,26);
            var panel=Panel(content,"LogPanel",81,417,1740,818,"PanelCream");
            LogStat(panel.transform,50,"FillPeach","IconFire","5-day","streak");
            LogStat(panel.transform,605,"FillBlue","IconPlay","3 flights","this week");
            LogStat(panel.transform,1160,"FillLavender","IconInhale","8 min","practiced");
            var week=Panel(panel.transform,"Week",50,272,818,377,"PanelOutline"); Label(week.transform,"Title","This week",38,24,700,75,41);
            string[] days={"Mon","Tue","Wed","Thu","Fri","Sat","Sun"};
            for(int i=0;i<7;i++) { float x=49+i*106; Label(week.transform,"Day"+i,days[i],x-8,131,84,43,25,Muted,TextAlignmentOptions.Center); var dot=Panel(week.transform,"Complete"+i,x,200,70,70,"ButtonCream"); dot.GetComponent<Image>().color=i<3?Green:Hex("e4e0d6"); if(i<3) Picture(dot.transform,"Check","IconCheckWhite",16,20,37,32); }
            var recent=Panel(panel.transform,"Recent",900,272,789,377,"PanelOutline"); Label(recent.transform,"Title","Recent flights",37,24,710,75,41);
            string[] names={"Gentle Breeze","First Lift","Forest Hello"}; string[] times={"2:48","2:35","2:30"};
            for(int i=0;i<3;i++) { var row=Nav(recent.transform,"RecentFlight"+i,29,98+i*92,730,80,"",UIScreenId.FlightComplete); Picture(row.transform,"Emblem","ForestEmblem",10,5,74,70); Label(row.transform,"Title",names[i],97,0,545,43,29); Label(row.transform,"Detail","Completed - "+times[i],97,39,545,34,24,Muted); Picture(row.transform,"Chevron","IconChevronRight",680,23,22,33).color=Muted; }
            Picture(panel.transform,"Heart","IconHeart",81,710,53,49); Label(panel.transform,"Footer","Every flight counts.",160,710,800,60,32,Muted);
            Nav(panel.transform,"Maps",1176,693,513,98,"BACK TO MAPS  >",UIScreenId.HomeDashboard,true);
        }
        static void Practice(UIScreenId id,Transform art,Transform header,Transform content,Transform footer) {
            int step=(int)id-9; int stars=step<3?0:step==3?4:8;
            var hud=InstantiateShared("PracticeHUD",header); Stretch(hud.GetComponent<RectTransform>());
            hud.transform.Find("Step/Label").GetComponent<TMP_Text>().text=step+" / 4";
            hud.transform.Find("Stars/Label").GetComponent<TMP_Text>().text=stars.ToString();
            if(id==UIScreenId.PracticeComplete) {
                Picture(art,"Airship","AirshipGuiding",220,505,498,568);
                Picture(art,"Crane","Crane",1665,648,210,210);
                var panel=Panel(content,"PracticeCompletePanel",809,315,819,779,"FillCream");
                Label(panel.transform,"Title","PRACTICE COMPLETE",56,40,710,100,44,Ink,TextAlignmentOptions.Center);
                Picture(panel.transform,"Star","PaperStar",101,181,195,195);
                Label(panel.transform,"Count","8",414,188,240,145,128,Ink,TextAlignmentOptions.Center);
                Label(panel.transform,"CountCaption","STARS COLLECTED",362,340,389,65,35,Ink,TextAlignmentOptions.Center);
                Line(panel.transform,70,425,675,2,Hex("efd5ad"));
                Label(panel.transform,"Ready","Ready for your first level?",65,469,690,95,38,Ink,TextAlignmentOptions.Center);
                Nav(panel.transform,"ChooseLevel",110,578,599,140,"CHOOSE A LEVEL  >",UIScreenId.LevelSelect,true);
                return;
            }
            bool inhale=id==UIScreenId.PracticeInhale;
            Picture(art,"Airship","AirshipGuiding",inhale?269:295,inhale?535:id==UIScreenId.PracticeExhale?245:420,452,557);
            if(id!=UIScreenId.PracticeFollow) {
                var ghost=Picture(art,"AirshipGhost","AirshipGuiding",inhale?285:295,inhale?206:670,350,480); ghost.color=new Color(1,1,1,.16f);
                Picture(content,"GuidePath",inhale?"PathDescend":"PathAscend",691,278,785,550,false);
                Picture(content,"Crane","Crane",inhale?1385:1389,inhale?813:285,170,150);
                var cue=Panel(content,"Cue",865,480,447,87,"FillLavender"); Label(cue.transform,"Label",inhale?"BREATHE IN":"BREATHE OUT",0,0,447,87,40,Ink,TextAlignmentOptions.Center);
            } else {
                Picture(content,"GuidePath","PathFollow",747,400,923,546,false);
                Picture(content,"Crane","Crane",1465,350,210,210);
                float[,] points={{795,785},{1032,885},{1239,664},{1442,771}};
                for(int i=0;i<4;i++) Picture(content,"Star"+i,"PaperStar",points[i,0],points[i,1],100,100);
                Label(content,"CollectFeedback","+1",767,648,100,95,48,Color.white,TextAlignmentOptions.Center);
            }
            string title=inhale?"Breathe in gently.":id==UIScreenId.PracticeExhale?"Breathe out gently.":"Follow the crane.";
            string subtitle=inhale?"Follow the crane downward.":id==UIScreenId.PracticeExhale?"Follow the crane upward.":"Collect stars along the way.";
            var instruction=Panel(footer,"Instruction",456,1110,990,145,"FillCream");
            Label(instruction.transform,"Title",title,50,22,890,68,44,Ink,TextAlignmentOptions.Center);
            Label(instruction.transform,"Subtitle",subtitle,50,86,890,45,30,Muted,TextAlignmentOptions.Center);
            var button=instruction.AddComponent<Button>(); button.targetGraphic=instruction.GetComponent<Image>();
            instruction.GetComponent<Image>().raycastTarget=true;
            instruction.AddComponent<UIRouteButton>().action=UIRouteAction.AdvancePractice;
        }

        static void PracticeHeader(Transform parent,int step,int stars) {
            var pause=IconButton(parent,"Pause","IconPause",70,50,89,89); pause.gameObject.AddComponent<UIRouteButton>().action=UIRouteAction.Pause;
            var count=Panel(parent,"Step",865,50,173,89,"ButtonCream"); Label(count.transform,"Label",step+" / 4",0,0,173,89,40,Ink,TextAlignmentOptions.Center);
            if(sharedReady) { var badge=InstantiateShared("ConnectionBadge",parent); Place(badge.GetComponent<RectTransform>(),1352,50,275,89); }
            else { var badge=Panel(parent,"Connection",1352,50,275,89,"ButtonCream"); Picture(badge.transform,"Icon","IconLink",25,26,35,35); Label(badge.transform,"Label","Connected",80,24,180,40,28,Green); }
            var score=Panel(parent,"Stars",1651,50,181,89,"ButtonCream"); Picture(score.transform,"Icon","PaperStar",14,12,62,62); Label(score.transform,"Label",stars.ToString(),92,18,80,60,40,Ink,TextAlignmentOptions.Center);
        }
        static void SectionPill(Transform parent,string label,string icon,string skin) {
            var pill=Panel(parent,label+"Pill",23,10,263,60,skin); Picture(pill.transform,"Icon",icon,15,9,42,42); Label(pill.transform,"Label",label,84,8,168,46,28);
        }
        static Button SupportRow(Transform parent,string name,string icon,string value,float y,UIScreenId destination,Color valueColor) {
            var button=TextButton(parent,name.Replace(" ",""),37,y,1030,55,"",destination,Ink,30);
            Picture(button.transform,"Icon",icon,7,5,42,42); Label(button.transform,"Name",name,70,0,650,55,30);
            Label(button.transform,"Value",value,710,0,241,55,29,valueColor,TextAlignmentOptions.Right);
            Picture(button.transform,"Chevron","IconChevronRight",987,10,23,32); return button;
        }
        static void Detail(Transform parent,string name,string icon,string value,float x,float y) {
            Picture(parent,name+"Icon",icon,x,y,50,50); Label(parent,name+"Label",name,x+80,y,440,58,32,Muted); Label(parent,name+"Value",value,x+520,y,190,58,32,Ink,TextAlignmentOptions.Right);
        }
        static void LogStat(Transform parent,float x,string skin,string icon,string value,string caption) {
            var card=Panel(parent,"Summary"+caption.Replace(" ",""),x,45,530,193,skin); Picture(card.transform,"Icon",icon,35,60,84,84); Label(card.transform,"Value",value,146,26,358,83,54); Label(card.transform,"Caption",caption,146,119,358,52,33,Muted);
        }
        static void Slider(Transform parent,string name,float x,float y,float width,float value,float labelX,float labelY) {
            var root=Group(parent,name); Place(root,x,y-16,width,60);
            var track=Panel(root,"Track",0,16,width,28,"PillWhite"); track.GetComponent<Image>().color=Hex("efd5ad"); track.GetComponent<Image>().pixelsPerUnitMultiplier=128/(28*SY);
            var fillArea=Group(root,"FillArea"); Place(fillArea,0,16,width,28); var fill=Panel(fillArea,"Fill",0,0,width,28,"PillWhite"); fill.GetComponent<Image>().color=Orange; fill.GetComponent<Image>().pixelsPerUnitMultiplier=128/(28*SY);
            var fillRect=fill.GetComponent<RectTransform>(); Stretch(fillRect);
            track.GetComponent<Image>().raycastTarget=true;
            var handleArea=Group(root,"HandleArea"); Stretch(handleArea); handleArea.offsetMin=new Vector2(14*SX,0); handleArea.offsetMax=new Vector2(-14*SX,0);
            var handle=Picture(handleArea,"Handle","SliderHandle",0,0,56,56);
            handle.raycastTarget=true;
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(value,.5f); handle.rectTransform.pivot=new Vector2(.5f,.5f); handle.rectTransform.anchoredPosition=Vector2.zero;
            var slider=root.gameObject.AddComponent<Slider>(); slider.fillRect=fill.GetComponent<RectTransform>(); slider.handleRect=handle.rectTransform;
            slider.targetGraphic=handle; slider.minValue=0; slider.maxValue=1; slider.value=value;
            var label=Label(parent,name+"Value",Mathf.RoundToInt(value*100)+"%",labelX,labelY,110,60,32,Muted);
            root.gameObject.AddComponent<UISliderValue>().valueLabel=label;
        }
        static void Toggle(Transform parent,string name,float x,float y,float labelX,float labelY) {
            var root=Panel(parent,name,x,y,109,55,"ToggleOff");
            root.GetComponent<Image>().raycastTarget=true;
            var handle=Picture(root.transform,"Handle","FillWhite",5,6,43,43); handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(.5f,.5f); handle.rectTransform.pivot=new Vector2(.5f,.5f); handle.rectTransform.anchoredPosition=new Vector2(-35,0);
            handle.type=Image.Type.Sliced; handle.preserveAspect=false;
            var toggle=root.AddComponent<Toggle>(); toggle.targetGraphic=root.GetComponent<Image>(); toggle.isOn=false;
            var presenter=root.AddComponent<UIToggleValue>(); presenter.track=root.GetComponent<Image>(); presenter.handle=handle.rectTransform;
            presenter.valueLabel=Label(parent,name+"Value","Off",labelX,labelY,90,60,29,Muted);
        }
        static void Legend(Transform parent,string icon,string text,float x,float y) { Picture(parent,text+"Icon",icon,x,y,36,38); Label(parent,text+"Label",text,x+52,y-1,300,48,28); }
        static void Progress(Transform parent,float x,float y,float w,float h,float fraction) { var track=Panel(parent,"Track",x,y,w,h,"PillWhite").GetComponent<Image>(); track.color=Hex("efd5ad"); track.pixelsPerUnitMultiplier=128/(h*SY); var fill=Panel(parent,"Fill",x,y,w*fraction,h,"PillWhite").GetComponent<Image>(); fill.color=Green; fill.pixelsPerUnitMultiplier=128/(h*SY); }
        static void Back(Transform parent,string text,UIScreenId destination,bool history=false) {
            var button=Nav(parent,"Back",80,43,228,80,text,destination);
            IconInside(button,"IconChevronLeft",24,23,26,36); MoveButtonLabel(button,64,0,155,80,28);
            if(history) button.GetComponent<UIRouteButton>().action=UIRouteAction.Back;
        }
        static void TopSettings(Transform parent) { var button=Nav(parent,"Settings",1594,43,228,80,"Settings",UIScreenId.Settings); IconInside(button,"IconSettings",22,20,40,40); MoveButtonLabel(button,69,0,154,80,26); }
        static GameObject Stat(Transform parent,float x,float y,float w,float h,string skin,string icon,string label,string value) {
            var root=InstantiateShared("StatCard",parent); Place(root.GetComponent<RectTransform>(),x,y,w,h); root.GetComponent<Image>().sprite=S(skin);
            var image=root.transform.Find("Icon").GetComponent<Image>(); image.sprite=S(icon);
            var text=root.transform.Find("Label").GetComponent<TMP_Text>(); text.text=label;
            var number=root.transform.Find("Value").GetComponent<TMP_Text>(); number.text=value;
            bool compact=h<150;
            bool medium=!compact&&h<200;
            Place(image.rectTransform,compact?20:24,compact?43:23,55,55);
            Place(text.rectTransform,compact?89:24,compact?20:medium?80:96,w-(compact?100:40),medium?35:48); text.fontSize=(compact?30:24)*SX*TypeScale;
            Place(number.rectTransform,compact?89:24,compact?64:medium?122:135,w-(compact?100:40),medium?46:64); number.fontSize=42*SX*TypeScale;
            return root;
        }

        static Button ButtonRaw(Transform parent,string name,float x,float y,float w,float h,string text,bool primary) {
            var root=Panel(parent,name,x,y,w,h,primary?"ButtonOrangeNormal":"ButtonCream");
            root.GetComponent<Image>().raycastTarget=true;
            var button=root.AddComponent<Button>(); button.targetGraphic=root.GetComponent<Image>(); ConfigureButton(button,primary);
            root.AddComponent<UIRouteButton>(); Label(root.transform,"Label",text,8,0,w-16,h,32,primary?Color.white:Ink,TextAlignmentOptions.Center);
            return button;
        }
        static Button Nav(Transform parent,string name,float x,float y,float w,float h,string text,UIScreenId destination,bool primary=false) {
            Button button;
            if(sharedReady) { var go=InstantiateShared(primary?"PrimaryButton":"SecondaryButton",parent); go.name=name; Place(go.GetComponent<RectTransform>(),x,y,w,h); button=go.GetComponent<Button>(); MoveButtonLabel(button,8,0,w-16,h,32); button.GetComponentInChildren<TMP_Text>().text=text; }
            else button=ButtonRaw(parent,name,x,y,w,h,text,primary);
            var route=button.GetComponent<UIRouteButton>(); route.action=UIRouteAction.Navigate; route.destination=destination;
            return button;
        }
        static Button TextButton(Transform parent,string name,float x,float y,float w,float h,string text,UIScreenId destination,Color color,float size) {
            var button=ButtonRaw(parent,name,x,y,w,h,text,false); button.GetComponent<Image>().color=Color.clear; button.transition=Selectable.Transition.ColorTint;
            button.GetComponent<UIRouteButton>().destination=destination; MoveButtonLabel(button,0,0,w,h,size); button.GetComponentInChildren<TMP_Text>().color=color; return button;
        }
        static Button IconButton(Transform parent,string name,string icon,float x,float y,float w,float h) {
            var root=Panel(parent,name,x,y,w,h,"ButtonCream"); var button=root.AddComponent<Button>(); button.targetGraphic=root.GetComponent<Image>();
            root.GetComponent<Image>().raycastTarget=true;
            Picture(root.transform,"Icon",icon,(w-38)/2,(h-45)/2,38,45); return button;
        }
        static void ConfigureButton(Button button,bool primary) {
            button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState { highlightedSprite=S(primary?"ButtonOrangeHover":"ButtonCreamHover"), selectedSprite=S(primary?"ButtonOrangeHover":"ButtonCreamHover"), pressedSprite=S(primary?"ButtonOrangePressed":"ButtonCreamHover"), disabledSprite=S("ButtonDisabled") };
        }
        static void SetSkin(Button button,string key) { button.GetComponent<Image>().sprite=S(key); button.transition=Selectable.Transition.ColorTint; }
        static void MoveButtonLabel(Button button,float x,float y,float w,float h,float size) { var label=button.transform.Find("Label").GetComponent<TMP_Text>(); Place(label.rectTransform,x,y,w,h); label.fontSize=size*SX*TypeScale; }
        static void IconInside(Button button,string icon,float x,float y,float w,float h) { Picture(button.transform,"Icon",icon,x,y,w,h); }
        static GameObject InstantiateShared(string name,Transform parent) { return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Shared+name+".prefab"),parent); }
        static RectTransform Group(Transform parent,string name) { var root=new GameObject(name,typeof(RectTransform)); root.transform.SetParent(parent,false); var rt=root.GetComponent<RectTransform>(); Stretch(rt); return rt; }
        static GameObject Panel(Transform parent,string name,float x,float y,float w,float h,string sprite) {
            var root=new GameObject(name,typeof(RectTransform),typeof(Image)); root.transform.SetParent(parent,false); Place(root.GetComponent<RectTransform>(),x,y,w,h);
            var image=root.GetComponent<Image>(); image.sprite=S(sprite); image.type=image.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple; image.raycastTarget=false; return root;
        }
        static Image Picture(Transform parent,string name,string sprite,float x,float y,float w,float h,bool preserve=true) {
            var root=Panel(parent,name,x,y,w,h,sprite); var image=root.GetComponent<Image>(); image.type=Image.Type.Simple; image.preserveAspect=preserve; return image;
        }
        static TMP_Text Label(Transform parent,string name,string value,float x,float y,float w,float h,float size,Color? color=null,TextAlignmentOptions alignment=TextAlignmentOptions.Left) {
            var root=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); root.transform.SetParent(parent,false); Place(root.GetComponent<RectTransform>(),x,y,w,h);
            var text=root.GetComponent<TextMeshProUGUI>(); text.font=font; text.text=value; text.fontSize=size*SX*TypeScale; text.color=color??Ink;
            text.alignment=alignment; text.enableWordWrapping=true; text.overflowMode=TextOverflowModes.Overflow; text.raycastTarget=false;
            text.margin=Vector4.zero; text.extraPadding=true; return text;
        }
        static void Line(Transform parent,float x,float y,float w,float h,Color color) { var go=Panel(parent,"Divider",x,y,w,h,"PixelWhite"); go.GetComponent<Image>().color=color; }
        static void Place(RectTransform rt,float x,float y,float w,float h) { rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1); rt.anchoredPosition=new Vector2(x*SX,-y*SY); rt.sizeDelta=new Vector2(w*SX,h*SY); }
        static void Stretch(RectTransform rt) { rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=rt.offsetMax=Vector2.zero; rt.localScale=Vector3.one; }
        static Sprite S(string name) { if(!sprites.TryGetValue(name,out var sprite)) throw new KeyNotFoundException(name); return sprite; }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out var color); return color; }
        static void Folder(string path) { if(AssetDatabase.IsValidFolder(path)) return; Folder(Path.GetDirectoryName(path).Replace('\\','/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path)); }
        static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); checks.Add(message); }

        static void ValidateStructure(UIScreenRouter router,List<string> paths) {
            Require(paths.Count==13,"Exactly 13 reference screen prefabs were saved");
            Require(router.pages.Select(p=>p.screenId).Distinct().Count()==13,"All screen IDs are unique");
            foreach(var page in router.pages) {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(page.gameObject)==0,"No missing root scripts: "+page.screenId);
                Require(page.GetComponentsInChildren<TMP_Text>(true).Length>0,"Editable TMP text: "+page.screenId);
                Require(page.GetComponentsInChildren<UIRouteButton>(true).Length>0,"Editable navigation: "+page.screenId);
                foreach(var image in page.GetComponentsInChildren<Image>(true)) Require(image.sprite!=null,"Resolved image sprite: "+page.screenId+"/"+image.name);
                foreach(var button in page.GetComponentsInChildren<Button>(true)) Require(button.GetComponent<Image>().raycastTarget,"Button accepts pointer input: "+page.screenId+"/"+button.name);
            }
        }
        static void ValidateNavigation(UIScreenRouter router) {
            router.Initialize(); router.Route(UIRouteAction.Navigate,UIScreenId.Settings); router.Route(UIRouteAction.Navigate,UIScreenId.ControllerConnection); router.Back();
            Require(router.CurrentScreen==UIScreenId.Settings,"Controller Back restores Settings"); router.Back();
            Require(router.CurrentScreen==UIScreenId.HomeDashboard,"Settings Back restores Home");
            router.Route(UIRouteAction.SelectLevel,level:4); Require(router.CurrentScreen==UIScreenId.ReadyForTakeoff&&router.SelectedLevel==4,"Level selection opens Ready with selected level");
            router.Route(UIRouteAction.TryRhythm); router.Route(UIRouteAction.AdvancePractice); Require(router.CurrentScreen==UIScreenId.PracticeExhale,"Practice inhale advances to exhale");
            router.Route(UIRouteAction.Pause); router.Route(UIRouteAction.Navigate,UIScreenId.Settings); router.Back(); router.Route(UIRouteAction.Resume);
            Require(router.CurrentScreen==UIScreenId.PracticeExhale,"Pause > Settings > Back > Resume retains the practice page");
            router.Route(UIRouteAction.AdvancePractice); router.Route(UIRouteAction.AdvancePractice); Require(router.CurrentScreen==UIScreenId.PracticeComplete,"Practice follows crane then completes");
            router.Route(UIRouteAction.StartFlight); router.Route(UIRouteAction.AdvancePractice); Require(router.CurrentScreen==UIScreenId.FlightComplete,"Explicit preview mode reaches flight results");
            router.Route(UIRouteAction.NextLevel); Require(router.CurrentScreen==UIScreenId.ReadyForTakeoff&&router.SelectedLevel==5,"Next Level increments selection and returns to Ready");
            foreach(var page in router.pages) { var id=page.screenId; router.Show(id); Require(router.pages.Count(p=>p.gameObject.activeSelf)==1,"Exactly one active UI screen: "+id); }
            router.Initialize();
        }
        static void CapturePages(GameObject root,UIScreenRouter router) {
            var cameraObject=new GameObject("UI Capture Camera",typeof(Camera)); var camera=cameraObject.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.orthographic=true;
            camera.transform.position=new Vector3(0,0,-10); camera.nearClipPlane=.1f; camera.farClipPlane=100;
            var target=new RenderTexture(1210,834,24,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture=target;
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
            foreach(var page in router.pages) {
                router.Show(page.screenId,false); Canvas.ForceUpdateCanvases();
                foreach(var text in page.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true,true);
                Canvas.ForceUpdateCanvases(); camera.Render();
                var previous=RenderTexture.active; RenderTexture.active=target;
                var capture=new Texture2D(1210,834,TextureFormat.RGB24,false); capture.ReadPixels(new Rect(0,0,1210,834),0,0); capture.Apply();
                File.WriteAllBytes(PreviewDir+((int)page.screenId).ToString("00")+"_"+page.screenId+".png",capture.EncodeToPNG());
                Object.DestroyImmediate(capture); RenderTexture.active=previous;
                Require(new FileInfo(PreviewDir+((int)page.screenId).ToString("00")+"_"+page.screenId+".png").Length>10000,"Canvas capture rendered: "+page.screenId);
            }
            canvas.worldCamera=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; camera.targetTexture=null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
        }
    }
}
