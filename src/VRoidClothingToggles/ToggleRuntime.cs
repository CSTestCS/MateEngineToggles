using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRoidClothingToggles
{
    /// <summary>
    /// Watches for the active avatar, finds its clothing layers, and wires them into
    /// MateEngine's built-in Clothes menu (plus an optional on-screen panel).
    /// </summary>
    internal sealed class ToggleRuntime : MonoBehaviour
    {
        // MateEngine's Clothes menu shows at most 8 buttons.
        const int MaxMenuButtons = 8;
        const float PollInterval = 0.5f;

        internal StateStore Store;
        internal KeyCode WindowKey = KeyCode.F9;
        internal bool UseClothesMenu = true;

        GameObject trackedModel;
        bool trackedHadModel;
        string modelKey;
        readonly List<ClothingLayer> layers = new List<ClothingLayer>();
        readonly List<GameObject> proxies = new List<GameObject>();
        readonly List<bool> applied = new List<bool>();
        readonly SubMeshHider hider = new SubMeshHider();
        Component menuComponent;
        GameObject proxyRoot;
        float nextPoll;

        bool showWindow;
        Rect windowRect = new Rect(40, 40, 260, 120);
        Vector2 scroll;

        void Awake()
        {
            proxyRoot = new GameObject("VRoidClothingToggles_Layers");
            proxyRoot.hideFlags = HideFlags.HideAndDontSave;
            proxyRoot.transform.SetParent(transform, false);
        }

        bool loggedError;
        bool inputBroken;
        GameObject menuHost;

        void Update()
        {
            try
            {
                if (Time.unscaledTime >= nextPoll)
                {
                    nextPoll = Time.unscaledTime + PollInterval;
                    CheckModel();
                    EnsureMenuHost();
                }
                SyncLayers();
            }
            catch (System.Exception e)
            {
                if (!loggedError) Plugin.Log?.LogError("Unexpected error: " + e);
                loggedError = true;
            }

            if (WindowKey != KeyCode.None && !inputBroken)
            {
                try { if (Input.GetKeyDown(WindowKey)) showWindow = !showWindow; }
                catch (System.Exception e)
                {
                    inputBroken = true;
                    Plugin.Log?.LogWarning("Hotkey disabled (input not available): " + e.Message);
                }
            }
        }

        void CheckModel()
        {
            if (!GameBridge.IsSupported) return;
            var model = GameBridge.GetCurrentModel();

            // Unity's == treats destroyed objects as null, so a removed model is noticed here too.
            bool same = model == trackedModel && (model != null) == trackedHadModel;
            if (same) return;

            Teardown();
            trackedModel = model;
            trackedHadModel = model != null;
            if (model != null)
            {
                Plugin.Log?.LogInfo($"Active model changed: '{model.name}'.");
                Setup(model);
            }
        }

        void Setup(GameObject model)
        {
            if (!GameBridge.IsVrm(model))
            {
                Plugin.Log?.LogInfo($"'{model.name}' is not a VRM model; skipping.");
                return;
            }

            if (GameBridge.HasOwnClothes(model, null))
            {
                Plugin.Log?.LogInfo($"'{model.name}' already has its own clothing setup; leaving it alone.");
                return;
            }

            var found = LayerDetector.Detect(model);
            if (found.Count == 0)
            {
                Plugin.Log?.LogInfo($"No clothing layers found on '{model.name}'. Materials on this model:");
                foreach (var r in LayerDetector.GetMeshRenderers(model))
                {
                    var mats = new List<string>();
                    foreach (var m in r.sharedMaterials) mats.Add(m != null ? m.name : "(none)");
                    Plugin.Log?.LogInfo($"  {r.name}: {string.Join(" | ", mats.ToArray())}");
                }
                return;
            }

            var path = GameBridge.GetSelectedModelPath();
            modelKey = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : model.name;

            foreach (var layer in found)
            {
                bool visible = !Store.IsHidden(modelKey, layer.Key);
                var proxy = new GameObject("Layer_" + layer.Label);
                proxy.hideFlags = HideFlags.HideAndDontSave;
                proxy.transform.SetParent(proxyRoot.transform, false);
                proxy.SetActive(visible);

                layers.Add(layer);
                proxies.Add(proxy);
                applied.Add(true); // geometry starts fully visible
                ApplyLayer(layers.Count - 1, visible, save: false);
            }

            if (UseClothesMenu)
            {
                int n = Mathf.Min(layers.Count, MaxMenuButtons);
                var names = new string[n];
                var objs = new GameObject[n];
                for (int i = 0; i < n; i++) { names[i] = layers[i].Label; objs[i] = proxies[i]; }

                pendingNames = names;
                pendingProxies = objs;
                EnsureMenuHost();

                if (layers.Count > MaxMenuButtons)
                    Plugin.Log?.LogInfo($"'{model.name}' has {layers.Count} layers; the Clothes menu shows the first {MaxMenuButtons}. Press {WindowKey} for all of them.");
            }

            var labels = new List<string>();
            foreach (var l in layers) labels.Add(l.Label);
            Plugin.Log?.LogInfo($"'{model.name}': found {layers.Count} clothing layer(s): {string.Join(", ", labels.ToArray())}");
        }

        string[] pendingNames;
        GameObject[] pendingProxies;

        /// <summary>
        /// The radial menu shows the Clothes button only when the animator it tracks has an
        /// MEClothes component. Keep ours on that exact object, even if the game swaps it later.
        /// </summary>
        void EnsureMenuHost()
        {
            if (pendingNames == null || trackedModel == null) return;

            var modelTr = trackedModel.transform;
            GameObject host = null;
            var menuAnim = GameBridge.GetMenuAnimator();
            if (menuAnim != null && (menuAnim.transform.IsChildOf(modelTr) || modelTr.IsChildOf(menuAnim.transform)))
                host = menuAnim.gameObject;
            if (host == null)
            {
                var a = trackedModel.GetComponentInChildren<Animator>();
                host = a != null ? a.gameObject : trackedModel;
            }

            if (menuComponent != null && menuHost == host) return;

            if (menuComponent != null) Destroy(menuComponent);
            menuComponent = GameBridge.AddClothesMenu(host, pendingNames, pendingProxies);
            menuHost = host;
            Plugin.Log?.LogInfo(menuComponent != null
                ? $"Added Clothes menu to '{host.name}' (menu animator: {(menuAnim != null ? menuAnim.name : "none found")})."
                : "Could not add the Clothes menu component.");
        }

        void Teardown()
        {
            pendingNames = null;
            pendingProxies = null;
            menuHost = null;
            hider.RestoreAll();
            foreach (var p in proxies) if (p != null) Destroy(p);
            proxies.Clear();
            layers.Clear();
            applied.Clear();
            if (menuComponent != null) Destroy(menuComponent);
            menuComponent = null;
            modelKey = null;
        }

        /// <summary>The Clothes menu switches proxy objects on/off; mirror that onto the model.</summary>
        void SyncLayers()
        {
            for (int i = 0; i < proxies.Count; i++)
            {
                var p = proxies[i];
                if (p == null) continue;
                bool want = p.activeSelf;
                if (want != applied[i]) ApplyLayer(i, want, save: true);
            }
        }

        void ApplyLayer(int index, bool visible, bool save)
        {
            foreach (var t in layers[index].Targets)
                hider.SetVisible(t.Renderer, t.SubMesh, visible);
            applied[index] = visible;
            if (save && modelKey != null) Store.Set(modelKey, layers[index].Key, !visible);
        }

        void SetLayer(int index, bool visible)
        {
            if (proxies[index] != null) proxies[index].SetActive(visible);
            ApplyLayer(index, visible, save: true);
        }

        void OnGUI()
        {
            if (!showWindow) return;
            float h = Mathf.Min(70 + Mathf.Max(1, layers.Count) * 24, Screen.height - 80);
            windowRect.height = h;
            windowRect = GUILayout.Window(0x5643_5447, windowRect, DrawWindow, "Clothing Layers");
        }

        void DrawWindow(int id)
        {
            if (layers.Count == 0)
            {
                GUILayout.Label("No toggleable layers on the current model.");
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Show all")) for (int i = 0; i < layers.Count; i++) SetLayer(i, true);
                if (GUILayout.Button("Hide all")) for (int i = 0; i < layers.Count; i++) SetLayer(i, false);
                GUILayout.EndHorizontal();

                scroll = GUILayout.BeginScrollView(scroll);
                for (int i = 0; i < layers.Count; i++)
                {
                    bool now = applied[i];
                    bool next = GUILayout.Toggle(now, " " + layers[i].Label);
                    if (next != now) SetLayer(i, next);
                }
                GUILayout.EndScrollView();
            }
            if (GUILayout.Button("Close")) showWindow = false;
            GUI.DragWindow();
        }

        void OnDestroy()
        {
            Teardown();
            if (proxyRoot != null) Destroy(proxyRoot);
        }
    }
}
