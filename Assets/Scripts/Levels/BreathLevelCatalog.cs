using System;
using System.Collections.Generic;
using UnityEngine;

namespace WingSinger {
    [CreateAssetMenu(menuName = "WingSinger/Level Catalog")]
    public sealed class BreathLevelCatalog : ScriptableObject {
        public BreathLevelDefinition[] levels = Array.Empty<BreathLevelDefinition>();
        public int Count => levels == null ? 0 : levels.Length;
        public BreathLevelDefinition At(int index) => index >= 0 && index < Count ? levels[index] : null;
        public int IndexOf(string id) {
            for (int i = 0; i < Count; i++) if (levels[i] != null && levels[i].levelId == id) return i;
            return -1;
        }
        public void Validate() {
            if (Count == 0 || Count > 12) throw new InvalidOperationException("The current level selector supports 1 to 12 configured levels.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var level in levels) {
                if (level == null) throw new InvalidOperationException("Level catalog contains an empty definition.");
                level.Validate();
                if (!ids.Add(level.levelId)) throw new InvalidOperationException("Duplicate level ID: " + level.levelId);
            }
        }
    }
}
