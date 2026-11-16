using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using WingSinger.UI;

namespace WingSinger.Editor {
    // Shared art treatment for both existing Prefabs and future targeted UI rebuilds.
    public static class FigmaVisualStyle {
        const string MaterialPath="Assets/Resources/WingSingerUIArt.mat";
        static readonly Color Orange=new Color(199f/255,122f/255,59f/255,1);
        static Material art;
        static Material ArtMaterial {
            get {
                if(art!=null)return art;
                art=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if(art==null) {
                    AssetDatabase.ImportAsset("Assets/Resources/WingSingerUIArt.shader");
                    var shader=Shader.Find("WingSinger/UI Art Color");
                    if(shader==null)throw new InvalidOperationException("UI art color shader was not imported.");
                    art=new Material(shader);AssetDatabase.CreateAsset(art,MaterialPath);
                }
                art.SetFloat("_Saturation",1.15f);EditorUtility.SetDirty(art);return art;
            }
        }
        static void Dirty(Component component) {
            EditorUtility.SetDirty(component);
            if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        public static void Apply(GameObject root) {
            foreach(var button in root.GetComponentsInChildren<Button>(true)) {
                var surface=button.targetGraphic as FigmaSurface;
                if(surface==null)continue;
                var c=surface.color;
                if(c.a<.1f||c.r<c.g+.16f||c.g<c.b+.12f)continue;
                surface.color=Orange;surface.borderColor=new Color(.68f,.35f,.14f,1);surface.SetVerticesDirty();
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=Color.white;
                colors.pressedColor=new Color(.9f,.86f,.8f,1);button.colors=colors;
                Dirty(surface);Dirty(button);
            }
            foreach(var image in root.GetComponentsInChildren<Image>(true)) {
                if(image.sprite==null)continue;
                string path=AssetDatabase.GetAssetPath(image.sprite);
                bool artwork=path.Contains("/Figma20261116/")&&!path.EndsWith(".svg.png",StringComparison.OrdinalIgnoreCase);
                if(!artwork && image.name!="Background")continue;
                image.material=ArtMaterial;Dirty(image);
            }
            foreach(var surface in root.GetComponentsInChildren<FigmaSurface>(true)) if(surface.backdrop!=null) {
                surface.material=ArtMaterial;Dirty(surface);
            }
        }
        public static void ApplySavedAssets() {
            UIIntegrationSetup.RequireSavedScenes();
            foreach(string file in Directory.GetFiles("Assets/Prefabs/UI/Screens","*.prefab")) {
                string path=file.Replace('\\','/');var root=PrefabUtility.LoadPrefabContents(path);
                try {Apply(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/BreathDemoVolume.asset");
            if(profile==null)throw new InvalidOperationException("Gameplay volume profile is missing.");
            if(!profile.TryGet<ColorAdjustments>(out var grading)) {
                grading=profile.Add<ColorAdjustments>(false);AssetDatabase.AddObjectToAsset(grading,profile);
            }
            grading.active=true;grading.saturation.Override(15);EditorUtility.SetDirty(grading);EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(GameplayNavigationSetup.ScenePath);
            var flow=UnityEngine.Object.FindObjectOfType<BreathUIFlowController>();Apply(flow.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("WingSinger: primary buttons are opaque warm orange; gameplay and UI art saturation raised by 15 percent.");
        }
    }
}
