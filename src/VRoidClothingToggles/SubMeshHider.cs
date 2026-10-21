using System.Collections.Generic;
using UnityEngine;

namespace VRoidClothingToggles
{
    /// <summary>
    /// Hides individual sub-meshes (material slots) of renderers at runtime.
    ///
    /// The original mesh is never edited. When something on a renderer is hidden, the
    /// renderer is pointed at an in-memory copy whose hidden sub-meshes have no triangles.
    /// When everything is visible again, the renderer gets its original mesh back.
    /// Nothing is ever written to disk, so the .vrm file is untouched.
    /// </summary>
    internal sealed class SubMeshHider
    {
        sealed class RendererState
        {
            public Renderer Renderer;
            public Mesh Original;
            public Mesh Working;
            public int[][] OriginalIndices;
            public MeshTopology[] Topologies;
            public readonly HashSet<int> Hidden = new HashSet<int>();
            public bool Unreadable;
        }

        static readonly int[] Empty = new int[0];
        readonly Dictionary<Renderer, RendererState> states = new Dictionary<Renderer, RendererState>();

        public void SetVisible(Renderer renderer, int subMesh, bool visible)
        {
            if (renderer == null) return;
            var s = GetState(renderer);
            if (s == null || s.Unreadable) return;
            if (subMesh < 0 || subMesh >= s.OriginalIndices.Length) return;

            bool changed = visible ? s.Hidden.Remove(subMesh) : s.Hidden.Add(subMesh);
            if (changed) Apply(s);
        }

        RendererState GetState(Renderer renderer)
        {
            if (states.TryGetValue(renderer, out var s)) return s;

            var mesh = LayerDetector.GetMesh(renderer);
            if (mesh == null) return null;

            s = new RendererState { Renderer = renderer, Original = mesh };
            if (!mesh.isReadable)
            {
                s.Unreadable = true;
                Plugin.Log?.LogWarning($"Mesh '{mesh.name}' on '{renderer.name}' is not readable; its layers cannot be toggled.");
            }
            else
            {
                int n = mesh.subMeshCount;
                s.OriginalIndices = new int[n][];
                s.Topologies = new MeshTopology[n];
                for (int i = 0; i < n; i++)
                {
                    s.OriginalIndices[i] = mesh.GetIndices(i);
                    s.Topologies[i] = mesh.GetTopology(i);
                }
            }
            states[renderer] = s;
            return s;
        }

        void Apply(RendererState s)
        {
            if (s.Renderer == null) return;

            if (s.Hidden.Count == 0)
            {
                SwapMesh(s.Renderer, s.Original);
                return;
            }

            if (s.Working == null)
            {
                s.Working = Object.Instantiate(s.Original);
                s.Working.name = s.Original.name + " (ClothingToggles)";
            }

            for (int i = 0; i < s.OriginalIndices.Length; i++)
            {
                var indices = s.Hidden.Contains(i) ? Empty : s.OriginalIndices[i];
                // calculateBounds: false keeps the original bounds, so culling stays the same.
                s.Working.SetIndices(indices, s.Topologies[i], i, false);
            }
            SwapMesh(s.Renderer, s.Working);
        }

        static void SwapMesh(Renderer r, Mesh mesh)
        {
            if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == mesh) return;
                // Keep blend shape weights (expressions, body tweaks) across the swap.
                int count = smr.sharedMesh != null ? smr.sharedMesh.blendShapeCount : 0;
                var weights = new float[count];
                for (int i = 0; i < count; i++) weights[i] = smr.GetBlendShapeWeight(i);
                smr.sharedMesh = mesh;
                int max = Mathf.Min(count, mesh != null ? mesh.blendShapeCount : 0);
                for (int i = 0; i < max; i++) smr.SetBlendShapeWeight(i, weights[i]);
            }
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != mesh) mf.sharedMesh = mesh;
            }
        }

        /// <summary>Puts every original mesh back (if the model still exists) and frees the copies.</summary>
        public void RestoreAll()
        {
            foreach (var s in states.Values)
            {
                if (s.Renderer != null && s.Original != null) SwapMesh(s.Renderer, s.Original);
                if (s.Working != null) Object.Destroy(s.Working);
            }
            states.Clear();
        }
    }
}
