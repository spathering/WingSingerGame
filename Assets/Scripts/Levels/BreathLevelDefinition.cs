using System;
using UnityEngine;

namespace WingSinger {
    [CreateAssetMenu(menuName = "WingSinger/Level Definition")]
    public sealed class BreathLevelDefinition : ScriptableObject {
        [Tooltip("Stable save identifier. Keep unchanged after publishing a level.")]
        public string levelId;
        public string title;
        [TextArea] public string description;
        public Sprite thumbnail;
        public BreathLevel prefab;
        public BreathLevelSettings settings;
        public bool availableFromStart = true;
        [Tooltip("Authoring reference only. Runtime activates the registered Prefab instance in the main scene.")]
        public string sourceScenePath;
        public void Validate() {
            if (string.IsNullOrWhiteSpace(levelId) || string.IsNullOrWhiteSpace(title) || prefab == null || settings == null)
                throw new InvalidOperationException("Level definition needs an ID, title, Prefab and breathing settings: " + name);
            if (prefab.settings != settings) throw new InvalidOperationException("Level Prefab and definition reference different settings: " + name);
            settings.Validate();
        }
    }
}
