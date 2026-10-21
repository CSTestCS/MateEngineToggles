using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VRoidClothingToggles
{
    /// <summary>One piece of geometry that belongs to a layer: a single sub-mesh of a renderer.</summary>
    internal sealed class LayerTarget
    {
        public Renderer Renderer;
        public int SubMesh;
    }

    /// <summary>A toggleable clothing layer (for example "Tops" or "Shoes").</summary>
    internal sealed class ClothingLayer
    {
        public string Key;    // stable id used for saving state
        public string Label;  // text shown on the toggle button
        public readonly List<LayerTarget> Targets = new List<LayerTarget>();
    }

    /// <summary>
    /// Finds the clothing layers of a loaded VRoid model by reading its material names.
    ///
    /// VRoid Studio keeps most clothing inside one "Body" mesh, as separate material slots
    /// (sub-meshes) named like "N00_002_01_Tops_01_CLOTH". Each such slot is a layer.
    /// Nothing about the model is changed here; this only reads it.
    /// </summary>
    internal static class LayerDetector
    {
        // N00_002_01_Tops_01_CLOTH, N00_010_01_Onepiece_00_CLOTH_02 (Instance), ...
        static readonly Regex VRoidMaterial = new Regex(
            @"^(?<id>N\d+_\d+_\d+)_(?<part>[A-Za-z]+)_(?<variant>\d+)_(?<kind>[A-Za-z]+)",
            RegexOptions.Compiled);

        // VRoid material kinds that are part of the body itself and must never be toggled.
        static readonly HashSet<string> BodyKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SKIN", "FACE", "EYE", "HAIR"
        };

        // Words that mark clothing/accessories when a model does not follow VRoid naming.
        static readonly string[] ClothingWords =
        {
            "cloth", "tops", "top_", "shirt", "jacket", "coat", "hoodie", "sweater", "vest",
            "bottoms", "pants", "skirt", "shorts", "onepiece", "dress", "shoes", "shoe",
            "boots", "socks", "sock", "stocking", "tights", "gloves", "glove", "hat", "cap",
            "accessory", "accessories", "ribbon", "necktie", "tie_", "scarf", "glasses",
            "outfit", "costume", "uniform", "apron", "cape", "bag"
        };

        public static List<ClothingLayer> Detect(GameObject model)
        {
            var layers = new List<ClothingLayer>();
            if (model == null) return layers;

            var byKey = new Dictionary<string, ClothingLayer>(StringComparer.Ordinal);
            var renderers = GetMeshRenderers(model);

            // 1) VRoid material naming (the normal case).
            foreach (var r in renderers)
            {
                var mesh = GetMesh(r);
                if (mesh == null) continue;
                var mats = r.sharedMaterials;
                int count = Mathf.Min(mesh.subMeshCount, mats.Length);
                for (int i = 0; i < count; i++)
                {
                    var name = CleanName(mats[i] != null ? mats[i].name : null);
                    if (string.IsNullOrEmpty(name)) continue;

                    var m = VRoidMaterial.Match(name);
                    if (!m.Success) continue;

                    string part = m.Groups["part"].Value;
                    string kind = m.Groups["kind"].Value;
                    if (!IsVRoidClothing(part, kind)) continue;

                    // Materials like "..._CLOTH" and "..._CLOTH_01" are the same item.
                    string key = "vroid:" + m.Value;
                    if (!byKey.TryGetValue(key, out var layer))
                    {
                        layer = new ClothingLayer { Key = key, Label = Prettify(part) };
                        byKey[key] = layer;
                        layers.Add(layer);
                    }
                    layer.Targets.Add(new LayerTarget { Renderer = r, SubMesh = i });
                }
            }

            // 2) Fallback for models that do not use VRoid naming: separate clothing objects.
            if (layers.Count == 0)
            {
                foreach (var r in renderers)
                {
                    var mesh = GetMesh(r);
                    if (mesh == null) continue;
                    string objName = r.gameObject.name;
                    if (!LooksLikeClothing(objName)) continue;

                    string key = "object:" + GetPath(model.transform, r.transform);
                    var layer = new ClothingLayer { Key = key, Label = Prettify(objName) };
                    for (int i = 0; i < mesh.subMeshCount; i++)
                        layer.Targets.Add(new LayerTarget { Renderer = r, SubMesh = i });
                    layers.Add(layer);
                }
            }

            MakeLabelsUnique(layers);
            return layers;
        }

        static bool IsVRoidClothing(string part, string kind)
        {
            if (kind.Equals("CLOTH", StringComparison.OrdinalIgnoreCase)) return true;
            if (BodyKinds.Contains(kind)) return false;
            // Unknown kinds (for example accessories in newer VRoid versions): accept if the part looks like clothing.
            return LooksLikeClothing(part);
        }

        static bool LooksLikeClothing(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n == "body" || n == "face" || n == "hair") return false;
            for (int i = 0; i < ClothingWords.Length; i++)
                if (n.Contains(ClothingWords[i])) return true;
            return false;
        }

        internal static List<Renderer> GetMeshRenderers(GameObject model)
        {
            var list = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer || r is MeshRenderer) list.Add(r);
            }
            return list;
        }

        internal static Mesh GetMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                return mf != null ? mf.sharedMesh : null;
            }
            return null;
        }

        internal static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            name = name.Trim();
            const string inst = "(Instance)";
            while (name.EndsWith(inst, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - inst.Length).TrimEnd();
            return name;
        }

        /// <summary>"Onepiece" -> "Onepiece", "HairAccessory" -> "Hair Accessory", "my_jacket" -> "My Jacket".</summary>
        internal static string Prettify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Layer";
            var sb = new StringBuilder();
            char prev = '\0';
            foreach (char c in raw.Replace('_', ' ').Replace('-', ' ').Replace('.', ' '))
            {
                if (char.IsUpper(c) && prev != '\0' && char.IsLower(prev)) sb.Append(' ');
                sb.Append(c);
                prev = c;
            }
            var words = sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1));
            var result = string.Join(" ", words.ToArray());
            return string.IsNullOrEmpty(result) ? "Layer" : result;
        }

        static void MakeLabelsUnique(List<ClothingLayer> layers)
        {
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in layers)
            {
                if (seen.TryGetValue(l.Label, out int n))
                {
                    seen[l.Label] = n + 1;
                    l.Label = l.Label + " " + (n + 1);
                }
                else seen[l.Label] = 1;
            }
        }

        static string GetPath(Transform root, Transform t)
        {
            var parts = new List<string>();
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
