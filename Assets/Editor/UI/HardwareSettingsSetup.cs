using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using WingSinger.UI;
using Object = UnityEngine.Object;

namespace WingSinger.Editor {
    public static class HardwareSettingsSetup {
        public const string SettingsPath = "Assets/Settings/Input/WingSingerHardwareSettings.asset";
        public const string PagePath = "Assets/Prefabs/UI/Screens/14_HardwareSettings.prefab";
        const string FontPath = "Assets/Art/UI/Fonts/HardwareStatus SDF.asset";
        static TMP_FontAsset font, statusFont;
        static readonly Color Ink = new Color(0, .294f, .259f);

        [MenuItem("WingSinger/UI/Install Hardware Settings")]
        public static void Apply() {
            UIIntegrationSetup.RequireSavedScenes();
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Temp/HardwareUIBackup");
            foreach (string path in new[] { GameplayNavigationSetup.ScenePath, "Assets/Prefabs/UI/UIFlowRoot.prefab", "Assets/Prefabs/UI/Screens/07_Settings.prefab" }) {
                string backup = "Temp/HardwareUIBackup/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            var settings = AssetDatabase.LoadAssetAtPath<BreathSensorSettings>(SettingsPath);
            if (settings == null) { AssetDatabase.ImportAsset(SettingsPath); settings = AssetDatabase.LoadAssetAtPath<BreathSensorSettings>(SettingsPath); }
            if (settings == null) throw new InvalidOperationException("Independent hardware settings asset is missing.");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/DarumadropOne SDF.asset");
            statusFont = PrepareStatusFont();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PagePath) == null) {
                var page = CreatePage(); PrefabUtility.SaveAsPrefabAsset(page, PagePath); Object.DestroyImmediate(page);
            }
            EditPrefab(PagePath, UpdatePageLayout);
            EditPrefab("Assets/Prefabs/UI/Screens/07_Settings.prefab", ConfigureSettingsEntry);
            foreach (string path in new[] { "Assets/Prefabs/UI/Screens/10_PracticeInhale.prefab", "Assets/Prefabs/UI/Screens/11_PracticeExhale.prefab",
                "Assets/Prefabs/UI/Screens/12_PracticeFollow.prefab", "Assets/Prefabs/UI/Screens/13_PracticeComplete.prefab" }) EditPrefab(path, ConfigureHud);
            EditPrefab("Assets/Prefabs/UI/UIFlowRoot.prefab", root => ConfigureRouter(root.GetComponent<UIScreenRouter>()));
            foreach (string path in new[] { GameplayNavigationSetup.ScenePath, "Assets/Scenes/UI/UIFlowPreview.unity" }) {
                var scene = EditorSceneManager.OpenScene(path);
                ConfigureRouter(Object.FindObjectOfType<UIScreenRouter>());
                var sensor = Object.FindObjectOfType<ArduinoBreathInput>();
                if (sensor != null) { sensor.settings = settings; EditorUtility.SetDirty(sensor); }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? GameplayNavigationSetup.ScenePath : original);
            Debug.Log("WingSinger: independent sensor configuration, Settings > Hardware settings, live ADC HUD installed.");
        }
        static void EditPrefab(string path, Action<GameObject> edit) {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void ConfigureRouter(UIScreenRouter router) {
            if (router == null) throw new InvalidOperationException("UI router missing.");
            var pages = router.GetComponentsInChildren<UIPageView>(true).ToList();
            if (pages.All(page => page.screenId != UIScreenId.HardwareSettings)) {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PagePath), router.transform);
                instance.SetActive(false); pages.Add(instance.GetComponent<UIPageView>());
            }
            foreach (var page in pages) { if (page.screenId == UIScreenId.Settings) ConfigureSettingsEntry(page.gameObject); ConfigureHud(page.gameObject); }
            router.pages = pages.ToArray(); EditorUtility.SetDirty(router); PrefabUtility.RecordPrefabInstancePropertyModifications(router);
        }
        static void ConfigureSettingsEntry(GameObject root) {
            // Replace the previously disabled language placeholder; retain the existing controller status route.
            var row = root.transform.Find("Content/SettingsPanel/SupportSection/Language");
            if (row == null) return;
            row.GetComponent<Button>().interactable = true;
            var route = row.GetComponent<UIRouteButton>(); route.action = UIRouteAction.Navigate; route.destination = UIScreenId.HardwareSettings;
            row.Find("Name").GetComponent<TMP_Text>().text = "Hardware settings";
            row.Find("Value").GetComponent<TMP_Text>().text = "Configure";
            if (row.GetComponent<UIInteractionTween>() == null) row.gameObject.AddComponent<UIInteractionTween>();
            foreach (var component in new Component[] { row.GetComponent<Button>(), route, row.Find("Name").GetComponent<TMP_Text>(), row.Find("Value").GetComponent<TMP_Text>() }) {
                EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        static void ConfigureHud(GameObject root) {
            var hud = root.transform.Find("Header/PracticeHUD"); if (hud == null) return;
            var badge = hud.Find("ConnectionBadge") ?? hud.Find("Connection"); if (badge == null) return;
            Place(badge.GetComponent<RectTransform>(), 1434, 64, 636, 113);
            var label = badge.Find("Label").GetComponent<TMP_Text>();
            Place(label.rectTransform, 88, 8, 534, 97);
            label.font = font; label.fontSize = 36; label.enableAutoSizing = true; label.fontSizeMax = 36; label.fontSizeMin = 28;
            label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = "ADC -- mV\n默认";
            var allBindings = label.GetComponents<UIRuntimeLabel>();
            if (allBindings.Length == 0) allBindings = new[] { label.gameObject.AddComponent<UIRuntimeLabel>() };
            foreach (var binding in allBindings) {
                binding.value = UIValueKind.SensorReading; EditorUtility.SetDirty(binding); PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
            }
            var localized = label.GetComponent<HardwareStatusFont>() ?? label.gameObject.AddComponent<HardwareStatusFont>(); localized.fallback = statusFont;
            foreach (var component in new Component[] { badge.GetComponent<RectTransform>(), label.rectTransform, label, localized }) {
                EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        static void UpdatePageLayout(GameObject root) {
            var panel = root.GetComponent<HardwareSettingsPanel>(); var live = root.transform.Find("Content/Hardware");
            var title = live.Find("Title").GetComponent<TMP_Text>(); Place(title.rectTransform,55,30,690,95); title.fontSize = 46;
            Place(panel.hardwareInfo.rectTransform,55,140,690,410); panel.hardwareInfo.fontSize = 28;
            Place(panel.connectionInfo.rectTransform,55,590,690,225); panel.connectionInfo.fontSize = 28;
            Place(panel.readings.rectTransform,55,835,690,270); panel.readings.fontSize = 30;
            var content = root.transform.Find("Content");
            var messageBox = content.Find("MessageBackground");
            if (messageBox == null) messageBox = Panel(content,"MessageBackground",100,1350,2220,105,"Skins/PanelCream.png").transform;
            messageBox.SetAsFirstSibling(); Place(panel.message.rectTransform,130,1365,2160,80);
            root.transform.Find("Content/Parameters/UsbInhaleLabel").GetComponent<TMP_Text>().text = "USB inhale";
            root.transform.Find("Content/Parameters/UsbExhaleLabel").GetComponent<TMP_Text>().text = "USB exhale";
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true)) {
                dropdown.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Skins/ButtonCream.png");
                dropdown.template.GetComponent<Image>().sprite = dropdown.GetComponent<Image>().sprite;
                dropdown.template.sizeDelta = new Vector2(0,160);
                var viewport = dropdown.template.Find("Viewport");
                viewport.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Skins/PixelWhite.png");
                var item = viewport.Find("Content/Item").GetComponent<RectTransform>(); item.sizeDelta = new Vector2(0,64);
                viewport.Find("Content").GetComponent<RectTransform>().sizeDelta = new Vector2(0,68);
                item.Find("Item Checkmark").GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/IconCheck.png");
                var arrow = dropdown.transform.Find("Arrow"); arrow.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/IconChevronRight.png");
                arrow.localRotation = Quaternion.Euler(0,0,-90);
                dropdown.captionText.enableAutoSizing = true; dropdown.captionText.fontSizeMax = 28; dropdown.captionText.fontSizeMin = 20;
            }
        }
        static TMP_FontAsset PrepareStatusFont() {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); if (existing != null) return existing;
            // Import only while baking; the delivered asset contains five static glyphs, not a system font dependency.
            string temporary = "Assets/Art/UI/Fonts/HardwareBakeSource-" + Guid.NewGuid().ToString("N") + ".ttf";
            try {
                File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "msyh.ttc"), temporary);
                AssetDatabase.ImportAsset(temporary);
                var source = AssetDatabase.LoadAssetAtPath<Font>(temporary);
                var baked = TMP_FontAsset.CreateFontAsset(source, 72, 8, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
                if (baked == null) throw new InvalidOperationException("Cannot bake the Chinese hardware status glyphs.");
                if (!baked.TryAddCharacters("吸气默认呼", out string missing)) throw new InvalidOperationException("Status glyphs missing: " + missing);
                baked.name = "HardwareStatus SDF"; baked.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(baked, FontPath); AssetDatabase.AddObjectToAsset(baked.material, baked);
                foreach (var texture in baked.atlasTextures) AssetDatabase.AddObjectToAsset(texture, baked);
                var serialized = new SerializedObject(baked);
                serialized.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = null;
                serialized.FindProperty("m_SourceFontFileGUID").stringValue = "";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(baked); return baked;
            } finally { AssetDatabase.DeleteAsset(temporary); }
        }
        public static GameObject CreatePage() {
            if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/DarumadropOne SDF.asset");
            if (statusFont == null) statusFont = PrepareStatusFont();
            var root = new GameObject("14_HardwareSettings", typeof(RectTransform)); root.SetActive(false);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(2420, 1668);
            var view = root.AddComponent<UIPageView>(); view.screenId = UIScreenId.HardwareSettings; view.usesReferencePreviewData = false;
            var background = Panel(root.transform, "Background", 0, 0, 2420, 1668, "Backgrounds/ForestValley.png");
            background.GetComponent<Image>().type = Image.Type.Simple;
            var header = Group(root.transform, "Header");
            var back = Button(header, "Back", "< Back", 100, 60, 250); back.gameObject.AddComponent<UIRouteButton>().action = UIRouteAction.Back;
            Label(header, "Title", "Hardware Settings", 500, 55, 1500, 100, 68);
            var content = Group(root.transform, "Content");
            var live = Panel(content, "Hardware", 100, 210, 800, 1120, "Skins/PanelCream.png");
            Label(live.transform, "Title", "Live hardware", 55, 35, 690, 65, 48);
            var panel = root.AddComponent<HardwareSettingsPanel>();
            panel.hardwareInfo = Label(live.transform, "DeviceInfo", "ESP32_C3_Mini\nBLE / CSV notifications", 55, 130, 690, 470, 30);
            panel.connectionInfo = Label(live.transform, "Connection", "Waiting for controller", 55, 630, 690, 190, 30);
            panel.readings = Label(live.transform, "Readings", "ADC -- mV\nAverage -- mV\nJudgement: 默认", 55, 850, 690, 235, 34);
            panel.readings.gameObject.AddComponent<HardwareStatusFont>().fallback = statusFont;
            var edit = Panel(content, "Parameters", 950, 210, 1370, 1120, "Skins/PanelCream.png");
            Label(edit.transform, "Title", "Connection & breathing parameters", 50, 35, 1270, 70, 46);
            var t = edit.transform;
            panel.transport = Dropdown(t, "Transport", "Transport", 50, 150, new[] { "USB serial", "Bluetooth LE" });
            panel.protocol = Dropdown(t, "Protocol", "BLE protocol", 700, 150, new[] { "Hardware CSV", "Legacy UART" });
            panel.deviceName = Field(t, "DeviceName", "BLE name", 50, 255); panel.address = Field(t, "Address", "BLE address", 700, 255);
            panel.center = Field(t, "Center", "Center (mV)", 50, 360); panel.deviation = Field(t, "Deviation", "Deviation (mV)", 700, 360);
            panel.filterSamples = Field(t, "Filter", "Filter samples", 50, 465); panel.freshness = Field(t, "Freshness", "Freshness (s)", 700, 465);
            panel.reconnectDelay = Field(t, "Retry", "Retry delay (s)", 50, 570); panel.inactivity = Field(t, "Inactivity", "Inactivity (s)", 700, 570);
            panel.inhale = Field(t, "UsbInhale", "USB inhale <=", 50, 675); panel.exhale = Field(t, "UsbExhale", "USB exhale >=", 700, 675);
            panel.port = Field(t, "Port", "USB port", 50, 780); panel.handshake = Field(t, "Handshake", "USB wait (s)", 700, 780);
            panel.connectTimeout = Field(t, "BleTimeout", "BLE wait (s)", 50, 885);
            panel.debugLogging = Toggle(t, "Debug", "Console debug", 50, 995);
            panel.keyboardFallback = Toggle(t, "Keyboard", "Keyboard fallback", 700, 995);
            panel.message = Label(content, "Message", "Edits are saved locally.", 110, 1360, 2200, 70, 30);
            panel.reconnect = Button(content, "Reconnect", "Reconnect", 100, 1480, 600);
            panel.restoreDefaults = Button(content, "RestoreDefaults", "Restore defaults", 900, 1480, 600);
            panel.apply = Button(content, "Apply", "Apply & save", 1720, 1480, 600);
            UIViewportSetup.ConfigurePage(view); return root;
        }
        static Transform Group(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Stretch(go.GetComponent<RectTransform>()); return go.transform; }
        static GameObject Panel(Transform parent, string name, float x, float y, float w, float h, string skin) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), x, y, w, h);
            var image = go.GetComponent<Image>(); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/" + skin);
            image.type = Image.Type.Sliced; image.raycastTarget = false; return go;
        }
        static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size) {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), x, y, w, h);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = Ink;
            text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text;
        }
        static Button Button(Transform parent, string name, string title, float x, float y, float width) {
            var go = Panel(parent, name, x, y, width, 105, "Skins/ButtonCream.png"); var image = go.GetComponent<Image>(); image.raycastTarget = true;
            var button = go.AddComponent<Button>(); button.targetGraphic = image; go.AddComponent<UIInteractionTween>();
            var text = Label(go.transform, "Label", title, 20, 10, width - 40, 85, 40); text.alignment = TextAlignmentOptions.Center; return button;
        }
        static TMP_InputField Field(Transform parent, string name, string title, float x, float y) {
            Label(parent, name + "Label", title, x, y, 225, 70, 30);
            var go = Panel(parent, name, x + 235, y, 350, 75, "Skins/PillWhite.png"); go.GetComponent<Image>().raycastTarget = true;
            var input = go.AddComponent<TMP_InputField>(); input.targetGraphic = go.GetComponent<Image>();
            var area = Group(go.transform, "Viewport").GetComponent<RectTransform>(); area.offsetMin = new Vector2(14, 7); area.offsetMax = new Vector2(-14, -7);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Label(area, "Text", "", 0, 0, 320, 60, 30); Stretch(text.rectTransform); text.enableWordWrapping = false;
            input.textViewport = area; input.textComponent = text; input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 96;
            return input;
        }
        static TMP_Dropdown Dropdown(Transform parent, string name, string title, float x, float y, string[] options) {
            // Reuse Unity's standard dropdown template, then apply the project's font and colours.
            Label(parent, name + "Label", title, x, y, 225, 70, 30);
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), x + 235, y, 350, 75);
            var dropdown = go.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(options.ToList());
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSize = 28; text.color = Ink; }
            return dropdown;
        }
        static Toggle Toggle(Transform parent, string name, string title, float x, float y) {
            var go = Panel(parent, name, x, y, 65, 65, "Skins/PanelOutline.png"); go.GetComponent<Image>().raycastTarget = true;
            var check = Panel(go.transform, "Check", 12, 12, 41, 41, "Skins/FillGreen.png").GetComponent<Image>();
            var toggle = go.AddComponent<Toggle>(); toggle.targetGraphic = go.GetComponent<Image>(); toggle.graphic = check;
            Label(parent, name + "Label", title, x + 85, y, 500, 65, 34); return toggle;
        }
        static void Place(RectTransform rt, float x, float y, float w, float h) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); }
        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one; }
    }
}
