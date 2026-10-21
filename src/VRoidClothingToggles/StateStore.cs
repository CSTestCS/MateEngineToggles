using System;
using System.Collections.Generic;
using System.IO;

namespace VRoidClothingToggles
{
    /// <summary>
    /// Remembers which layers are hidden, per model, in a small text file inside
    /// BepInEx/config. Only hidden layers are stored; everything else is visible.
    /// Format: one "modelKey<TAB>layerKey" line per hidden layer.
    /// </summary>
    internal sealed class StateStore
    {
        readonly string path;
        readonly HashSet<string> hidden = new HashSet<string>(StringComparer.Ordinal);

        public StateStore(string path)
        {
            this.path = path;
            Load();
        }

        public bool IsHidden(string model, string layer) => hidden.Contains(model + "\t" + layer);

        public void Set(string model, string layer, bool isHidden)
        {
            string k = model + "\t" + layer;
            bool changed = isHidden ? hidden.Add(k) : hidden.Remove(k);
            if (changed) Save();
        }

        void Load()
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.IndexOf('\t') > 0) hidden.Add(line);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("Could not read saved layer states: " + e.Message);
            }
        }

        void Save()
        {
            try
            {
                var lines = new List<string>(hidden);
                lines.Sort(StringComparer.Ordinal);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("Could not save layer states: " + e.Message);
            }
        }
    }
}
