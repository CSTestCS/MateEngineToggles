using System;
using System.Reflection;
using UnityEngine;

namespace VRoidClothingToggles
{
    /// <summary>
    /// Talks to MateEngine's own classes (VRMLoader, MEClothes, SaveLoadHandler) through
    /// reflection, so the plugin does not need to be rebuilt for every game update.
    /// </summary>
    internal static class GameBridge
    {
        static Type vrmLoaderType, meClothesType, outfitEntryType, saveLoadType;
        static MethodInfo getCurrentModel;
        static bool resolved;
        static UnityEngine.Object loader;

        public static Type MEClothesType { get { Resolve(); return meClothesType; } }

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            vrmLoaderType = FindType("VRMLoader");
            meClothesType = FindType("MEClothes");
            saveLoadType = FindType("SaveLoadHandler");
            if (vrmLoaderType != null)
                getCurrentModel = vrmLoaderType.GetMethod("GetCurrentModel", BindingFlags.Instance | BindingFlags.Public);
            if (meClothesType != null)
                outfitEntryType = meClothesType.GetNestedType("OutfitEntry", BindingFlags.Public);

            if (vrmLoaderType == null || getCurrentModel == null)
                Plugin.Log?.LogError("MateEngine's VRMLoader was not found. This game version is not supported.");
            if (meClothesType == null || outfitEntryType == null)
                Plugin.Log?.LogError("MateEngine's MEClothes was not found. This game version is not supported.");
        }

        public static bool IsSupported
        {
            get { Resolve(); return getCurrentModel != null && outfitEntryType != null; }
        }

        /// <summary>The custom model the user loaded (.vrm or .me), or null while the default avatar is shown.</summary>
        public static GameObject GetCurrentModel()
        {
            Resolve();
            if (getCurrentModel == null) return null;
            if (loader == null)
            {
                loader = UnityEngine.Object.FindFirstObjectByType(vrmLoaderType);
                if (loader == null) return null;
            }
            try { return getCurrentModel.Invoke(loader, null) as GameObject; }
            catch { return null; }
        }

        public static bool IsVrm(GameObject model)
        {
            foreach (var c in model.GetComponents<Component>())
            {
                if (c == null) continue;
                var n = c.GetType().Name;
                if (n == "VRMMeta" || n == "Vrm10Instance") return true;
            }
            return false;
        }

        /// <summary>True if the model already ships its own clothing setup (made with the ME exporter).</summary>
        public static bool HasOwnClothes(GameObject model, Component ours)
        {
            Resolve();
            if (meClothesType == null) return false;
            foreach (var c in model.GetComponentsInChildren(meClothesType, true))
                if (c != null && c != ours) return true;
            return false;
        }

        /// <summary>
        /// Adds MateEngine's MEClothes component to the avatar so the game's built-in
        /// "Clothes" menu button appears, with one button per layer.
        /// Each entry switches one of our empty proxy objects on or off; the plugin watches
        /// those proxies and hides or shows the matching layer.
        /// </summary>
        public static Component AddClothesMenu(GameObject host, string[] names, GameObject[] proxies)
        {
            Resolve();
            if (meClothesType == null || outfitEntryType == null) return null;

            var comp = host.AddComponent(meClothesType);
            var entries = Array.CreateInstance(outfitEntryType, names.Length);
            var fName = outfitEntryType.GetField("name");
            var fTag = outfitEntryType.GetField("tag");
            var fObjs = outfitEntryType.GetField("gameObjects");
            for (int i = 0; i < names.Length; i++)
            {
                var e = Activator.CreateInstance(outfitEntryType);
                fName?.SetValue(e, names[i]);
                fTag?.SetValue(e, "");
                fObjs?.SetValue(e, new[] { proxies[i] });
                entries.SetValue(e, i);
            }
            meClothesType.GetField("entries")?.SetValue(comp, entries);
            return comp;
        }

        /// <summary>The path of the selected model (used as a key for saved layer states).</summary>
        public static string GetSelectedModelPath()
        {
            Resolve();
            try
            {
                if (saveLoadType == null) return null;
                var inst = GetMember(saveLoadType, null, "Instance");
                var data = inst != null ? GetMember(saveLoadType, inst, "data") : null;
                return data != null ? GetMember(data.GetType(), data, "selectedModelPath") as string : null;
            }
            catch { return null; }
        }

        static object GetMember(Type t, object target, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var f = t.GetField(name, flags);
            if (f != null) return f.GetValue(target);
            var p = t.GetProperty(name, flags);
            return p != null ? p.GetValue(target, null) : null;
        }

        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(name, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
