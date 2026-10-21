using System.IO;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace VRoidClothingToggles
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "mateengine.vroidclothingtoggles";
        public const string Name = "VRoid Clothing Toggles";
        public const string Version = "1.0.1";

        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;

            var key = Config.Bind("General", "ToggleWindowKey", KeyCode.F9,
                "Key that shows or hides the on-screen list of clothing layers (useful if a model has more than 8). Set to None to disable.");
            var useMenu = Config.Bind("General", "AddToClothesMenu", true,
                "Add the detected layers to MateEngine's built-in Clothes menu.");

            // Run on our own persistent object so we don't depend on BepInEx's manager object surviving.
            var go = new GameObject("VRoidClothingToggles");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            var runtime = go.AddComponent<ToggleRuntime>();
            runtime.Store = new StateStore(Path.Combine(Paths.ConfigPath, "VRoidClothingToggles.states.txt"));
            runtime.WindowKey = key.Value;
            runtime.UseClothesMenu = useMenu.Value;

            Log.LogInfo($"{Name} {Version} loaded.");
        }
    }
}
