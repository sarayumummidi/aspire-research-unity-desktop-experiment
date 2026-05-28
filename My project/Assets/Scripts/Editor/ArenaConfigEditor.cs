using UnityEditor;
using UnityEngine;

/// Creates the 5 ArenaConfig ScriptableObject assets.
/// Arena 1 is fully open; arenas 2–5 are placeholders to be filled in
/// once you can see the tessellation overlay in Play mode.
public static class ArenaConfigEditor
{
    [MenuItem("ASPIRE/2 - Create Arena Configs")]
    static void CreateConfigs()
    {
        EnsureFolder("Assets", "ArenaConfigs");

        // Arena 1: nearly open (no obstacles)
        Create(1, "Nearly open — complexity ≈ 0", new int[0], new int[0]);

        // Arenas 2–5: skeletons; add obstacleEdgesA/B pairs in the Inspector
        // after verifying triangle IDs using the TessellationDebugger overlay.
        Create(2, "Low complexity — fill in obstacle edges", new int[0], new int[0]);
        Create(3, "Moderate complexity — fill in obstacle edges", new int[0], new int[0]);
        Create(4, "High complexity — fill in obstacle edges", new int[0], new int[0]);
        Create(5, "Dense maze — fill in obstacle edges", new int[0], new int[0]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>("Assets/ArenaConfigs");

        Debug.Log("[ASPIRE] 5 ArenaConfig assets created in Assets/ArenaConfigs/. " +
                  "Use TessellationDebugger gizmos (showCentroids + showEdges) in Play mode " +
                  "to identify triangle IDs, then fill in the edge pairs for arenas 2–5.");
    }

    static void Create(int id, string desc, int[] edgesA, int[] edgesB)
    {
        string path = $"Assets/ArenaConfigs/Arena{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<ArenaConfig>(path) != null) return;

        var cfg = ScriptableObject.CreateInstance<ArenaConfig>();
        cfg.arenaId         = id;
        cfg.description     = desc;
        cfg.obstacleEdgesA  = edgesA;
        cfg.obstacleEdgesB  = edgesB;
        AssetDatabase.CreateAsset(cfg, path);
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
